using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MadWizard;
using System.Diagnostics;
using MadWizard.WinUSBNet;

namespace openocd.CmsisDap.Backend
{

    // This class provides basic functions to access a WIN USB device using:
    //     - write/read an endpoint
    // 
    public class BackendWinUsb : IBackend
    {
        public readonly string vendor_name;
        public readonly string product_name;
        public readonly MadWizard.WinUSBNet.USBDeviceInfo device_info;
        public readonly UInt16 vid;
        public readonly UInt16 pid;
        public byte packet_count { get; set; }
        internal UInt16 packet_size;
        internal string serial_number { get; }
        internal USBPipe deviceOut;
        internal USBPipe deviceIn;

        public BackendWinUsb(MadWizard.WinUSBNet.USBDeviceInfo deviceInfo)
        {
            // Vendor page and usage_id = 2
            this.packet_size = 0x200;
            this.vendor_name = deviceInfo.Manufacturer;
            this.product_name = deviceInfo.DeviceDescription.First().ToString();
            this.serial_number = ""; // deviceInfo.;
            this.vid = (UInt16)deviceInfo.VID;
            this.pid = (UInt16)deviceInfo.PID;
            this.device_info = deviceInfo;
            this.deviceOut = null;
            this.deviceIn = null;
        }

        public bool isAvailable { get; set; }

        public string getInfo()
        {
            throw new NotImplementedException();
        }

        public void init()
        {
        }

        public void open()
        {
            try
            {
                // open_path(this.device_info["path"]);
                // this.device.OpenDevice(DeviceMode.NonOverlapped, DeviceMode.NonOverlapped, ShareMode.Exclusive);
                var usbInterface = new MadWizard.WinUSBNet.USBDevice(this.device_info).Interfaces.First(
                        usbIf =>
                               usbIf.BaseClass == USBBaseClass.VendorSpecific &&
                               usbIf.Protocol == 0
                    );
                this.deviceOut = usbInterface.Pipes.First(p => p.IsOut);
                this.deviceIn = usbInterface.Pipes.First(p => p.IsIn);
            }
            catch // (IOError)
            {
                throw new Exception("Unable to open device"); //DAPAccessIntf.DeviceError
            }
        }


        // 
        //         write data on the OUT endpoint associated to the HID interface
        //         
        public void write(List<byte> data)
        {
            foreach (var _ in Enumerable.Range(0, (int)this.packet_size - data.Count))
            {
                data.Add(0);
            }
            Debug.Assert(data.Count == 0x200);
            deviceOut.Write(data.ToArray());
        }

        // 
        //         read data on the IN endpoint associated to the HID interface
        //         
        public List<byte> read(int size = -1, int timeout = -1)
        {
            byte[] packet = new byte[this.packet_size];
            deviceIn.Read(packet);
            return packet.ToList();
        }

        public virtual string getSerialNumber()
        {
            return this.serial_number;
        }

        // 
        //         close the interface
        //         
        public void close()
        {
            Trace.TraceInformation("closing Win USB interface");
            this.deviceOut = null;
            this.deviceIn = null;
        }

        public void setPacketSize(UInt16 size)
        {
            this.packet_size = size;
        }
    }
}

