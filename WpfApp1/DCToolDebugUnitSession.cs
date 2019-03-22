using MadWizard.WinUSBNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfApp1.UILog;

namespace WpfApp1
{
    public class DCToolDebugUnitSession : DCTool
    {
        #region Boilerplate
        public static TraceSource DebugUnitSessionTraceSource = new TraceSource("DebugUnitSession", SourceLevels.All);
        private readonly DCToolDebugUnit nextTool;
        private USBDeviceInfo deviceInfo = null;

        public DCToolDebugUnitSession(DCToolDebugUnit nextTool) : base(DebugUnitSessionTraceSource)
        {
            this.nextTool = nextTool;
        }

        public void OnUSBDeviceInfoChanged(USBDeviceInfo deviceInfo)
        {
            if (DeviceInfo != deviceInfo)
            {
                LogInfo("USB Device Info changed");
                DeviceInfo = deviceInfo;
                if (IsCloseEnabled) CloseAsync();
            }
        }

        public override string About
        {
            get => @"Tool Debug Unit Session writes and reads packets over USB connection. 
Additionally it listens to optional Out Pipe for on-chip tracing data, debug console used though serial output during semihosting";
        }

        internal USBDeviceInfo DeviceInfo
        {
            get => deviceInfo;
            set
            {
                deviceInfo = value;
                InvalidateProperty(nameof(IsOpenEnabled));
            }
        }

        internal override void OnPendingActionCounterChanged()
        {
            base.OnPendingActionCounterChanged();
            InvalidateProperty(nameof(IsOpenEnabled));
            InvalidateProperty(nameof(IsCloseEnabled));
        }

        internal override void OnToolChanged()
        {
            base.OnToolChanged();
            InvalidateProperty(nameof(IsOpenEnabled));
            InvalidateProperty(nameof(IsCloseEnabled));
            nextTool.OnDebugUnitSessionAvailable((IBackend)Tool);
        }

        public bool IsOpenEnabled { get => PendingActionsCounter == 0 && Tool == null && deviceInfo != null; }
        public bool IsCloseEnabled { get => PendingActionsCounter == 0 && Tool != null; }

        public void OpenAsync() => PostAction(() => Open());
        public void CloseAsync() => PostAction(() => Close());
        #endregion

        internal void Open()
        {
            Tool = new BackendWinUsb(DebugUnitSessionTraceSource, DeviceInfo);
            ((BackendWinUsb)Tool).init();
            ((BackendWinUsb)Tool).open();
        }
        internal void Close()
        {
            ((BackendWinUsb)Tool).close();
            Tool = null;
        }
    }

    public interface IBackend : ITool
    {
        void write(List<byte> data);
        List<byte> read(int size = -1, int timeout = -1);
    }

    public class BackendWinUsb: Loggable, IBackend
    {
        public readonly string vendor_name;
        public readonly string product_name;
        public readonly USBDeviceInfo device_info;
        public readonly UInt16 vid;
        public readonly UInt16 pid;
        public byte packet_count { get; set; }
        internal UInt16 packet_size;
        internal string serial_number { get; }
        internal USBDevice usbDevice;
        internal USBPipe deviceOut;
        internal USBPipe deviceIn;

        public BackendWinUsb(TraceSource namedTraceSource, USBDeviceInfo deviceInfo): base (namedTraceSource)
        {
            // Vendor page and usage_id = 2
            this.packet_size = 0x200;
            this.vendor_name = deviceInfo.Manufacturer;
            this.product_name = deviceInfo.DeviceDescription;
            this.serial_number = "";// deviceInfo.;
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
                usbDevice = new MadWizard.WinUSBNet.USBDevice(this.device_info);
                var usbInterface = usbDevice.Interfaces.First(
                        usbIf =>
                               usbIf.BaseClass == USBBaseClass.VendorSpecific &&
                               usbIf.Protocol == 0
                    );
                this.deviceOut = usbInterface.Pipes.First(p => p.IsOut);
                this.deviceIn = usbInterface.Pipes.First(p => p.IsIn);
                LogInfo("Opened Win USB interface");
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
        //         read data on the IN endpoint associated to the WinUSB interface
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
            LogInfo("Closing Win USB interface");
            this.deviceOut = null;
            this.deviceIn = null;
            usbDevice.Dispose();
            usbDevice = null;
        }

        public void setPacketSize(UInt16 size)
        {
            this.packet_size = size;
        }
    }

}
