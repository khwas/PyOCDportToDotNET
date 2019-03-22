using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using openocd.CoreSight;

/// <summary>
/// DAP - CoreSight Debug Access Port
/// </summary>
namespace openocd.CmsisDap
{
    public enum EDapCommandByte
    {
        DAP_INFO = 0,
        DAP_HOST_STATUS_LED = 1,
        DAP_CONNECT = 2,
        DAP_DISCONNECT = 3,
        DAP_TRANSFER_CONFIGURE = 4,
        DAP_TRANSFER = 5,
        DAP_TRANSFER_BLOCK = 6,
        DAP_TRANSFER_ABORT = 7,
        DAP_WRITE_ABORT = 8,
        DAP_DELAY = 9,
        DAP_RESET_TARGET = 10,
        DAP_SWJ_PINS = 16,
        DAP_SWJ_CLOCK = 17,
        DAP_SWJ_SEQUENCE = 18,
        DAP_SWD_CONFIGURE = 19,
        DAP_JTAG_SEQUENCE = 20,
        DAP_JTAG_CONFIGURE = 21,
        DAP_JTAG_IDCODE = 22,
        DAP_VENDOR0 = 128
    };

    public enum EDapReponseStatusByte
    {
        DAP_OK = 0,
        DAP_ERROR = 255,
    }

    // Information ID used for call to identify
    public enum EDapInfoIDByte
    {
        VENDOR = 1,
        PRODUCT = 2,
        SER_NUM = 3,
        FW_VER = 4,
        DEVICE_VENDOR = 5,
        DEVICE_NAME = 6,
        CAPABILITIES = 240,
        SWO_BUFFER_SIZE = 253,
        MAX_PACKET_COUNT = 254,
        MAX_PACKET_SIZE = 255,
    }

    public enum EDapHostStatusLedByte
    {
        DAP_LED_CONNECT = 0,
        DAP_LED_RUNNING = 1,
    }

    // Physical access ports
    // https://www.keil.com/pack/doc/cmsis/DAP/html/group__DAP__Config__Debug__gr.html#ga89462514881c12c1508395050ce160eb
    // #define DAP_DEFAULT_PORT   1U
    // Default communication mode on the Debug Access Port.Used for the command DAP_Connect 
    // when Port Default mode is selected. Default JTAG/SWJ Port Mode: 1 = SWD, 2 = JTAG.
    public enum EDapConnectPortModeByte
    {
        DEFAULT = 0,
        SWD = 1,
        JTAG = 2,
    }

    public enum EDapResetTargetResultByte
    {
        NO_DEVICE_SPECIFIC_RESET_SEQ_IMPLEMENTED = 0, // = no device specific reset sequence is implemented.
        DEVICE_SPECIFIC_RESET_SEQ_IS_IMPLEMENTED = 1, // = a device specific reset sequence is implemented.
    }

    // Contains information about requested access from host debugger.
    [Flags]
    public enum EDapTransferRequestByte
    {
        DP_ACC = 0,
        AP_ACC = 1 << 0,       // APnDP: 0 = Debug Port (DP), 1 = Access Port (AP).
        WRITE = 0,
        READ = 1 << 1,         // RnW: 0 = Write Register, 1 = Read Register.
        A2 = 1 << 2,           // A2 Register Address bit 2.
        A3 = 1 << 3,           // A3 Register Address bit 3.
        Value_Match = 1 << 4,  // Value Match (only valid for Read Register): 0 = Normal Read Register, 1 = Read Register with Value Match.
        Match_Mask = 1 << 5,   // Match Mask(only valid for Write Register): 0 = Normal Write Register, 1 = Write Match Mask(instead of Register).
        TD_TimeStamp = 1 << 7  // TD_TimeStamp request: 0 = No time stamp, 1 = Include time stamp value from Test Domain Timer before every Transfer Data word(restrictions see note).
    }

    // public const byte AP_ACC = 1 << 0;
    // public const byte DP_ACC = 0 << 0;
    // public const byte READ = 1 << 1;
    // public const byte WRITE = 0 << 1;
    // public const byte VALUE_MATCH = 1 << 4;
    // public const byte MATCH_MASK = 1 << 5;

