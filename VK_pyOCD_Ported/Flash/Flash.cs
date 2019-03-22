using openocd.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace openocd.Flash
{
    // 
    //     This class is responsible to flash a new binary in a target
    //     
    public class Flash
    {
        public readonly ITarget target;
        private Dictionary<string, object> flash_algo;
        private bool flash_algo_debug;
        private UInt32? end_flash_algo;
        private UInt32? begin_stack;
        private UInt32? begin_data;
        private UInt32? static_base;
        private UInt32? min_program_length;
        private List<UInt32> page_buffers;
        private bool double_buffer_supported;
        private UInt32 _saved_vector_catch;

        public Flash(ITarget target, Dictionary<string, object> flash_algo)
        {
            this.target = target;
            this.flash_algo = flash_algo;
            this.flash_algo_debug = false;
            if (flash_algo != null)
            {
                this.end_flash_algo = (UInt32)((UInt32)flash_algo["load_address"] + flash_algo.Count * 4);
                this.begin_stack = (UInt32)flash_algo["begin_stack"];
                this.begin_data = (UInt32)flash_algo["begin_data"];
                this.static_base = (UInt32)flash_algo["static_base"];
                this.min_program_length = flash_algo.ContainsKey("min_program_length") ? (UInt32)flash_algo["min_program_length"] : 0;
                // Check for double buffering support.
                if (flash_algo.ContainsKey("page_buffers"))
                {
                    this.page_buffers = (List<UInt32>)flash_algo["page_buffers"];
                }
                else
                {
                    this.page_buffers = new List<UInt32> {
                            (UInt32)this.begin_data
                        };
                }
                this.double_buffer_supported = this.page_buffers.Count > 1;
            }
            else
            {
                this.end_flash_algo = null;
                this.begin_stack = null;
                this.begin_data = null;
                this.static_base = null;
            }
        }

        public object minimumProgramLength
        {
            get
            {
                return this.min_program_length;
            }
        }

        // 
        //         Download the flash algorithm in RAM
        //         
        public virtual void init()
        {
            this.target.halt();
            this.target.setTargetState(ETargetState.TARGET_PROGRAM);
            // update core register to execute the init subroutine
            UInt32 result = this.callFunctionAndWait((UInt32)this.flash_algo["pc_init"], init: true);
            // check the return code
            if (result != 0)
            {
                Trace.TraceError("init error: {0}", result);
            }
        }

        public virtual List<UInt32> computeCrcs(IEnumerable<Tuple<UInt32, UInt32>> sectors)
        {
            List<UInt32> data = new List<UInt32>();
            // Convert address, size pairs into commands
            // for the crc computation algorithm to preform
            foreach (var _tup_1 in sectors)
            {
                var addr = _tup_1.Item1;
                var size = _tup_1.Item2;
                byte size_val = FlashConsts._msb(size);
                UInt32 addr_val = addr / size;
                // Size must be a power of 2
                Debug.Assert(1 << size_val == size);
                // Address must be a multiple of size
                Debug.Assert(addr % size == 0);
                UInt32 val = (UInt32)((UInt32)(size_val << 0) | (UInt32)(addr_val << 16));
                data.Add(val);
            }
            this.target.writeBlockMemoryAligned32((UInt32)this.begin_data, data);
            // update core register to execute the subroutine
            var result = this.callFunctionAndWait((UInt32)this.flash_algo["analyzer_address"], this.begin_data, (UInt32)data.Count);
            // Read back the CRCs for each section
            data = this.target.readBlockMemoryAligned32((UInt32)this.begin_data, (UInt32)data.Count);
            return data;
        }

        // 
        //         Erase all the flash
        //         
        public virtual void eraseAll()
        {
            // update core register to execute the eraseAll subroutine
            UInt32 result = this.callFunctionAndWait((UInt32)this.flash_algo["pc_eraseAll"]);
            // check the return code
            if (result != 0)
            {
                Trace.TraceError("eraseAll error: {0}", result);
            }
        }

        // 
        //         Erase one page
        //         
        public virtual void erasePage(UInt32 flashPtr)
        {
            // update core register to execute the erasePage subroutine
            UInt32 result = this.callFunctionAndWait((UInt32)this.flash_algo["pc_erase_sector"], flashPtr);
            // check the return code
            if (result != 0)
            {
                Trace.TraceError("erasePage(0x{0:X}) error: {1}", flashPtr, result);
            }
        }

        // 
        //         Flash one page
        //         
        public virtual void programPage(UInt32 flashPtr, List<byte> bytes)
        {
            // prevent security settings from locking the device
            bytes = this.overrideSecurityBits(flashPtr, bytes);
            // first transfer in RAM
            this.target.writeBlockMemoryUnaligned8((UInt32)this.begin_data, bytes);
            // get info about this page
            var page_info = this.getPageInfo(flashPtr);
            // update core register to execute the program_page subroutine
            UInt32 result = this.callFunctionAndWait((UInt32)this.flash_algo["pc_program_page"], flashPtr, (UInt32)bytes.Count, this.begin_data);
            // check the return code
            if (result != 0)
            {
                Trace.TraceError("programPage(0x{0:X}) error: {1:X}", flashPtr, result);
            }
        }

        public virtual UInt32 getPageBufferCount()
        {
            return (UInt32)this.page_buffers.Count;
        }

        public virtual bool isDoubleBufferingSupported()
        {
            return this.double_buffer_supported;
        }

        // 
        //         Flash one page
        //         
        public virtual void startProgramPageWithBuffer(UInt32 bufferNumber, UInt32 flashPtr)
        {
            Debug.Assert(bufferNumber < this.page_buffers.Count, "Invalid buffer number");
            // get info about this page
            var page_info = this.getPageInfo(flashPtr);
            // update core register to execute the program_page subroutine
            //var result = 
            this.callFunction((UInt32)this.flash_algo["pc_program_page"], flashPtr, page_info.size, this.page_buffers[(int)bufferNumber]);
        }

        public virtual void loadPageBuffer(UInt32 bufferNumber, UInt32 flashPtr, List<byte> bytes)
        {
            Debug.Assert(bufferNumber < this.page_buffers.Count, "Invalid buffer number");
            // prevent security settings from locking the device
            bytes = this.overrideSecurityBits(flashPtr, bytes);
            // transfer the buffer to device RAM
            this.target.writeBlockMemoryUnaligned8(this.page_buffers[(int)bufferNumber], bytes);
        }

        // 
        //         Flash a portion of a page.
        //         
        public virtual void programPhrase(UInt32 flashPtr, List<byte> bytes)
        {
            UInt32 min_len;
            // Get min programming length. If one was not specified, use the page size.
            if (this.min_program_length != null)
            {
                min_len = (UInt32)this.min_program_length;
            }
            else
            {
                min_len = (UInt32)(this.getPageInfo(flashPtr).size);
            }
            // Require write address and length to be aligned to min write size.
            if (flashPtr % min_len != 0)
            {
                throw new Exception("unaligned flash write address");
            }
            if (bytes.Count % min_len != 0)
            {
                throw new Exception("phrase length is unaligned or too small");
            }
            // prevent security settings from locking the device
            bytes = this.overrideSecurityBits(flashPtr, bytes);
            // first transfer in RAM
            this.target.writeBlockMemoryUnaligned8((UInt32)this.begin_data, bytes);
            // update core register to execute the program_page subroutine
            UInt32 result = this.callFunctionAndWait((UInt32)this.flash_algo["pc_program_page"], flashPtr, (UInt32)bytes.Count, this.begin_data);
            // check the return code
            if (result != 0)
            {
                Trace.TraceError("programPhrase(0x{0:X}) error: {1}", flashPtr, result);
            }
        }

        // 
        //         Get info about the page that contains this address
        // 
        //         Override this function if variable page sizes are supported
        //         
        public virtual FlashConsts.PageInfo getPageInfo(UInt32 addr)
        {
            Core.Memory.MemoryRegion region = this.target.getMemoryMap().getRegionForAddress(addr);
            if (region == null)
            {
                return null;
            }
            FlashConsts.PageInfo info = new FlashConsts.PageInfo
            {
                erase_weight = FlashConsts.DEFAULT_PAGE_ERASE_WEIGHT,
                program_weight = FlashConsts.DEFAULT_PAGE_PROGRAM_WEIGHT,
                size = region.blocksize
            };
            info.base_addr = addr - addr % info.size;
            return info;
        }

        // 
        //         Get info about the flash
        // 
        //         Override this function to return differnt values
        //         
        public virtual FlashConsts.FlashInfo getFlashInfo()
        {
            Core.Memory.MemoryRegion boot_region = this.target.getMemoryMap().getBootMemory();
            FlashConsts.FlashInfo info = new FlashConsts.FlashInfo
            {
                rom_start = boot_region != null ? boot_region.start : 0,
                erase_weight = FlashConsts.DEFAULT_CHIP_ERASE_WEIGHT,
                crc_supported = (bool)this.flash_algo["analyzer_supported"]
            };
            return info;
        }

        public virtual FlashBuilder getFlashBuilder()
        {
            return new FlashBuilder(this, (UInt32)this.getFlashInfo().rom_start);
        }

        // 
        //         Flash a block of data
        //         
        public virtual object flashBlock(
            UInt32 addr,
            List<byte> data,
            bool smart_flash = true,
            bool? chip_erase = null,
            Action<double> progress_cb = null,
            bool fast_verify = false)
        {
            UInt32 flash_start = (UInt32)this.getFlashInfo().rom_start;
            FlashBuilder fb = new FlashBuilder(this, flash_start);
            fb.addData(addr, data);
            var info = fb.program(chip_erase, progress_cb, smart_flash, fast_verify);
            return info;
        }

        // 
        //         Flash a binary
        //         
        public virtual void flashBinary(
            byte[] file_bytes,
            UInt32? flashPtr = null,
            bool smart_flash = true,
            bool? chip_erase = null,
            Action<double> progress_cb = null,
            bool fast_verify = false)
        {
            if (flashPtr == null)
            {
                flashPtr = this.getFlashInfo().rom_start;
            }
            // var f = open(path_file, "rb");
            // using (var f = open(path_file, "rb"))
            // {
            //     data = f.read();
            // }
            // var data = unpack(str(data.Count) + "B", data);
            byte[] data = file_bytes; // File.ReadAllBytes(path_file);
            this.flashBlock((UInt32)flashPtr, data.ToList(), smart_flash, chip_erase, progress_cb, fast_verify);
            if (this.flash_algo.ContainsKey("pc_uninit"))
            {
                callFunctionAndWait((UInt32)this.flash_algo["pc_uninit"]);
            }
        }

        public virtual void callFunction(
            UInt32 pc,
            UInt32? r0 = null,
            UInt32? r1 = null,
            UInt32? r2 = null,
            UInt32? r3 = null,
            bool init = false)
        {
            List<string> reg_list = new List<string>();
            List<UInt32> data_list = new List<UInt32>();
            if (this.flash_algo_debug)
            {
                // Save vector catch state for use in waitForCompletion()
                //this._saved_vector_catch = this.target.getVectorCatch();
                //this.target.setVectorCatch(Core.Target.CATCH_ALL);
            }
            if (init)
            {
                List<UInt32> algo = (List<UInt32>)this.flash_algo["instructions"];
                // download flash algo in RAM
                this.target.writeBlockMemoryAligned32(
                    (UInt32)this.flash_algo["load_address"],
                    algo);

                {
                    List<UInt32> vrfy = this.target.readBlockMemoryAligned32(
                        (UInt32)this.flash_algo["load_address"],
                        (UInt32)algo.Count());
                    Debug.Assert(algo.SequenceEqual(vrfy));
                }

                if ((bool)this.flash_algo["analyzer_supported"])
                {
                    this.target.writeBlockMemoryAligned32(
                        (UInt32)this.flash_algo["analyzer_address"], 
                        FlashConsts.analyzer.ToList());
                }
            }
            reg_list.Add("pc");
            data_list.Add(pc | 1);
            reg_list.Add("xpsr");
            data_list.Add(0x1000000);
            if (r0 != null)
            {
                reg_list.Add("r0");
                data_list.Add((UInt32)r0);
            }
            if (r1 != null)
            {
                reg_list.Add("r1");
                data_list.Add((UInt32)r1);
            }
            if (r2 != null)
            {
                reg_list.Add("r2");
                data_list.Add((UInt32)r2);
            }
            if (r3 != null)
            {
                reg_list.Add("r3");
                data_list.Add((UInt32)r3);
            }
            //if (init)
            {
                reg_list.Add("r9");
                data_list.Add((UInt32)this.static_base);
            }
            //if (init)
            {
                reg_list.Add("sp");
                data_list.Add((UInt32)this.begin_stack);
            }
            // VK: Prepare return address to be THUMB breakpoint instruction
            reg_list.Add("lr");
            data_list.Add((UInt32)this.flash_algo["load_address"] | 1);
            // convert to index only
            //List<sbyte> reg_list_ = reg_list.Select(reg => target.register_name_to_index(reg)).ToList();
            this.target.writeCoreRegistersRaw(reg_list, data_list);
            // resume target
            this.target.resume();
            /// 
            // while (true)
            // {
            //     UInt32 control = this.target.readCoreRegister("control");
            //     UInt32 mpuc = this.target.read32(0xE000ED94)();
            //     this.target.step();
            //     data_list = this.target.readCoreRegistersRaw(reg_list);
            //     UInt32 xpsr = this.target.readCoreRegister("xpsr");
            //     UInt32 hfsr = this.target.read32(0xE000ED2C)();
            //     UInt32 ufsr = this.target.read32(0xE000ED2A)();
            // }
        }

        // Wait until the breakpoint is hit.
        public virtual UInt32 waitForCompletion()
        {
            while (this.target.getState() == ETargetState.TARGET_RUNNING) { };
            // if (this.target.getState() == ETargetState.TARGET_RUNNING) Thread.Sleep(100000);
            // if (this.target.getState() == ETargetState.TARGET_RUNNING)
            // {
            //   this.target.halt();
            //   UInt32 demcr = this.target.read32(0xE000EDFC)();
            //   UInt32 pc = this.target.readCoreRegister("pc");
            //   UInt32 xpsr = this.target.readCoreRegister("xpsr");
            //   UInt32 hfsr = this.target.read32(0xE000ED2C)();
            //   UInt32 ufsr = this.target.read32(0xE000ED2A)();
            // }
            if (this.flash_algo_debug)
            {
                bool analyzer_supported = (bool)this.flash_algo["analyzer_supported"];
                UInt32 expected_fp = (UInt32)this.flash_algo["static_base"];
                UInt32 expected_sp = (UInt32)this.flash_algo["begin_stack"];
                UInt32 expected_pc = (UInt32)this.flash_algo["load_address"];
                var expected_flash_algo = this.flash_algo["instructions"];
                if (analyzer_supported)
                {
                    var expected_analyzer = FlashConsts.analyzer;
                }
                UInt32 final_fp = this.target.readCoreRegister("r9");
                UInt32 final_sp = this.target.readCoreRegister("sp");
                UInt32 final_pc = this.target.readCoreRegister("pc");
                //TODO - uncomment if Read/write and zero init sections can be moved into a separate flash algo section
                //final_flash_algo = self.target.readBlockMemoryAligned32(self.flash_algo['load_address'], len(self.flash_algo['instructions']))
                //if analyzer_supported:
                //    final_analyzer = self.target.readBlockMemoryAligned32(self.flash_algo['analyzer_address'], len(analyzer))
                bool error = false;
                if (final_fp != expected_fp)
                {
                    // Frame pointer should not change
                    Trace.TraceError(String.Format("Frame pointer should be 0x{0:X} but is 0x{1:X}", expected_fp, final_fp));
                    error = true;
                }
                if (final_sp != expected_sp)
                {
                    // Stack pointer should return to original value after function call
                    Trace.TraceError(String.Format("Stack pointer should be 0x{0:x} but is 0x{1:X}", expected_sp, final_sp));
                    error = true;
                }
                if (final_pc != expected_pc)
                {
                    // PC should be pointing to breakpoint address
                    Trace.TraceError(String.Format("PC should be 0x{0:X} but is 0x{1:X}", expected_pc, final_pc));
                    error = true;
                }
                //TODO - uncomment if Read/write and zero init sections can be moved into a separate flash algo section
                //if not _same(expected_flash_algo, final_flash_algo):
                //    Trace.TraceError("Flash algorithm overwritten!")
                //    error = True
                //if analyzer_supported and not _same(expected_analyzer, final_analyzer):
                //    Trace.TraceError("Analyzer overwritten!")
                //    error = True
                if (error)
                {
                    //IPSR
                    UInt32 xpsr = this.target.readCoreRegister("xpsr");
                    UInt32 hfsr = this.target.read32(0xE000ED2C)();
                    UInt32 ufsr = this.target.read32(0xE000ED2A)();
                }
               Debug.Assert(error == false);
                //this.target.setVectorCatch(this._saved_vector_catch);
            }
            return this.target.readCoreRegister("r0");
        }

        public virtual UInt32 callFunctionAndWait(
            UInt32 pc,
            UInt32? r0 = null,
            UInt32? r1 = null,
            UInt32? r2 = null,
            UInt32? r3 = null,
            bool init = false)
        {
            this.callFunction(pc, r0, r1, r2, r3, init);
            return this.waitForCompletion();
        }

        // 
        //         Turn on extra flash algorithm checking
        // 
        //         When set this will greatly slow down flash algo performance
        //         
        public virtual void setFlashAlgoDebug(bool enable)
        {
            this.flash_algo_debug = enable;
        }

        public virtual List<byte> overrideSecurityBits(UInt32 flashPtr, List<byte> data)
        {
            return data;
        }
    }
}
