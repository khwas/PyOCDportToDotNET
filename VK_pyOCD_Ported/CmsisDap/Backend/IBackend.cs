using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace openocd.CmsisDap
{
    public interface IBackend
    {
        bool isAvailable { get; set; }
        byte packet_count { get; set; }
        void open();
        void init();
        void write(List<byte> data);
        List<byte> read(int size = -1, int timeout = -1);
        string getInfo();
        void close();
        void setPacketSize(UInt16 size);
        string getSerialNumber();
    }
}