    // Transfer Response: Contains information about last response from target Device.
    [Flags]
    public enum EDapTransferResponseByte
    {
        // Bit 2..0: ACK (Acknowledge) value:
        DAP_TRANSFER_SWD_OK = 1 << 0, // OK (for SWD protocol), OK or FAULT(for JTAG protocol),
        DAP_TRANSFER_JTAG_FAULT = 1 << 0,
        DAP_TRANSFER_WAIT = 1 << 1, // 2 = WAIT
        DAP_TRANSFER_FAULT = 1 << 2, // 4 = FAULT
        DAP_TRANSFER_NO_ACK = 7, // NO_ACK(no response from target)
        DAP_PROTOCOL_ERROR_SWD = 1 << 3, // Bit 3: 1 = Protocol Error(SWD)
        DAP_VALUE_MISMATCH = 1 << 4, // Bit 4: 1 = Value Mismatch(Read Register with Value Match)
    }

    // public const byte DAP_TRANSFER_OK = 1;
    // public const byte DAP_TRANSFER_WAIT = 2;
    // public const byte DAP_TRANSFER_FAULT = 4;
    // public const byte DAP_TRANSFER_NO_ACK = 7;

    [Flags]
    public enum EDapSwjPinByte
    {
        None = 0,
        SWCLK_TCK = 1 << 0,
        SWDIO_TMS = 1 << 1,
        TDI = 1 << 2,
        TDO = 1 << 3,
        nTRST = 1 << 5,
        nRESET = 1 << 7
    };

    /// <summary>
    /// This class was originally named Protocol
    /// https://www.keil.com/pack/doc/cmsis/DAP/html/index.html
    /// </summary>
    public class DebugUnitV2_0_0
    {
        private IBackend anInterface { get; }

        public DebugUnitV2_0_0(IBackend anInterface)
        {
            this.anInterface = anInterface;
        }

        public virtual object dapInfo(EDapInfoIDByte id_)
        {
            List<byte> cmd = new List<byte>
                {
                    (byte)EDapCommandByte.DAP_INFO,
                    (byte)id_
                };
            this.anInterface.write(cmd);
            List<byte> resp = this.anInterface.read();
            if (resp[0] != (byte)EDapCommandByte.DAP_INFO)
            {
                // Response is to a different command
                throw new DeviceError();
            }
            if (resp[1] == 0)
            {
                return null;
            }
            // Integer values
            if (new EDapInfoIDByte[] { EDapInfoIDByte.CAPABILITIES, EDapInfoIDByte.SWO_BUFFER_SIZE, EDapInfoIDByte.MAX_PACKET_COUNT, EDapInfoIDByte.MAX_PACKET_SIZE }.Contains(id_))
            {
                if (resp[1] == 1)
                {
                    return resp[2];
                }
                if (resp[1] == 2)
                {
                    return (UInt16)(resp[3] << 8 | resp[2]);
                }
                else throw new ArgumentOutOfRangeException("EDapInfoID id_");
            }
            // String values. They are sent as C strings with a terminating null char, so we strip it out.
            string x = Encoding.ASCII.GetString(resp.GetRange(2, resp[1]).ToArray());
            //if (x[-1] == "\x00")
            //{
            //    x = x[0:: - 1];
            //}
            return x;
        }

        public virtual byte setHostStatusLed(EDapHostStatusLedByte type, bool enabled)
        {
            List<byte> cmd = new List<byte>
                {
                    (byte)EDapCommandByte.DAP_HOST_STATUS_LED,
                    (byte)type,
                    (byte)(enabled ? 1 : 0)
                };
            this.anInterface.write(cmd);
            List<byte> resp = this.anInterface.read();
            if (resp[0] != (byte)EDapCommandByte.DAP_HOST_STATUS_LED)
            {
                // Response is to a different command
                throw new DeviceError();
            }
            if (resp[1] != 0)
            {
                // Second response byte must be 0
                throw new CommandError();
            }
            return resp[1];
        }

