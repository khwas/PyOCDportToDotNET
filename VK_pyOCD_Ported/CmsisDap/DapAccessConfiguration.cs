using HidLibrary;
using openocd.CmsisDap.Backend;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace openocd.CmsisDap
{
    public class DapAccessConfiguration: IDapAccessConfiguration
    {
        public bool isAvailable { get; private set; }

        public List<IDapAccessLink> get_connected_devices()
        {
            throw new NotImplementedException();
        }

        public object set_args(object arg_list)
        {
            throw new NotImplementedException(); ;
        }

        public IDapAccessLink get_device(object device_id)
        {
            throw new NotImplementedException(); ;
        }


        // 
        //         returns all the connected devices which matches HidApiUSB.vid/HidApiUSB.pid.
        //         returns an array of HidApiUSB (Interface) objects
        //         
        // [staticmethod]
        public static List<IBackend> getAllConnectedInterface()
        {
            List<IBackend> boards = new List<IBackend>();
            IEnumerable<HidDevice> devices = HidDevices.Enumerate();
            if (!devices.Any())
            {
                Trace.TraceInformation("No Mbed device connected");
                return boards;
            }
            foreach (HidDevice deviceInfo in devices)
            {
                deviceInfo.ReadProduct(out byte[] data);
                string product_name = UnicodeEncoding.Unicode.GetString(data);
                if (!product_name.Contains("CMSIS-DAP"))
                {
                    // Skip non cmsis-dap devices
                    continue;
                }
                HidDevice dev = deviceInfo;
                try
                {
                    //dev = hid.device(vendor_id: deviceInfo["vendor_id"], product_id: deviceInfo["product_id"], path: deviceInfo["path"]);
                }
                catch //(IOError)
                {
                    Trace.TraceInformation("Failed to open Mbed device");
                    continue;
                }
                BackendHidUsb new_board = new BackendHidUsb(dev);
                boards.Add(new_board);
            }
            return boards;
        }
    }
}
