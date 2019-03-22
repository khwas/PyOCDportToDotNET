using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace openocd.CmsisDap
{
    public static class DapAccessConsts
    {
        public const bool LOG_PACKET_BUILDS = false;

        // Get the connected USB devices
        public static List<IBackend> _get_interfaces()
        {
            if (DapSettings.use_ws)
            {
                throw new NotImplementedException();
                //return pyDAPAccess.Interface.__init__.INTERFACE[pyDAPAccess.Interface.__init__.ws_backend].getAllConnectedInterface(DAPSettings.ws_host, DAPSettings.ws_port);
            }
            else
            {
                return DapAccessConfiguration.getAllConnectedInterface();
                //return pyDAPAccess.Interface.__init__.INTERFACE[pyDAPAccess.Interface.__init__.usb_backend].getAllConnectedInterface();
            }
        }

        // Get the unique id from an interface
        public static string _get_unique_id(IBackend anInterface)
        {
            return anInterface.getSerialNumber();
        }

    }

    public static class DapSettings
    {
        public static readonly bool use_ws = false;
        public static readonly string ws_host = "localhost";
        public static readonly UInt16 ws_port = 8081;
        public static readonly bool limit_packets = false;
    }

}