        public virtual EDapConnectPortModeByte connect(EDapConnectPortModeByte mode = EDapConnectPortModeByte.DEFAULT)
        {
            List<byte> cmd = new List<byte>
                {
                    (byte)EDapCommandByte.DAP_CONNECT,
                    (byte)mode
                };
            this.anInterface.write(cmd);
            List<byte> resp = this.anInterface.read();
            if (resp[0] != (byte)EDapCommandByte.DAP_CONNECT)
            {
                // Response is to a different command
                throw new DeviceError();
            }
            if (resp[1] == 0)
            {
                // DAP connect failed
                throw new CommandError();
            }
            if (resp[1] == 1)
            {
                Trace.TraceInformation("DAP SWD MODE initialized");
            }
            if (resp[1] == 2)
            {
                Trace.TraceInformation("DAP JTAG MODE initialized");
            }
            Debug.Assert(Enum.IsDefined(typeof(EDapConnectPortModeByte), (EDapConnectPortModeByte)resp[1]));
            return (EDapConnectPortModeByte)resp[1];
        }

        public virtual EDapReponseStatusByte disconnect()
        {
            List<byte> cmd = new List<byte>
                {
                    (byte)EDapCommandByte.DAP_DISCONNECT
                };
            this.anInterface.write(cmd);
            List<byte> resp = this.anInterface.read();
            if (resp[0] != (byte)EDapCommandByte.DAP_DISCONNECT)
            {
                // Response is to a different command
                throw new DeviceError();
            }
            if (resp[1] != (byte)EDapReponseStatusByte.DAP_OK)
            {
                // DAP Disconnect failed
                throw new CommandError();
            }
            return (EDapReponseStatusByte)resp[1];
        }

        public virtual EDapReponseStatusByte writeAbort(UInt32 data, byte dap_index = 0)
        {
            List<byte> cmd = new List<byte>
                {
                    (byte)EDapCommandByte.DAP_WRITE_ABORT,
                    dap_index,
                    (byte)((data >> 0) & 0xFF),
                    (byte)((data >> 8) & 0xFF),
                    (byte)((data >> 16) & 0xFF),
                    (byte)((data >> 24) & 0xFF)
                };
            this.anInterface.write(cmd);
            List<byte> resp = this.anInterface.read();
            if (resp[0] != (byte)EDapCommandByte.DAP_WRITE_ABORT)
            {
                // Response is to a different command
                throw new DeviceError();
            }
            if (resp[1] != (byte)EDapReponseStatusByte.DAP_OK)
            {
                // DAP Write Abort failed
                throw new CommandError();
            }
            return (EDapReponseStatusByte)resp[1];
        }

        public virtual EDapResetTargetResultByte resetTarget()
        {
            List<byte> cmd = new List<byte>
                {
                    (byte)EDapCommandByte.DAP_RESET_TARGET
                };
            this.anInterface.write(cmd);
            List<byte> resp = this.anInterface.read();
            if (resp[0] != (byte)EDapCommandByte.DAP_RESET_TARGET)
            {
                // Response is to a different command
                throw new DeviceError();
            }
            if (resp[1] != (byte)EDapReponseStatusByte.DAP_OK)
            {
                // DAP Reset target failed
                throw new CommandError();
            }
            if (Enum.IsDefined(typeof(EDapResetTargetResultByte), resp[2]))
            {
                return (EDapResetTargetResultByte)resp[2];
            }
            else
            {
                throw new ArgumentOutOfRangeException("EDapResetTargetResultByte");
            }
        }

        public virtual EDapReponseStatusByte transferConfigure(byte idle_cycles = 0, UInt16 wait_retry = 80, UInt16 match_retry = 0)
        {
            List<byte> cmd = new List<byte>
                {
                    (byte)EDapCommandByte.DAP_TRANSFER_CONFIGURE,
                    idle_cycles,
                    (byte)(wait_retry & 0xFF),
                    (byte)(wait_retry >> 8),
                    (byte)(match_retry & 0xFF),
                    (byte)(match_retry >> 8)
                };
            this.anInterface.write(cmd);
            List<byte> resp = this.anInterface.read();
            if (resp[0] != (byte)EDapCommandByte.DAP_TRANSFER_CONFIGURE)
            {
                // Response is to a different command
                throw new DeviceError();
            }
            if (resp[1] != (byte)EDapReponseStatusByte.DAP_OK)
            {
                // DAP Transfer Configure failed
                throw new CommandError();
            }
            return (EDapReponseStatusByte)resp[1];
        }

