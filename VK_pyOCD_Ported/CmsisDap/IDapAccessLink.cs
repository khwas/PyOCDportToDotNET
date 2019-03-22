using openocd.CoreSight;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// namespace pyDAPAccess
// {
// 
//     public static class dap_access_api
//     {
// 
//         public class DAPAccessIntf
//         {
// 
namespace openocd.CmsisDap
{

    // Parent of all error DAPAccess can raise
    [Serializable]
    public class Error
        : Exception
    {
    }

    // Error communicating with device
    [Serializable]
    public class DeviceError
        : Error
    {
    }

    // The host debugger reported failure for the given command
    [Serializable]
    public class CommandError
        : DeviceError
    {
    }

    // Error ocurred with a transfer over SWD or JTAG
    [Serializable]
    public class TransferError
        : CommandError
    {
    }

    // A SWD or JTAG timeout occurred
    [Serializable]
    public class TransferTimeoutError
        : TransferError
    {
    }

    // A SWD Fault occurred
    [Serializable]
    public class TransferFaultError
        : TransferError
    {

        private UInt32? _address;

        public TransferFaultError(UInt32? faultAddress = null)
        {
            this._address = faultAddress;
        }

        public UInt32? fault_address
        {
            get
            {
                return this._address;
            }
            set
            {
                this._address = value;
            }
        }

        public override string ToString()
        {
            var desc = "SWD/JTAG Transfer Fault";
            if (this._address != null)
            {
                desc += String.Format(" @ 0x{0:X08}", this._address);
            }
            return desc;
        }
    }

    // A SWD protocol error occurred
    [Serializable]
    public class TransferProtocolError
        : TransferError
    {
    }

    public interface IDapAccessConfiguration
    {
        bool isAvailable { get; }
        // Return a list of DAPAccess devices
        List<IDapAccessLink> get_connected_devices();

        // Return the DAPAccess device with the give ID
        IDapAccessLink get_device(object device_id);

        // Set arguments to configure behavior
        object set_args(object arg_list);
    }

    public interface IDapAccessLink
    {
        // ------------------------------------------- #
        //          Host control functions
        // ------------------------------------------- #
        // Open device and lock it for exclusive access
        void open();

        // Close device and unlock it
        void close();

        // Get the unique ID of this device which can be used in get_device
        // 
        //         This function is safe to call before open is called.
        //         
        string get_unique_id();

        // Return the requested information for this device
        object identify(EDapInfoIDByte item);

        // ------------------------------------------- #
        //          Target control functions
        // ------------------------------------------- #
        // Initailize DAP IO pins for JTAG or SWD
        void connect(EDapConnectPortModeByte port = EDapConnectPortModeByte.DEFAULT);

        // Send seqeunce to activate JTAG or SWD on the target
        void swj_sequence();

        // Deinitialize the DAP I/O pins
        void disconnect();

        // Set the frequency for JTAG and SWD in Hz
        // 
        //         This function is safe to call before connect is called.
        //         
        void set_clock(UInt32 frequency);

        // Return the current port type - SWD or JTAG
        EDapConnectPortModeByte get_swj_mode();

        // Reset the target
        void reset();

        // Assert or de-assert target reset line
        void assert_reset(bool asserted);

        // Allow reads and writes to be buffered for increased speed
        void set_deferred_transfer(bool enable);

        // Write out all unsent commands
        void flush();

        // Send a vendor specific command
        byte vendor(byte index, List<byte> data = null);

        // ------------------------------------------- #
        //          DAP Access functions
        // ------------------------------------------- #
        // Write a single word to a DP or AP register
        void write_reg(REG_APnDP_A3_A2 reg_id, UInt32 value, byte dap_index = 0);

        // Read a single word to a DP or AP register
        Func<UInt32> read_reg(REG_APnDP_A3_A2 reg_id, byte dap_index = 0, bool now = true);

        // Write one or more words to the same DP or AP register
        void reg_write_repeat(UInt16 num_repeats, REG_APnDP_A3_A2 reg_id, List<UInt32> data_array, byte dap_index = 0);

        // Read one or more words from the same DP or AP register
        Func<List<UInt32>> reg_read_repeat(UInt16 num_repeats, REG_APnDP_A3_A2 reg_id, byte dap_index = 0, bool now = true);
    }
}




