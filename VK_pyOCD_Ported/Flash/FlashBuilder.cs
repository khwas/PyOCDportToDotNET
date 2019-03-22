using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace openocd.Flash
{
    public class FlashBuilder
    {
        internal Flash flash;
        internal UInt32 flash_start;
        internal List<FlashBuilderConsts.flash_operation> flash_operation_list;
        internal List<FlashBuilderConsts.flash_page> page_list;
        internal FlashBuilderConsts.ProgrammingInfo perf;
        internal bool enable_double_buffering;
        internal byte max_errors;
        internal UInt32 chip_erase_count;
        internal double chip_erase_weight;
        internal UInt32 page_erase_count;
        internal double page_erase_weight;

        public const byte FLASH_PAGE_ERASE = 1;
        public const byte FLASH_CHIP_ERASE = 2;
        public const string FLASH_ANALYSIS_CRC32 = "CRC32";
        public const string FLASH_ANALYSIS_PARTIAL_PAGE_READ = "PAGE_READ";

        public FlashBuilder(Flash flash, UInt32 base_addr = 0)
        {
            this.flash = flash;
            this.flash_start = base_addr;
            this.flash_operation_list = new List<FlashBuilderConsts.flash_operation>();
            this.page_list = new List<FlashBuilderConsts.flash_page>();
            this.perf = new FlashBuilderConsts.ProgrammingInfo();
            this.enable_double_buffering = true;
            this.max_errors = 10;
        }

        public virtual void enableDoubleBuffer(bool enable)
        {
            this.enable_double_buffering = enable;
        }

        public virtual void setMaxErrors(byte count)
        {
            this.max_errors = count;
        }

        // 
        //         Add a block of data to be programmed
        // 
        //         Note - programming does not start until the method
        //         program is called.
        //         
        public virtual void addData(UInt32 addr, List<byte> data)
        {
            // Sanity check
            if (addr < this.flash_start)
            {
                throw new Exception(String.Format("Invalid flash address 0x{0:X} is before flash start 0x{1:X}", addr, this.flash_start));
            }
            // Add operation to list
            this.flash_operation_list.Add(new FlashBuilderConsts.flash_operation(addr, data));
            // Keep list sorted
            this.flash_operation_list = this.flash_operation_list.OrderBy(operation => operation.addr).ToList();
            // Verify this does not overlap
            FlashBuilderConsts.flash_operation prev_flash_operation = null;
            foreach (var operation in this.flash_operation_list)
            {
                if (prev_flash_operation != null)
                {
                    if (prev_flash_operation.addr + prev_flash_operation.data.Count > operation.addr)
                    {
                        throw new ArgumentOutOfRangeException(String.Format("Error adding data - Data at 0x{0:x}..0x{1:x} overlaps with 0x{2:x}..0x{3:x}", prev_flash_operation.addr, prev_flash_operation.addr + prev_flash_operation.data.Count, operation.addr, operation.addr + operation.data.Count));
                    }
                }
                prev_flash_operation = operation;
            }
        }

        // 
        //         Determine fastest method of flashing and then run flash programming.
        // 
        //         Data must have already been added with addData
        //         
        public virtual FlashBuilderConsts.ProgrammingInfo program(bool? chip_erase = null, Action<double> progress_cb = null, bool smart_flash = true, bool fast_verify = false)
        {
            // Assumptions
            // 1. Page erases must be on page boundaries ( page_erase_addr % page_size == 0 )
            // 2. Page erase can have a different size depending on location
            // 3. It is safe to program a page with less than a page of data
            // Examples
            // - lpc4330     -Non 0 base address
            // - nRF51       -UICR location far from flash (address 0x10001000)
            // - LPC1768     -Different sized pages
            DateTime program_start = DateTime.Now;
            progress_cb = progress_cb ?? FlashBuilderConsts._stub_progress;
            // There must be at least 1 flash operation
            if (this.flash_operation_list.Count == 0)
            {
                Trace.TraceWarning("No pages were programmed");
                return null;
            }
            // Convert the list of flash operations into flash pages
            UInt32 program_byte_count = 0;
            UInt32 flash_addr = this.flash_operation_list[0].addr;
            FlashConsts.PageInfo info = this.flash.getPageInfo(flash_addr);
            UInt32 page_addr = flash_addr - flash_addr % (UInt32)info.size;
            var current_page = new FlashBuilderConsts.flash_page(page_addr, (UInt32)info.size, new List<byte>(), (double)info.erase_weight, (double)info.program_weight);
            this.page_list.Add(current_page);
            foreach (FlashBuilderConsts.flash_operation flash_op in this.flash_operation_list)
            {
                UInt32 pos = 0;
                while (pos < flash_op.data.Count)
                {
                    // Check if operation is in next page
                    flash_addr = flash_op.addr + pos;
                    if (flash_addr >= current_page.addr + current_page.size)
                    {
                        info = this.flash.getPageInfo(flash_addr);
                        page_addr = flash_addr - flash_addr % (UInt32)info.size;
                        current_page = new FlashBuilderConsts.flash_page(page_addr, (UInt32)info.size, new List<byte>(), (double)info.erase_weight, (double)info.program_weight);
                        this.page_list.Add(current_page);
                    }
                    // Fill the page gap if there is one
                    UInt32 page_data_end = current_page.addr + (UInt32)current_page.data.Count;
                    if (flash_addr != page_data_end)
                    {
                        List<byte> old_data = this.flash.target.readBlockMemoryUnaligned8(page_data_end, flash_addr - page_data_end);
                        current_page.data.AddRange(old_data);
                    }
                    // Copy data to page and increment pos
                    UInt32 space_left_in_page = (UInt32)(info.size - current_page.data.Count);
                    UInt32 space_left_in_data = (UInt32)flash_op.data.Count - pos;
                    UInt32 amount = Math.Min(space_left_in_page, space_left_in_data);
                    current_page.data.AddRange(flash_op.data.GetRange((int)pos, (int)amount));
                    program_byte_count += amount;
                    //increment position
                    pos += amount;
                }
            }
            // If smart flash was set to false then mark all pages
            // as requiring programming
            if (!smart_flash)
            {
                this._mark_all_pages_for_programming();
            }
            // If the first page being programmed is not the first page
            // in ROM then don't use a chip erase
            if (this.page_list[0].addr > this.flash_start)
            {
                if (chip_erase == null)
                {
                    chip_erase = false;
                }
                else if (chip_erase == true)
                {
                    Trace.TraceWarning("Chip erase used when flash address 0x{0:X} is not the same as flash start 0x{1:X}", this.page_list[0].addr, this.flash_start);
                }
            }
            this.flash.init();
            var _tup_1 = this._compute_chip_erase_pages_and_weight();
            var chip_erase_count = _tup_1.Item1;
            TimeSpan chip_erase_program_time = TimeSpan.FromSeconds(_tup_1.Item2);
            TimeSpan page_erase_min_program_time = TimeSpan.FromSeconds(this._compute_page_erase_pages_weight_min());
            // If chip_erase hasn't been specified determine if chip erase is faster
            // than page erase regardless of contents
            if (chip_erase == null && chip_erase_program_time < page_erase_min_program_time)
            {
                chip_erase = true;
            }

            TimeSpan page_program_time = TimeSpan.Zero;
            UInt32 sector_erase_count = 0;
            // If chip erase isn't True then analyze the flash
            if (!(bool)chip_erase)
            {
                DateTime analyze_start = DateTime.Now;
                if ((bool)this.flash.getFlashInfo().crc_supported)
                {
                    var _tup_2 = this._compute_page_erase_pages_and_weight_crc32(fast_verify);
                    sector_erase_count = _tup_2.Item1;
                    page_program_time = TimeSpan.FromSeconds(_tup_2.Item2);
                    this.perf.analyze_type = FlashBuilder.FLASH_ANALYSIS_CRC32;
                }
                else
                {
                    var _tup_3 = this._compute_page_erase_pages_and_weight_sector_read();
                    sector_erase_count = _tup_3.Item1;
                    page_program_time = TimeSpan.FromSeconds(_tup_3.Item2);
                    this.perf.analyze_type = FlashBuilder.FLASH_ANALYSIS_PARTIAL_PAGE_READ;
                }
                DateTime analyze_finish = DateTime.Now;
                this.perf.analyze_time = analyze_finish - analyze_start;
                Trace.TraceInformation(String.Format("Analyze time: {0}", analyze_finish - analyze_start));
            }
            // If chip erase hasn't been set then determine fastest method to program
            if (chip_erase == null)
            {
                Trace.TraceInformation(String.Format("Chip erase count {0}, Page erase est count {1}", chip_erase_count, sector_erase_count));
                //Trace.TraceInformation(String.Format("Chip erase weight {0}, Page erase weight {2}", chip_erase_program_time, page_program_time));
                chip_erase = chip_erase_program_time < page_program_time;
            }

            byte? flash_operation = null;
            if ((bool)chip_erase)
            {
                if (this.flash.isDoubleBufferingSupported() && this.enable_double_buffering)
                {
                    Trace.TraceInformation("Using double buffer chip erase program");
                    flash_operation = this._chip_erase_program_double_buffer(progress_cb);
                }
                else
                {
                    flash_operation = this._chip_erase_program(progress_cb);
                }
            }
            else if (this.flash.isDoubleBufferingSupported() && this.enable_double_buffering)
            {
                Trace.TraceInformation("Using double buffer page erase program");
                flash_operation = this._page_erase_program_double_buffer(progress_cb);
            }
            else
            {
                flash_operation = this._page_erase_program(progress_cb);
            }
            this.flash.target.resetStopOnReset();
            DateTime program_finish = DateTime.Now;
            this.perf.program_time = program_finish - program_start;
            this.perf.program_type = flash_operation.ToString();
            Trace.TraceInformation("Programmed {0} bytes ({1} pages) at {2:0.00} kB/s", program_byte_count, this.page_list.Count, program_byte_count / 1024 / ((TimeSpan)this.perf.program_time).TotalSeconds);
            return this.perf;
        }

        public virtual FlashBuilderConsts.ProgrammingInfo getPerformance()
        {
            return this.perf;
        }

        public virtual void _mark_all_pages_for_programming()
        {
            foreach (var page in this.page_list)
            {
                page.erased = false;
                page.same = false;
            }
        }

        // 
        //         Compute the number of erased pages.
        // 
        //         Determine how many pages in the new data are already erased.
        //         
        public virtual Tuple<UInt32, double> _compute_chip_erase_pages_and_weight()
        {
            UInt32 chip_erase_count = 0;
            double chip_erase_weight = 0;
            chip_erase_weight += (double)this.flash.getFlashInfo().erase_weight;
            foreach (var page in this.page_list)
            {
                if (page.erased == null)
                {
                    page.erased = FlashBuilderConsts._erased(page.data);
                }
                if (!(bool)page.erased)
                {
                    chip_erase_count += 1;
                    chip_erase_weight += page.getProgramWeight();
                }
            }
            this.chip_erase_count = chip_erase_count;
            this.chip_erase_weight = chip_erase_weight;
            return Tuple.Create(chip_erase_count, chip_erase_weight);
        }

        public virtual double _compute_page_erase_pages_weight_min()
        {
            double page_erase_min_weight = 0;
            foreach (var page in this.page_list)
            {
                page_erase_min_weight += page.getVerifyWeight();
            }
            return page_erase_min_weight;
        }

        // 
        //         Estimate how many pages are the same.
        // 
        //         Quickly estimate how many pages are the same.  These estimates are used
        //         by page_erase_program so it is recommended to call this before beginning programming
        //         This is done automatically by smart_program.
        //         
        public virtual Tuple<UInt32, double> _compute_page_erase_pages_and_weight_sector_read()
        {
            // Quickly estimate how many pages are the same
            UInt32 page_erase_count = 0;
            double page_erase_weight = 0;
            foreach (var page in this.page_list)
            {
                // Analyze pages that haven't been analyzed yet
                if (page.same == null)
                {
                    UInt32 size = Math.Min(FlashBuilderConsts.PAGE_ESTIMATE_SIZE, (UInt32)page.data.Count);
                    List<byte> data = this.flash.target.readBlockMemoryUnaligned8(page.addr, size);
                    bool page_same = FlashBuilderConsts._same(data, page.data.GetRange(0, (int)size));
                    if (page_same == false)
                    {
                        page.same = false;
                    }
                }
            }
            // Put together page and time estimate
            foreach (var page in this.page_list)
            {
                if (page.same == false)
                {
                    page_erase_count += 1;
                    page_erase_weight += page.getEraseProgramWeight();
                }
                else if (page.same == null)
                {
                    // Page is probably the same but must be read to confirm
                    page_erase_weight += page.getVerifyWeight();
                }
                else if (page.same == true)
                {
                    // Page is confirmed to be the same so no programming weight
                }
            }
            this.page_erase_count = page_erase_count;
            this.page_erase_weight = page_erase_weight;
            return Tuple.Create(page_erase_count, page_erase_weight);
        }


        private UInt32 crc32(IEnumerable<byte> data) => throw new NotImplementedException();

        // 
        //         Estimate how many pages are the same.
        // 
        //         Quickly estimate how many pages are the same.  These estimates are used
        //         by page_erase_program so it is recommended to call this before beginning programming
        //         This is done automatically by smart_program.
        // 
        //         If assume_estimate_correct is set to True, then pages with matching CRCs
        //         will be marked as the same.  There is a small chance that the CRCs match even though the
        //         data is different, but the odds of this happing are low: ~1/(2^32) = ~2.33*10^-8%.
        //         
        public virtual Tuple<UInt32, double> _compute_page_erase_pages_and_weight_crc32(bool assume_estimate_correct = false)
        {
            // Build list of all the pages that need to be analyzed
            List<Tuple<UInt32, UInt32>> sector_list = new List<Tuple<UInt32, UInt32>>();
            List<FlashBuilderConsts.flash_page> page_list = new List<FlashBuilderConsts.flash_page>();
            foreach (var page in this.page_list)
            {
                if (page.same == null)
                {
                    // Add sector to computeCrcs
                    sector_list.Add(Tuple.Create(page.addr, page.size));
                    page_list.Add(page);
                    // Compute CRC of data (Padded with 0xFF)
                    List<byte> data = page.data;
                    var pad_size = page.size - page.data.Count;
                    if (pad_size > 0)
                    {
                        data.AddRange(Enumerable.Repeat<byte>(0xFF, (int)pad_size));
                    }
                    page.crc = crc32(data) & 0xFFFFFFFF;
                }
            }
            // Analyze pages
            UInt32 page_erase_count = 0;
            double page_erase_weight = 0;
            if (page_list.Count > 0)
            {
                List<UInt32> crc_list = this.flash.computeCrcs(sector_list);
                foreach (var _tup_1 in page_list.Zip(crc_list, (pg, crc) => new Tuple<FlashBuilderConsts.flash_page, UInt32>(pg, crc)))
                {
                    var page = _tup_1.Item1;
                    var crc = _tup_1.Item2;
                    var page_same = page.crc == crc;
                    if (assume_estimate_correct)
                    {
                        page.same = page_same;
                    }
                    else if (page_same == false)
                    {
                        page.same = false;
                    }
                }
            }
            // Put together page and time estimate
            foreach (var page in this.page_list)
            {
                if (page.same == false)
                {
                    page_erase_count += 1;
                    page_erase_weight += page.getEraseProgramWeight();
                }
                else if (page.same == null)
                {
                    // Page is probably the same but must be read to confirm
                    page_erase_weight += page.getVerifyWeight();
                }
                else if (page.same == true)
                {
                    // Page is confirmed to be the same so no programming weight
                }
            }
            this.page_erase_count = page_erase_count;
            this.page_erase_weight = page_erase_weight;
            return Tuple.Create(page_erase_count, page_erase_weight);
        }

        // 
        //         Program by first performing a chip erase.
        //         
        public virtual byte _chip_erase_program(Action<double> progress_cb = null)
        {
            progress_cb = progress_cb ?? FlashBuilderConsts._stub_progress;
            Trace.TraceInformation("Smart chip erase");
            Trace.TraceInformation("{0} of {1} pages already erased", this.page_list.Count - this.chip_erase_count, this.page_list.Count);
            progress_cb(0.0);
            double progress = 0;
            this.flash.eraseAll();
            progress += (double)(this.flash.getFlashInfo().erase_weight);
            foreach (var page in this.page_list)
            {
                if (!(bool)page.erased)
                {
                    this.flash.programPage(page.addr, page.data);
                    progress += page.getProgramWeight();
                    progress_cb((float)(progress) / (float)(this.chip_erase_weight));
                }
            }
            progress_cb(1.0);
            return FlashBuilder.FLASH_CHIP_ERASE;
        }

        public virtual Tuple<FlashBuilderConsts.flash_page, UInt32> _next_unerased_page(UInt32 i)
        {
            if (i >= this.page_list.Count)
            {
                return new Tuple<FlashBuilderConsts.flash_page, UInt32>(null, i);
            }
            var page = this.page_list[(int)i];
            while ((bool)page.erased)
            {
                i += 1;
                if (i >= this.page_list.Count)
                {
                    return new Tuple<FlashBuilderConsts.flash_page, UInt32>(null, i);
                }
                page = this.page_list[(int)i];
            }
            return new Tuple<FlashBuilderConsts.flash_page, UInt32>(page, i + 1);
        }

        // 
        //         Program by first performing a chip erase.
        //         
        public virtual byte _chip_erase_program_double_buffer(Action<double> progress_cb = null)
        {
            progress_cb = progress_cb ?? FlashBuilderConsts._stub_progress;
            Trace.TraceInformation("Smart chip erase");
            Trace.TraceInformation("{0} of {1} pages already erased", this.page_list.Count - this.chip_erase_count, this.page_list.Count);
            progress_cb(0.0);
            double progress = 0;
            this.flash.eraseAll();
            progress += (double)this.flash.getFlashInfo().erase_weight;
            // Set up page and buffer info.
            var error_count = 0;
            UInt32 current_buf = 0;
            UInt32 next_buf = 1;
            var _tup_1 = this._next_unerased_page(0);
            var page = _tup_1.Item1;
            var i = _tup_1.Item2;
            Debug.Assert(page != null);
            // Load first page buffer
            this.flash.loadPageBuffer(current_buf, page.addr, page.data);
            while (page != null)
            {
                // Kick off this page program.
                var current_addr = page.addr;
                var current_weight = page.getProgramWeight();
                this.flash.startProgramPageWithBuffer(current_buf, current_addr);
                // Get next page and load it.
                var _tup_2 = this._next_unerased_page(i);
                page = _tup_2.Item1;
                i = _tup_2.Item2;
                if (page != null)
                {
                    this.flash.loadPageBuffer(next_buf, page.addr, page.data);
                }
                // Wait for the program to complete.
                var result = this.flash.waitForCompletion();
                // check the return code
                if (result != 0)
                {
                    Trace.TraceError("programPage(0x{0:X}) error: {1}", current_addr, result);
                    error_count += 1;
                    if (error_count > this.max_errors)
                    {
                        Trace.TraceError("Too many page programming errors, aborting program operation");
                        break;
                    }
                }
                // Swap buffers.
                var temp = current_buf;
                current_buf = next_buf;
                next_buf = temp;
                // Update progress.
                progress += current_weight;
                progress_cb((float)(progress) / (float)(this.chip_erase_weight));
            }
            progress_cb(1.0);
            return FlashBuilder.FLASH_CHIP_ERASE;
        }

        // 
        //         Program by performing sector erases.
        //         
        public virtual byte _page_erase_program(Action<double> progress_cb = null)
        {
            progress_cb = progress_cb ?? FlashBuilderConsts._stub_progress;
            UInt32 actual_page_erase_count = 0;
            double actual_page_erase_weight = 0;
            double progress = 0;
            progress_cb(0.0);
            foreach (var page in this.page_list)
            {
                // If the page is not the same
                if (page.same == false)
                {
                    progress += page.getEraseProgramWeight();
                }
                // Read page data if unknown - after this page.same will be True or False
                if (page.same == null)
                {
                    var data = this.flash.target.readBlockMemoryUnaligned8(page.addr, (UInt32)page.data.Count);
                    page.same = FlashBuilderConsts._same(page.data, data);
                    progress += page.getVerifyWeight();
                }
                // Program page if not the same
                if (page.same == false)
                {
                    this.flash.erasePage(page.addr);
                    this.flash.programPage(page.addr, page.data);
                    actual_page_erase_count += 1;
                    actual_page_erase_weight += page.getEraseProgramWeight();
                }
                // Update progress
                if (this.page_erase_weight > 0)
                {
                    progress_cb((float)(progress) / (float)(this.page_erase_weight));
                }
            }
            progress_cb(1.0);
            Trace.TraceInformation("Estimated page erase count: {0}", this.page_erase_count);
            Trace.TraceInformation("Actual page erase count: {0}", actual_page_erase_count);
            return FlashBuilder.FLASH_PAGE_ERASE;
        }

        // 
        //         Program by performing sector erases.
        //         
        public virtual double _scan_pages_for_same(Action<double> progress_cb = null)
        {
            progress_cb = progress_cb ?? FlashBuilderConsts._stub_progress;
            double progress = 0;
            var count = 0;
            var same_count = 0;
            foreach (FlashBuilderConsts.flash_page page in this.page_list)
            {
                // Read page data if unknown - after this page.same will be True or False
                if (page.same == null)
                {
                    var data = this.flash.target.readBlockMemoryUnaligned8(page.addr, (UInt32)page.data.Count);
                    page.same = FlashBuilderConsts._same(page.data, data);
                    progress += page.getVerifyWeight();
                    count += 1;
                    if ((bool)page.same)
                    {
                        same_count += 1;
                    }
                    // Update progress
                    progress_cb((float)(progress) / (float)(this.page_erase_weight));
                }
            }
            return progress;
        }

        public virtual Tuple<FlashBuilderConsts.flash_page, UInt32> _next_nonsame_page(UInt32 i)
        {
            if (i >= this.page_list.Count)
            {
                return new Tuple<FlashBuilderConsts.flash_page, UInt32>(null, i);
            }
            FlashBuilderConsts.flash_page page = this.page_list[(int)i];
            while ((bool)page.same)
            {
                i += 1;
                if (i >= this.page_list.Count)
                {
                    return new Tuple<FlashBuilderConsts.flash_page, UInt32>(null, i);
                }
                page = this.page_list[(int)i];
            }
            return Tuple.Create(page, i + 1);
        }

        // 
        //         Program by performing sector erases.
        //         
        public virtual byte _page_erase_program_double_buffer(Action<double> progress_cb = null)
        {
            progress_cb = progress_cb ?? FlashBuilderConsts._stub_progress;
            UInt32 actual_page_erase_count = 0;
            double actual_page_erase_weight = 0;
            double progress = 0;
            progress_cb(0.0);
            // Fill in same flag for all pages. This is done up front so we're not trying
            // to read from flash while simultaneously programming it.
            progress = this._scan_pages_for_same(progress_cb);
            // Set up page and buffer info.
            var error_count = 0;
            UInt32 current_buf = 0;
            UInt32 next_buf = 1;
            var _tup_1 = this._next_nonsame_page(0);
            var page = _tup_1.Item1;
            var i = _tup_1.Item2;
            // Make sure there are actually pages to program differently from current flash contents.
            if (page != null)
            {
                // Load first page buffer
                this.flash.loadPageBuffer(current_buf, page.addr, page.data);
                while (page != null)
                {
                    Debug.Assert(page.same != null);
                    // Kick off this page program.
                    var current_addr = page.addr;
                    var current_weight = page.getEraseProgramWeight();
                    this.flash.erasePage(current_addr);
                    this.flash.startProgramPageWithBuffer(current_buf, current_addr);
                    actual_page_erase_count += 1;
                    actual_page_erase_weight += page.getEraseProgramWeight();
                    // Get next page and load it.
                    var _tup_2 = this._next_nonsame_page(i);
                    page = _tup_2.Item1;
                    i = _tup_2.Item2;
                    if (page != null)
                    {
                        this.flash.loadPageBuffer(next_buf, page.addr, page.data);
                    }
                    // Wait for the program to complete.
                    var result = this.flash.waitForCompletion();
                    // check the return code
                    if (result != 0)
                    {
                        Trace.TraceError("programPage(0x{0:X}) error: {1}", current_addr, result);
                        error_count += 1;
                        if (error_count > this.max_errors)
                        {
                            Trace.TraceError("Too many page programming errors, aborting program operation");
                            break;
                        }
                    }
                    // Swap buffers.
                    var temp = current_buf;
                    current_buf = next_buf;
                    next_buf = temp;
                    // Update progress
                    progress += current_weight;
                    if (this.page_erase_weight > 0)
                    {
                        progress_cb((float)(progress) / (float)(this.page_erase_weight));
                    }
                }
            }
            progress_cb(1.0);
            Trace.TraceInformation("Estimated page erase count: {0}", this.page_erase_count);
            Trace.TraceInformation("Actual page erase count: {0}", actual_page_erase_count);
            return FlashBuilder.FLASH_PAGE_ERASE;
        }
    }

}