        public virtual Tuple<EDapTransferResponseByte, List<UInt32?>> transfer(byte dapIndex, List<Tuple<EDapTransferRequestByte, UInt32?>> transferRequests)
        {
            if (transferRequests.Count < 1 || transferRequests.Count > 255)
            {
                throw new ArgumentOutOfRangeException("transferRequests.Count");
            }
            List<byte> cmd = new List<byte>
                {
                    (byte)EDapCommandByte.DAP_TRANSFER,
                    dapIndex,
                    (byte)transferRequests.Count,
                };
            foreach (Tuple<EDapTransferRequestByte, UInt32?> request in transferRequests)
            {
                cmd.Add((byte)request.Item1);
                if (request.Item2 != null)
                {
                    cmd.Add((byte)((request.Item2 >> 0) & 0xFF));
                    cmd.Add((byte)((request.Item2 >> 8) & 0xFF));
                    cmd.Add((byte)((request.Item2 >> 16) & 0xFF));
                    cmd.Add((byte)((request.Item2 >> 24) & 0xFF));
                }
            }
            this.anInterface.write(cmd);
            List<byte> resp = this.anInterface.read();
            if (resp[0] != (byte)EDapCommandByte.DAP_TRANSFER)
            {
                // Response is to a different command
                throw new DeviceError();
            }
            if (resp[1] != (byte)EDapReponseStatusByte.DAP_OK)
            {
                // DAP Transfer Configure failed
                throw new CommandError();
            }
            ///
            Tuple<EDapTransferResponseByte, List<UInt32?>> result = new Tuple<EDapTransferResponseByte, List<UInt32?>>(0, null);
            return result;

        }

        public virtual byte setSWJClock(UInt32 clock = 1000000)
        {
            List<byte> cmd = new List<byte>
                {
                    (byte)EDapCommandByte.DAP_SWJ_CLOCK,
                    (byte)((clock >> 0) & 0xFF),
                    (byte)((clock >> 8) & 0xFF),
                    (byte)((clock >> 16) & 0xFF),
                    (byte)((clock >> 24) & 0xFF)
                };
            this.anInterface.write(cmd);
            List<byte> resp = this.anInterface.read();
            if (resp[0] != (byte)EDapCommandByte.DAP_SWJ_CLOCK)
            {
                // Response is to a different command
                throw new DeviceError();
            }
            if (resp[1] != (byte)EDapReponseStatusByte.DAP_OK)
            {
                // DAP SWJ Clock failed
                throw new CommandError();
            }
            return resp[1];
        }

        public virtual byte setSWJPins(byte output, EDapSwjPinByte pin, UInt32 wait = 0)
        {
            List<byte> cmd = new List<byte>
                {
                    (byte)EDapCommandByte.DAP_SWJ_PINS,
                    output,
                    (byte)pin,
                    (byte)((wait >> 0) & 0xFF),
                    (byte)((wait >> 8) & 0xFF),
                    (byte)((wait >> 16) & 0xFF),
                    (byte)((wait >> 24) & 0xFF),
                };
            this.anInterface.write(cmd);
            List<byte> resp = this.anInterface.read();
            if (resp[0] != (byte)EDapCommandByte.DAP_SWJ_PINS)
            {
                // Response is to a different command
                throw new DeviceError();
            }
            return resp[1];
        }

        public virtual byte swdConfigure(byte conf = 0)
        {
            List<byte> cmd = new List<byte>
                {
                    (byte)EDapCommandByte.DAP_SWD_CONFIGURE,
                    conf
                };
            this.anInterface.write(cmd);
            List<byte> resp = this.anInterface.read();
            if (resp[0] != (byte)EDapCommandByte.DAP_SWD_CONFIGURE)
            {
                // Response is to a different command
                throw new DeviceError();
            }
            if (resp[1] != (byte)EDapReponseStatusByte.DAP_OK)
            {
                // DAP SWD Configure failed
                throw new CommandError();
            }
            return resp[1];
        }

        public virtual byte swjSequence(List<byte> data)
        {
            List<byte> cmd = new List<byte>
                {
                    (byte)EDapCommandByte.DAP_SWJ_SEQUENCE,
                    (byte)(data.Count * 8)
                };
            cmd.AddRange(data);
            this.anInterface.write(cmd);
            List<byte> resp = this.anInterface.read();
            if (resp[0] != (byte)EDapCommandByte.DAP_SWJ_SEQUENCE)
            {
                // Response is to a different command
                throw new DeviceError();
            }
            if (resp[1] != (byte)EDapReponseStatusByte.DAP_OK)
            {
                // DAP SWJ Sequence failed
                throw new CommandError();
            }
            return resp[1];
        }

