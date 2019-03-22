using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using openocd.CmsisDap;
using openocd.CoreSight;

namespace openocd.CmsisDap
{
    public class DapAccessLink : IDapAccessLink
    {
        // ------------------------------------------- #
        //          Static Functions
        // ------------------------------------------- #
        // 
        // Return an array of all mbed boards connected
        //         
        // [staticmethod]
        public static List<IDapAccessLink> get_connected_devices()
        {
            List<IDapAccessLink> all_daplinks = new List<IDapAccessLink>();
            var all_interfaces = DapAccessConsts._get_interfaces();
            foreach (var anInterface in all_interfaces)
            {
                try
                {
                    var unique_id = DapAccessConsts._get_unique_id(anInterface);
                    DapAccessLink new_daplink = new DapAccessLink(unique_id);
                    all_daplinks.Add(new_daplink);
                }
                catch
                {
                    //var logger = logging.getLogger(@__name__);
                    Trace.TraceError("Failed to get unique id");
                }
            }
            return all_daplinks;
        }

        public static object get_device(string device_id)
        {
            return new DapAccessLink(device_id);
        }

        // [staticmethod]
        // public static object set_args(object arg_list)
        // {
        //     // Example: arg_list =['use_ws=True', 'ws_host=localhost', 'ws_port=8081']
        //     var arg_pattern = re.compile("([^=]+)=(.*)");
        //     if (arg_list)
        //     {
        //         foreach (var arg in arg_list)
        //         {
        //             var match = arg_pattern.match(arg);
        //             // check if arguments have correct format
        //             if (match)
        //             {
        //                 var attr = match.group(1);
        //                 if (hasattr(dap_settings.DAPSettings, attr))
        //                 {
        //                     var val = match.group(2);
        //                     // convert string to int or bool
        //                     if (val.isdigit())
        //                     {
        //                         val = Convert.ToInt32(val);
        //                     }
        //                     else if (val == "True")
        //                     {
        //                         val = true;
        //                     }
        //                     else if (val == "False")
        //                     {
        //                         val = false;
        //                     }
        //                     setattr(dap_settings.DAPSettings, attr, val);
        //                 }
        //             }
        //         }
        //     }
        // }

        private IBackend _backend_interface;
        private bool _deferred_transfer;
        private DebugUnitV2_0_0 _protocol;
        private string _unique_id;
        private UInt32 _frequency;
        private EDapConnectPortModeByte? _dap_port;
        private List<Transfer> _transfer_list;
        internal Command _crnt_cmd;
        private byte? _packet_count;
        private UInt16? _packet_size;
        internal List<Command> _commands_to_read;
        private List<byte> _command_response_buf;

        public DapAccessLink(string unique_id, IBackend backend_interface = null)
        {
            // super(DAPAccessCMSISDAP, this).@__init__();
            this._backend_interface = backend_interface;
            this._deferred_transfer = false;
            this._protocol = null;
            this._packet_count = null;
            this._unique_id = unique_id;
            this._frequency = 1000000;
            this._dap_port = null;
            this._transfer_list = null;
            this._crnt_cmd = null;
            this._packet_size = null;
            this._commands_to_read = null;
            this._command_response_buf = null;
            //this._logger = logging.getLogger(@__name__);
            return;
        }

        public void open()
        {
            if (this._backend_interface == null)
            {
                IEnumerable<IBackend> all_interfaces = DapAccessConsts._get_interfaces();
                foreach (var anInterface in all_interfaces)
                {
                    try
                    {
                        string unique_id = DapAccessConsts._get_unique_id(anInterface);
                        if (this._unique_id == unique_id)
                        {
                            // This assert could indicate that two boards
                            // had the same ID
                            Debug.Assert(this._backend_interface == null);
                            this._backend_interface = anInterface;
                        }
                    }
                    catch (Exception)
                    {
                        Trace.TraceError("Failed to get unique id for open");
                    }
                }
                if (this._backend_interface == null)
                {
                    throw new Exception("Unable to open device", new DeviceError());
                }
            }
            this._backend_interface.open();
            this._protocol = new DebugUnitV2_0_0(this._backend_interface);
            if (DapSettings.limit_packets)
            {
                this._packet_count = 1;
                Trace.TraceInformation("Limiting packet count to {0}", this._packet_count);
            }
            else
            {
                object dapInfo = this._protocol.dapInfo(EDapInfoIDByte.MAX_PACKET_COUNT);
                Debug.Assert(dapInfo != null);
                this._packet_count = (byte)dapInfo;
            }
            this._backend_interface.packet_count = (byte)this._packet_count;
            {
                object dapInfo = this._protocol.dapInfo(EDapInfoIDByte.MAX_PACKET_SIZE);
                Debug.Assert(dapInfo != null);
                this._packet_size = (UInt16)dapInfo;
            }
            this._backend_interface.setPacketSize((UInt16)this._packet_size);
            this._init_deferred_buffers();
        }

