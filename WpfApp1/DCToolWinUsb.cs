using MadWizard.WinUSBNet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using WpfApp1.UILog;

namespace WpfApp1
{

    /// <summary>
    /// Tool WinUSB supplies identity information for DebugUnit tool to instantiate USB session
    /// The only control over tool is plugging or unplugging the USB DebugUnit board(s)
    /// </summary>
    public class DCToolWinUsb : DCTool, INotifyPropertyChanged
    {
        public static TraceSource WinUSBTraceSource = new TraceSource("WinUSB", SourceLevels.All);

        /// Next DCTool in chain is DCDebugUnit
        private readonly DCToolDebugUnitSession nextTool;
        internal readonly static Guid ARM_LTD_WINUSB_GUID = new Guid("CDB3B5AD-293B-4663-AA36-1AAE46463776");
        internal readonly static UInt16 ARM_LTD_VID = 0xC251;
        internal readonly static UInt16 ARM_LTD_PID = 0xF00A;
        /// <summary>
        /// Function of DCToolWinUsb is to create/erase the DebugUnit Enumerator IToolWinUSB
        /// </summary>
        public DCToolWinUsb(DCToolDebugUnitSession dcDebugUnit) : base(WinUSBTraceSource)
        {
            Debug.Assert(dcDebugUnit != null);
            this.nextTool = dcDebugUnit;
        }

        public override string About
        {
            get =>
@"Tool WinUSB supplies identity information for DebugUnit tool to instantiate USB session
The only control over tool is plugging or unplugging the USB DebugUnit board(s).

Firmware of Debug Unit must match WinUSB related GUID: " + ARM_LTD_WINUSB_GUID.ToString().ToUpper() + @"
The Vendor VID and Product PID values should be: " + string.Format("{0:X4}", ARM_LTD_VID) + " " + string.Format("{0:X4}", ARM_LTD_PID) + @"
";
        }

        #region Commands are USB events. ITool value is driven by USB events 
        // USB events are treated as user input, clicks, keyboard commands.
        // USB events are dependent on part of UI: Window handle
        // So handling is placed in DataContext instead of ITool object
        // DataContext always exists, when ITool object life cycle is controlled by user
        private USBNotifier usbNotifier;

        public void OnLoaded(IntPtr mainWindowHandle)
        {
            this.PostAction(() =>
            {
                Debug.Assert(this.Tool == null);
                this.Tool = new ToolWinUSB();
                NotifyNextTool(string.Format("Initialized {0} Tool", Name));
                Debug.Assert(usbNotifier == null);
                usbNotifier = new USBNotifier(mainWindowHandle, ARM_LTD_WINUSB_GUID);
                usbNotifier.Arrival += UsbNotifierArrival;
                usbNotifier.Removal += UsbNotifierRemoval;
            });
        }

        private void UsbNotifierArrival(object sender, USBEvent e)
        {
            this.PostAction(() => NotifyNextTool("Debug Unit plugged"));
        }
        private void UsbNotifierRemoval(object sender, USBEvent e)
        {
            this.PostAction(() => NotifyNextTool("Debug Unit unplugged"));
        }
        private void NotifyNextTool(string reason)
        {
            USBDeviceInfo note = ((IToolWinUSB)Tool).GetUSBDeviceInfoForDebugUnit(reason);
            LogInfo("{0}. Notifying {1} Tool", reason, nextTool.Name);
            nextTool.OnUSBDeviceInfoChanged(note);
        }

        #endregion

        #region Other available commands
        public void Test1_For_Error()
        {
            PostAction(() => throw new NotImplementedException());
        }
        #endregion

        /// <summary>
        /// Function of IToolWinUSB is to enumerate exactly one CMSIS 5.3.0 DebugUnit (LPC-Link II board)
        /// When IToolWinUSB is null, it means that plugging is in progress
        /// When IToolWinUSB exists, it means that it is capable to supply enumeration
        /// </summary>
        public interface IToolWinUSB : ITool
        {
            USBDeviceInfo GetUSBDeviceInfoForDebugUnit(string reason);
        }

        public class ToolWinUSB : Loggable, IToolWinUSB
        {
            public ToolWinUSB() : base(WinUSBTraceSource) { }
            public USBDeviceInfo GetUSBDeviceInfoForDebugUnit(string reason)
            {
                USBDeviceInfo[] details = USBDevice.GetDevices(ARM_LTD_WINUSB_GUID);
                LogInfo("{0}. Enumerated {1} ARM LTD device(s)", reason, details.Length);
                if (details.Length > 0)
                {
                    foreach (USBDeviceInfo detail in details)
                    {
                        LogInfo("USBDeviceInfo.DevicePath {0}", detail.DevicePath);
                    }
                }
                USBDeviceInfo match = details.FirstOrDefault(info => info.VID == ARM_LTD_VID && info.PID == ARM_LTD_PID);
                return match;
            }
        }
    }

}