        public virtual byte jtagSequence(byte info, byte tdi)
        {
            List<byte> cmd = new List<byte>
                {
                    (byte)EDapCommandByte.DAP_JTAG_SEQUENCE,
                    1,
                    info,
                    tdi
                };
            this.anInterface.write(cmd);
            List<byte> resp = this.anInterface.read();
            if (resp[0] != (byte)EDapCommandByte.DAP_JTAG_SEQUENCE)
            {
                // Response is to a different command
                throw new DeviceError();
            }
            if (resp[1] != (byte)EDapReponseStatusByte.DAP_OK)
            {
                // DAP JTAG Sequence failed
                throw new CommandError();
            }
            return resp[2];
        }

        public virtual byte jtagConfigure(byte irlen, byte dev_num = 1)
        {
            List<byte> cmd = new List<byte>
                {
                    (byte)EDapCommandByte.DAP_JTAG_CONFIGURE,
                    dev_num,
                    irlen
                };
            this.anInterface.write(cmd);
            List<byte> resp = this.anInterface.read();
            if (resp[0] != (byte)EDapCommandByte.DAP_JTAG_CONFIGURE)
            {
                // Response is to a different command
                throw new DeviceError();
            }
            if (resp[1] != (byte)EDapReponseStatusByte.DAP_OK)
            {
                // DAP JTAG Configure failed
                throw new CommandError();
            }
            return resp[2];
        }

        public virtual UInt32 jtagIDCode(byte index = 0)
        {
            List<byte> cmd = new List<byte>
                {
                    (byte)EDapCommandByte.DAP_JTAG_IDCODE,
                    index
                };
            this.anInterface.write(cmd);
            List<byte> resp = this.anInterface.read();
            if (resp[0] != (byte)EDapCommandByte.DAP_JTAG_IDCODE)
            {
                // Response is to a different command
                throw new DeviceError();
            }
            if (resp[1] != (byte)EDapReponseStatusByte.DAP_OK)
            {
                // Operation failed
                throw new CommandError();
            }
            return (UInt32)(resp[2] << 0 | resp[3] << 8 | resp[4] << 16 | resp[5] << 24);
        }

        public virtual byte vendor(byte index, List<byte> data)
        {
            List<byte> cmd = new List<byte>
                {
                    (byte)((byte)EDapCommandByte.DAP_VENDOR0 + index)
                };
            cmd.AddRange(data);
            this.anInterface.write(cmd);
            List<byte> resp = this.anInterface.read();
            if (resp[0] != (byte)((byte)EDapCommandByte.DAP_VENDOR0 + index))
            {
                // Response is to a different command
                throw new DeviceError();
            }
            return resp[1];
        }

        public static EDapTransferRequestByte DapReadTransferRequestByte(REG_APnDP_A3_A2 reg_id)
        {
            Debug.Assert(Enum.IsDefined(typeof(REG_APnDP_A3_A2), reg_id));
            EDapTransferRequestByte request = EDapTransferRequestByte.READ;
            if ((int)reg_id < 4)
            {
                request |= EDapTransferRequestByte.DP_ACC;
            }
            else
            {
                request |= EDapTransferRequestByte.AP_ACC;
            }
            request |= (EDapTransferRequestByte)((int)reg_id % 4 * 4);
            return request;
        }

        public static EDapTransferRequestByte DapWriteTransferRequestByte(REG_APnDP_A3_A2 reg_id)
        {
            Debug.Assert(Enum.IsDefined(typeof(REG_APnDP_A3_A2), reg_id));
            EDapTransferRequestByte request = EDapTransferRequestByte.WRITE;
            if ((int)reg_id < 4)
            {
                request |= EDapTransferRequestByte.DP_ACC;
            }
            else
            {
                request |= EDapTransferRequestByte.AP_ACC;
            }
            request |= (EDapTransferRequestByte)((int)reg_id % 4 * 4);
            return request;
        }

    }
}

