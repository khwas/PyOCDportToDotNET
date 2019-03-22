using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace openocd.Flash
{
    public static class FlashConsts
    {
        public const double DEFAULT_PAGE_PROGRAM_WEIGHT = 0.13;
        public const double DEFAULT_PAGE_ERASE_WEIGHT = 0.048;
        public const double DEFAULT_CHIP_ERASE_WEIGHT = 0.174;

        // Program to compute the CRC of sectors.  This works on cortex-m processors.
        // Code is relocatable and only needs to be on a 4 byte boundary.
        // 200 bytes of executable data below + 1024 byte crc table = 1224 bytes
        // Usage requirements:
        // -In memory reserve 0x600 for code & table
        // -Make sure data buffer is big enough to hold 4 bytes for each page that could be checked (ie.  >= num pages * 4)
        public static UInt32[] analyzer = new UInt32[]
            {
                0x2180468c, 0x2600b5f0, 0x4f2c2501, 0x447f4c2c, 0x1c2b0049, 0x425b4033, 0x40230872, 0x085a4053,
                0x425b402b, 0x40534023, 0x402b085a, 0x4023425b, 0x085a4053, 0x425b402b, 0x40534023, 0x402b085a,
                0x4023425b, 0x085a4053, 0x425b402b, 0x40534023, 0x402b085a, 0x4023425b, 0x085a4053, 0x425b402b,
                0x40534023, 0xc7083601, 0xd1d2428e, 0x2b004663, 0x4663d01f, 0x46b4009e, 0x24ff2701, 0x44844d11,
                0x1c3a447d, 0x88418803, 0x4351409a, 0xd0122a00, 0x22011856, 0x780b4252, 0x40533101, 0x009b4023,
                0x0a12595b, 0x42b1405a, 0x43d2d1f5, 0x4560c004, 0x2000d1e7, 0x2200bdf0, 0x46c0e7f8, 0x000000b6,
                0xedb88320, 0x00000044,
            };

        public static byte _msb(UInt32 n)
        {
            byte ndx = 0;
            while (1 < n)
            {
                n = n >> 1;
                ndx += 1;
            }
            return ndx;
        }

        public static object _same(List<object> d1, List<object> d2)
        {
            if (d1.Count != d2.Count)
            {
                return false;
            }
            for (int i = 0; i < d1.Count; i++)
            {
                if (d1[i] != d2[i])
                {
                    return false;
                }
            }
            return true;
        }

        public class PageInfo
        {
            public bool? crc_supported;
            internal double? erase_weight;
            internal double? program_weight;
            internal UInt32? size;
            internal UInt32? base_addr;

            public PageInfo()
            {
                this.base_addr = null;
                this.erase_weight = null;
                this.program_weight = null;
                this.size = null;
                this.crc_supported = null;
            }
        }

        public class FlashInfo
        {
            public UInt32? rom_start;
            public double? erase_weight;
            public bool? crc_supported;
            public FlashInfo()
            {
                this.rom_start = null;
                this.erase_weight = null;
            }
        }

    }
}
