using openocd.CmsisDap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace openocd.Targets
{
    public static class Target_STM32F7x_1024
    {
        public static Dictionary<string, object> flash_algo()
        {
            Dictionary<string, object> result = new Dictionary<string, object>()
            {
            { "load_address", (UInt32) 0x20000000 },
            { "instructions",
                new List<UInt32> {
0x4770BE00,
0x8F4FF3BF,
0xF3C04770,
0x28403007,
0x2104D303,
0x1090EB01,
0x08C04770,
0x48584770,
0x60014956,
0x60014957,
0x1F002100,
0x48546001,
0x68013008,
0x01F0F041,
0x48516001,
0x68003010,
0xD4080680,
0xF2454850,
0x60015155,
0x60412106,
0x71FFF640,
0x20006081,
0x48494770,
0x6801300C,
0x4100F041,
0x20006001,
0xB5104770,
0x340C4C44,
0xF0406820,
0x60200004,
0xF4406820,
0x60203080,
0x1F224941,
0x20AAF64A,
0x6008E000,
0x03DB6813,
0x6820D4FB,
0x0004F020,
0x20006020,
0xB510BD10,
0xFFB3F7FF,
0x31084936,
0xF042680A,
0x600A02F0,
0x1D0C2202,
0x68226022,
0xEA4206C0,
0x60226210,
0xF4406820,
0x60203080,
0xF64A4A2F,
0xE00020AA,
0x680B6010,
0xD4FB03DB,
0xF0206820,
0x60200002,
0xF0106808,
0xD00400F0,
0xF0406808,
0x600800F0,
0xBD102001,
0x45F0E92D,
0x1CC94C21,
0x0103F021,
0x68233408,
0x03F0F043,
0x23006023,
0x602B1D25,
0x0C04F105,
0xA070F8DF,
0x2801F240,
0x26AAF64A,
0x682BE027,
0x0308EA43,
0xF8DC602B,
0xF4433000,
0xF8CC037F,
0xF04F3000,
0x68176300,
0xF3BF501F,
0x46578F4F,
0x603EE000,
0x03DB6823,
0x682BD4FB,
0x0301F023,
0x6823602B,
0x0FF0F013,
0x6820D006,
0x00F0F040,
0x20016020,
0x85F0E8BD,
0x1F091D00,
0x29001D12,
0x2000D1D5,
0x0000E7F6,
0x45670123,
0x40023C04,
0xCDEF89AB,
0x40003000,
0x00000000,
0x54530101,
0x4632334D,
0x31207837,
0x4620424D,
0x6873616C,
0x00000000,
0x00000000,
0x00000000,
0x00000000,
0x00000000,
0x00000000,
0x00000000,
0x00000000,
0x00000000,
0x00000000,
0x00000000,
0x00000000,
0x00000000,
0x00000000,
0x00000000,
0x00000000,
0x00000000,
0x00000000,
0x00000000,
0x00000000,
0x00000000,
0x00000000,
0x00000000,
0x00000000,
0x00000000,
0x00000000,
0x00000000,
0x00010000,
0x08000000,
0x00100000,
0x00000200,
0x00000000,
0x000000FF,
0x000003E8,
0x00001770,
0x00008000,
0x00000000,
0x00020000,
0x00020000,
0x00040000,
0x00040000,
0xFFFFFFFF,
0xFFFFFFFF,
                }},
            { "pc_init",         (UInt32)0x2000001F },
            { "pc_uninit",       (UInt32)0x2000005b },
            { "pc_eraseAll",     (UInt32)0x2000006b },
            { "pc_erase_sector", (UInt32)0x2000009f },
            { "pc_program_page", (UInt32)0x200000f5 },
            { "static_base",     (UInt32)0x20000190 },
            { "begin_data",      (UInt32)0x20002000 }, // Analyzer uses a max of 256 B data (64 pages * 4 bytes / page)
            { "begin_stack",     (UInt32)0x20003000 },
            { "page_size",              512 },
            { "analyzer_supported",    false },         ////
            { "analyzer_address",(UInt32)0x20011000 }, // Analyzer 0x20002000..0x20002600
            };
            return result;
        }

        public class Flash_STM32F7x_1024
            : Flash.Flash
        {

            public Flash_STM32F7x_1024(Core.Target target)
                : base(target, Target_STM32F7x_1024.flash_algo())
            {
            }
        }

        public class STM32F7x_1024 : Core.CoreSightTarget
        {
            public static Core.Memory.MemoryMap memoryMap = new Core.Memory.MemoryMap(
                new Core.Memory.FlashRegion(start: 0x08000000, length: 0x00100000, blocksize: 0x200, isBootMemory: true),
                new Core.Memory.RamRegion(start: 0x20000000, length: 0x00040000));
            public STM32F7x_1024(IDapAccessLink link) : base(link, memoryMap)
            {
            }
        }

    }
}