        public void close()
        {
            Debug.Assert(this._backend_interface != null);
            this.flush();
            this._backend_interface.close();
        }

        public string get_unique_id()
        {
            return this._unique_id;
        }

        public void reset()
        {
            this.flush();
            this._protocol.setSWJPins(0, EDapSwjPinByte.nRESET);
            Thread.Sleep(100);
            this._protocol.setSWJPins(128, EDapSwjPinByte.nRESET);
            Thread.Sleep(100);
        }

        public void assert_reset(bool asserted)
        {
            this.flush();
            if (asserted)
            {
                this._protocol.setSWJPins(0, EDapSwjPinByte.nRESET);
            }
            else
            {
                this._protocol.setSWJPins(128, EDapSwjPinByte.nRESET);
            }
        }

        public void set_clock(UInt32 frequency)
        {
            this.flush();
            this._protocol.setSWJClock(frequency);
            this._frequency = frequency;
        }

        public EDapConnectPortModeByte get_swj_mode()
        {
            return (EDapConnectPortModeByte)this._dap_port;
        }

        // 
        //         Allow transfers to be delayed and buffered
        // 
        //         By default deferred transfers are turned off.  All reads and
        //         writes will be completed by the time the function returns.
        // 
        //         When enabled packets are buffered and sent all at once, which
        //         increases speed.  When memory is written to, the transfer
        //         might take place immediately, or might take place on a future
        //         memory write.  This means that an invalid write could cause an
        //         exception to occur on a later, unrelated write.  To guarantee
        //         that previous writes are complete call the flush() function.
        // 
        //         The behaviour of read operations is determined by the modes
        //         READ_START, READ_NOW and READ_END.  The option READ_NOW is the
        //         default and will cause the read to flush all previous writes,
        //         and read the data immediately.  To improve performance, multiple
        //         reads can be made using READ_START and finished later with READ_NOW.
        //         This allows the reads to be buffered and sent at once.  Note - All
        //         READ_ENDs must be called before a call using READ_NOW can be made.
        //         
        public void set_deferred_transfer(bool enable)
        {
            if (this._deferred_transfer && !enable)
            {
                this.flush();
            }
            this._deferred_transfer = enable;
        }

        public void flush()
        {
            // Send current packet
            this._send_packet();
            // Read all backlogged
            foreach (var _ in Enumerable.Range(0, this._commands_to_read.Count))
            {
                this._read_packet();
            }
        }

        public object identify(EDapInfoIDByte item)
        {
            //Debug.Assert(item is ID);
            return this._protocol.dapInfo(item);
        }

        public byte vendor(byte index, List<byte> data = null)
        {
            if (data == null)
            {
                data = new List<byte>();
            }
            return this._protocol.vendor(index, data);
        }

        // ------------------------------------------- #
        //             Target access functions
        // ------------------------------------------- #
        public void connect(EDapConnectPortModeByte port = EDapConnectPortModeByte.DEFAULT)
        {
            //Debug.Assert(port is PORT);
            EDapConnectPortModeByte actual_port = this._protocol.connect(port);
            this._dap_port = actual_port;
            // set clock frequency
            this._protocol.setSWJClock(this._frequency);
            // configure transfer
            this._protocol.transferConfigure();
        }

        public void swj_sequence()
        {
            if (this._dap_port == EDapConnectPortModeByte.SWD)
            {
                // configure swd protocol
                this._protocol.swdConfigure();
                // switch from jtag to swd
                this._jtag_to_swd();
            }
            else if (this._dap_port == EDapConnectPortModeByte.JTAG)
            {
                // configure jtag protocol
                this._protocol.jtagConfigure(4);
                // Test logic reset, run test idle
                this._protocol.swjSequence(new List<byte> {
                        31
                    });
            }
            else
            {
                Debug.Assert(false);
            }
        }

        public void disconnect()
        {
            this.flush();
            this._protocol.disconnect();
        }

        public void write_reg(REG_APnDP_A3_A2 reg_id, UInt32 value, byte dap_index = 0)
        {
            Debug.Assert(Enum.IsDefined(typeof(REG_APnDP_A3_A2), reg_id));
            EDapTransferRequestByte request = DebugUnitV2_0_0.DapWriteTransferRequestByte(reg_id);
            this._write(dap_index, 1, request, new List<UInt32> {
                    value
                });
        }

