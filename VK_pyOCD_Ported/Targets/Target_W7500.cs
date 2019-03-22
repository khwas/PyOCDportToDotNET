using openocd.CmsisDap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace openocd.Targets
{
    public static class Target_W7500
    {

        public static Dictionary<string, object> flash_algo = new Dictionary<string, object>() {
            { "load_address", (UInt32) 0x20000000 },
            { "instructions",
                new List<UInt32> {0xE00ABE00, 0x062D780D, 0x24084068, 0xD3000040, 0x1E644058, 0x1C49D1FA, 0x2A001E52, 0x4770D1F2,
                                  0x4c11b430, 0xbc3046a4, 0x20004760, 0x20004770, 0x23004770, 0x461ab510, 0x20144619, 0xfff0f7ff,
                                  0xbd102000, 0x2300b510, 0x461a4601, 0xf7ff2012, 0x2000ffe7, 0x460bbd10, 0x4601b510, 0xf7ff2022,
                                  0x2000ffdf, 0x0000bd10, 0x1fff1001, 0x00000000,
                }},
            { "pc_init",         (UInt32)0x2000002B },
            { "pc_eraseAll",     (UInt32)0x20000033 },
            { "pc_erase_sector", (UInt32)0x20000045 },
            { "pc_program_page", (UInt32)0x20000057 },
            { "static_base",     (UInt32)0x2000015E },
            { "begin_data",      (UInt32)0x20002000 }, // Analyzer uses a max of 256 B data (64 pages * 4 bytes / page)
            { "begin_stack",     (UInt32)0x20004000 },
            { "page_size",              256 },
            { "analyzer_supported",    true },
            { "analyzer_address",(UInt32)0x20001000 }, // Analyzer 0x20001000..0x20001600
        };

        public class Flash_w7500
            : Flash.Flash
        {

            public Flash_w7500(Core.Target target)
                : base(target, Target_W7500.flash_algo)
            {
            }
        }

        public class W7500 : Core.CoreSightTarget
        {
            public static Core.Memory.MemoryMap memoryMap = new Core.Memory.MemoryMap(
                new Core.Memory.FlashRegion(start: 0x00000000, length: 0x20000, blocksize: 0x100, isBootMemory: true),
                new Core.Memory.RamRegion(start: 0x20000000, length: 0x4000));
            public W7500(IDapAccessLink link) : base(link, memoryMap)
            {
            }
        }
    }

}
