using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace openocd.Flash
{
    public class FlashBuilderConsts
    {
        public const UInt32 PAGE_ESTIMATE_SIZE = 32;

        public const double PAGE_READ_WEIGHT = 0.3;

        public const UInt32 DATA_TRANSFER_B_PER_S = 40 * 1000;

        public class ProgrammingInfo
        {
            internal string program_type;
            internal TimeSpan? program_time;
            internal string analyze_type;
            internal TimeSpan? analyze_time;

            public ProgrammingInfo()
            {
                this.program_type = null;
                this.program_time = null;
                this.analyze_type = null;
                this.analyze_time = null;
            }
        }

        public static bool _same(List<byte> d1, List<byte> d2)
        {
            Debug.Assert(d1.Count == d2.Count);
            for (int i = 0; i < d1.Count; i++)
            {
                if (d1[i] != d2[i])
                {
                    return false;
                }
            }
            return true;
        }

        public static bool _erased(IEnumerable<byte> d)
        {
            return d.All(b => b == 0xFF);
        }

        public static Action<double> _stub_progress = new Action<double>((double percent) => { });

        public class flash_page
        {
            internal UInt32 addr;
            internal UInt32 size;
            internal List<byte> data;
            internal double erase_weight;
            internal double program_weight;
            internal bool? erased;
            internal bool? same;
            internal UInt32 crc;

            public flash_page(
                UInt32 addr,
                UInt32 size,
                List<byte> data,
                double erase_weight,
                double program_weight)
            {
                this.addr = addr;
                this.size = size;
                this.data = data;
                this.erase_weight = erase_weight;
                this.program_weight = program_weight;
                this.erased = null;
                this.same = null;
            }

            // 
            //         Get time to program a page including the data transfer
            //         
            public virtual double getProgramWeight()
            {
                return this.program_weight + (float)(this.data.Count) / (float)(DATA_TRANSFER_B_PER_S);
            }

            // 
            //         Get time to erase and program a page including data transfer time
            //         
            public virtual double getEraseProgramWeight()
            {
                return this.erase_weight + this.program_weight + (float)(this.data.Count) / (float)(DATA_TRANSFER_B_PER_S);
            }

            // 
            //         Get time to verify a page
            //         
            public virtual double getVerifyWeight()
            {
                return (float)(this.size) / (float)(DATA_TRANSFER_B_PER_S);
            }
        }

        public class flash_operation
        {
            internal UInt32 addr;
            internal List<byte> data;

            public flash_operation(UInt32 addr, List<byte> data)
            {
                this.addr = addr;
                this.data = data;
            }
        }
    }
}