        public Func<UInt32> read_reg(REG_APnDP_A3_A2 reg_id, byte dap_index = 0, bool now = true)
        {
            Debug.Assert(Enum.IsDefined(typeof(REG_APnDP_A3_A2), reg_id));
            EDapTransferRequestByte request = DebugUnitV2_0_0.DapReadTransferRequestByte(reg_id);
            var transfer = this._write(dap_index, 1, request, null);
            Debug.Assert(transfer != null);
            uint read_reg_cb()
            {
                List<UInt32> res = transfer.get_result();
                Debug.Assert(res.Count == 1);
                return res[0];
            }
            if (now)
            {
                UInt32 result = read_reg_cb();
                return new Func<UInt32>(() => result);
            }
            else
            {
                return new Func<UInt32>(() => read_reg_cb());
            }
        }

        public void reg_write_repeat(UInt16 num_repeats, REG_APnDP_A3_A2 reg_id, List<UInt32> data_array, byte dap_index = 0)
        {
            //Debug.Assert(num_repeats is six.integer_types);
            Debug.Assert(num_repeats == data_array.Count);
            Debug.Assert(Enum.IsDefined(typeof(REG_APnDP_A3_A2), reg_id));
            //Debug.Assert(dap_index is six.integer_types);
            EDapTransferRequestByte request = DebugUnitV2_0_0.DapWriteTransferRequestByte(reg_id);
            this._write(dap_index, num_repeats, request, data_array);
        }

        public Func<List<UInt32>> reg_read_repeat(UInt16 num_repeats, REG_APnDP_A3_A2 reg_id, byte dap_index = 0, bool now = true)
        {
            //Debug.Assert(num_repeats is six.integer_types);
            Debug.Assert(Enum.IsDefined(typeof(REG_APnDP_A3_A2), reg_id));
            //Debug.Assert(dap_index is six.integer_types);
            //Debug.Assert(now is @bool);
            EDapTransferRequestByte request = DebugUnitV2_0_0.DapReadTransferRequestByte(reg_id);
            var transfer = this._write(dap_index, num_repeats, request, null);
            Debug.Assert(transfer != null);
            List<uint> reg_read_repeat_cb()
            {
                List<UInt32> res = transfer.get_result();
                Debug.Assert(res.Count == num_repeats);
                return res;
            }
            if (now)
            {
                List<UInt32> result = reg_read_repeat_cb();
                return new Func<List<UInt32>>(() => result);
            }
            else
            {
                return new Func<List<UInt32>>(() => reg_read_repeat_cb());
            }
        }

        // ------------------------------------------- #
        //          Private functions
        // ------------------------------------------- #
        // 
        //         Initialize or reinitalize all the deferred transfer buffers
        // 
        //         Calling this method will drop all pending transactions
        //         so use with care.
        //         
        public virtual void _init_deferred_buffers()
        {
            // List of transfers that have been started, but
            // not completed (started by write_reg, read_reg,
            // reg_write_repeat and reg_read_repeat)
            this._transfer_list = new List<Transfer>(); // collections.deque();
                                                        // The current packet - this can contain multiple
                                                        // different transfers
            this._crnt_cmd = new Command((UInt16)this._packet_size);
            // Packets that have been sent but not read
            this._commands_to_read = new List<Command>(); // collections.deque();
                                                          // Buffer for data returned for completed commands.
                                                          // This data will be added to transfers
            this._command_response_buf = new List<byte>();
        }

        // 
        //         Reads and decodes a single packet
        // 
        //         Reads a single packet from the device and
        //         stores the data from it in the current Command
        //         object
        //         
        public virtual void _read_packet()
        {
            List<byte> decoded_data;
            // Grab command, send it and decode response
            Command cmd = this._commands_to_read[0];
            this._commands_to_read.RemoveAt(0); // popleft();
            try
            {
                List<byte> raw_data = this._backend_interface.read();
                decoded_data = cmd.decode_data(raw_data);
            }
            catch (Exception e)
            {
                this._abort_all_transfers(e);
                throw;
            }
            this._command_response_buf.AddRange(decoded_data);
            // Attach data to transfers
            UInt32 pos = 0;
            while (true)
            {
                var size_left = this._command_response_buf.Count - pos;
                if (size_left == 0)
                {
                    // If size left is 0 then the transfer list might
                    // be empty, so don't try to access element 0
                    //Debug.Assert(this._transfer_list.Count <= 0);
                    break;
                }
                var transfer = this._transfer_list[0];
                UInt32 size = transfer.get_data_size();
                if (size > size_left)
                {
                    break;
                }
                this._transfer_list.RemoveAt(0); // popleft();
                var data = this._command_response_buf.GetRange((int)pos, (int)size);
                pos += size;
                transfer.add_response(data);
            }
            // Remove used data from _command_response_buf
            if (pos > 0)
            {
                this._command_response_buf = this._command_response_buf.GetRange((int)pos, (int)(this._command_response_buf.Count - pos));
            }
        }

        // 
        //         Send a single packet to the interface
        // 
        //         This function guarantees that the number of packets
        //         that are stored in daplink's buffer (the number of
        //         packets written but not read) does not exceed the
        //         number supported by the given device.
        //         
        public virtual void _send_packet()
        {
            var cmd = this._crnt_cmd;
            if (cmd.get_empty())
            {
                return;
            }
            byte max_packets = this._backend_interface.packet_count;
            if (this._commands_to_read.Count >= max_packets)
            {
                this._read_packet();
            }
            var data = cmd.encode_data();
            try
            {
                this._backend_interface.write(data.ToList());
            }
            catch (Exception e)
            {
                this._abort_all_transfers(e);
                throw;
            }
            this._commands_to_read.Add(cmd);
            this._crnt_cmd = new Command((UInt16)this._packet_size);
        }

        // 
        //         Write one or more commands
        //         
        public virtual Transfer _write(byte dap_index, UInt16 transfer_count, EDapTransferRequestByte transfer_request, List<UInt32> transfer_data)
        {
            List<UInt32> data;
            Debug.Assert(dap_index == 0);
            Debug.Assert(transfer_data == null || transfer_data.Count > 0);
            // Create transfer and add to transfer list
            Transfer transfer = null;
            if ((transfer_request & EDapTransferRequestByte.READ) != 0)
            {
                transfer = new Transfer(this, dap_index, transfer_count, transfer_request, transfer_data);
                this._transfer_list.Add(transfer);
            }
            // Build physical packet by adding it to command
            var cmd = this._crnt_cmd;
            var is_read = transfer_request & EDapTransferRequestByte.READ;
            UInt16 size_to_transfer = transfer_count;
            UInt16 trans_data_pos = 0;
            while (size_to_transfer > 0)
            {
                // Get the size remaining in the current packet for the given request.
                UInt16 size = cmd.get_request_space(size_to_transfer, transfer_request, dap_index);
                // This request doesn't fit in the packet so send it.
                if (size == 0)
                {
                    if (DapAccessConsts.LOG_PACKET_BUILDS)
                    {
                        Trace.TraceInformation("_write: send packet [size==0]");
                    }
                    this._send_packet();
                    cmd = this._crnt_cmd;
                    continue;
                }
                // Add request to packet.
                if (transfer_data == null)
                {
                    data = null;
                }
                else
                {
                    data = transfer_data.GetRange(trans_data_pos, size);
                }
                cmd.add(size, transfer_request, data, dap_index);
                size_to_transfer -= size;
                trans_data_pos += size;
                // Packet has been filled so send it
                if (cmd.get_full())
                {
                    if (DapAccessConsts.LOG_PACKET_BUILDS)
                    {
                        Trace.TraceInformation("_write: send packet [full]");
                    }
                    this._send_packet();
                    cmd = this._crnt_cmd;
                }
            }
            if (!this._deferred_transfer)
            {
                this.flush();
            }
            return transfer;
        }

        // 
        //         Send the command to switch from SWD to jtag
        //         
        public virtual void _jtag_to_swd()
        {
            List<byte> data = new List<byte>() { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
            this._protocol.swjSequence(data);
            data = new List<byte>() { 0x9E, 0xE7 };
            this._protocol.swjSequence(data);
            data = new List<byte>() { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
            this._protocol.swjSequence(data);
            data = new List<byte>() { 0x00 };
            this._protocol.swjSequence(data);
        }

        // 
        //         Abort any ongoing transfers and clear all buffers
        //         
        public virtual void _abort_all_transfers(Exception exception)
        {
            var pending_reads = this._commands_to_read.Count;
            // invalidate _transfer_list
            foreach (var transfer in this._transfer_list)
            {
                transfer.add_error(exception);
            }
            // clear all deferred buffers
            this._init_deferred_buffers();
            // finish all pending reads and ignore the data
            // Only do this if the error is a tranfer error.
            // Otherwise this could cause another exception
            if (exception is TransferError)
            {
                foreach (var _ in Enumerable.Range(0, pending_reads))
                {
                    this._backend_interface.read();
                }
            }
        }

    }
}
