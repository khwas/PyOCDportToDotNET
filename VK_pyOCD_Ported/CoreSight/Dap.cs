using openocd.CmsisDap;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace openocd.CoreSight
{

    // Register for DAP access functions
    public enum REG_APnDP_A3_A2
    {
        DP_0x0 = 0,
        DP_0x4 = 1,
        DP_0x8 = 2,
        DP_0xC = 3,
        AP_0x0 = 4,
        AP_0x4 = 5,
        AP_0x8 = 6,
        AP_0xC = 7,
    }

    public class DebugAccessPort
    {
        public static Dictionary<string, REG_APnDP_A3_A2> DP_REG = new Dictionary<string, REG_APnDP_A3_A2>()
        {
            {"IDCODE",    REG_APnDP_A3_A2.DP_0x0},
            {"ABORT",     REG_APnDP_A3_A2.DP_0x0},
            {"CTRL_STAT", REG_APnDP_A3_A2.DP_0x4},
            {"SELECT",    REG_APnDP_A3_A2.DP_0x8}}
        ;

        public static Dictionary<string, byte> AP_REG = new Dictionary<string, byte>()
        {
            {"CSW", 0x00},
            {"TAR", 0x04},
            {"DRW", 0x0C},
            {"IDR", 0xFC},
        };

        internal const byte CTRLSTAT_STICKYORUN = 2;
        internal const byte CTRLSTAT_STICKYCMP = 16;
        internal const byte CTRLSTAT_STICKYERR = 32;
        internal const byte IDCODE = 0 << 2;
        internal const byte AP_ACC = 1 << 0;
        internal const byte DP_ACC = 0 << 0;
        internal const byte READ = 1 << 1;
        internal const byte WRITE = 0 << 1;
        internal const byte VALUE_MATCH = 1 << 4;
        internal const byte MATCH_MASK = 1 << 5;
        internal const byte A3_A2 = 0x0C;
        internal const byte APSEL_SHIFT = 24;
        internal const UInt32 APSEL = 0xFF000000; 
        internal const UInt32 APBANKSEL = 0x000000f0;
        internal const UInt32 APREG_MASK = 0x000000fc;
        internal const UInt32 DPIDR_MIN_MASK = 0x10000;
        internal const UInt16 DPIDR_VERSION_MASK = 0xf000;
        internal const byte   DPIDR_VERSION_SHIFT = 12;
        internal const UInt32 CSYSPWRUPACK = 0x80000000;
        internal const UInt32 CDBGPWRUPACK = 0x20000000;
        internal const UInt32 CSYSPWRUPREQ = 0x40000000;
        internal const UInt32 CDBGPWRUPREQ = 0x10000000;
        internal const UInt32 TRNNORMAL = 0x00000000;
        internal const UInt32 MASKLANE = 0x00000f00;
        internal const bool LOG_DAP = false;

        public static REG_APnDP_A3_A2 _ap_addr_to_reg(UInt32 addr)
        {
            REG_APnDP_A3_A2 result = (REG_APnDP_A3_A2)(4 + ((addr & A3_A2) >> 2));
            Debug.Assert(Enum.IsDefined(typeof(REG_APnDP_A3_A2), result));
            return result;
        }


        public readonly IDapAccessLink link;
        private int _dp_select;
        private int _access_number;
        public UInt32 dpidr { get; private set; }
        private UInt32 dp_version;
        private bool is_mindp;
        private Dictionary<UInt32, UInt32> _csw;

        public object DAP_LOG_FILE = "pyocd_dap.log";

        public DebugAccessPort(IDapAccessLink link)
        {
            this.link = link;
            this._csw = new Dictionary<UInt32, UInt32>
            {
            };
            this._dp_select = -1;
            this._access_number = 0;
            if (LOG_DAP)
            {
                _setup_logging();
            }
        }

        public int next_access_number
        {
            get
            {
                this._access_number += 1;
                return this._access_number;
            }
        }

        // Set up DAP logging.
        //
        // A memory handler is created that buffers log records before flushing them to a file
        // handler that writes to DAP_LOG_FILE. This improves logging performance by writing to the
        // log file less often.
        public virtual void _setup_logging()
        {
            throw new NotImplementedException();
            // var cwd = os.getcwd();
            // var logfile = os.path.join(cwd, this.DAP_LOG_FILE);
            // Trace.TraceInformation("dap logfile: %s", logfile);
            // this.logger = logging.getLogger("dap");
            // this.logger.propagate = false;
            // var formatter = logging.Formatter("%(relativeCreated)010dms:%(levelname)s:%(name)s:%(message)s");
            // var fileHandler = logging.FileHandler(logfile, mode: "w+", delay: true);
            // fileHandler.setFormatter(formatter);
            // var memHandler = logging.handlers.MemoryHandler(capacity: 128, target: fileHandler);
            // this.logger.addHandler(memHandler);
            // this.logger.setLevel(logging.DEBUG);
        }

        public virtual void init()
        {
            // Connect to the target.
            this.link.connect();
            this.link.swj_sequence();
            this.read_id_code();
            this.clear_sticky_err();
        }

        public virtual UInt32 read_id_code()
        {
            // Read ID register and get DP version
            this.dpidr = this.read_reg(DP_REG["IDCODE"])();
            this.dp_version = (this.dpidr & DPIDR_VERSION_MASK) >> DPIDR_VERSION_SHIFT;
            this.is_mindp = (this.dpidr & DPIDR_MIN_MASK) != 0;
            return this.dpidr;
        }

        public virtual void flush()
        {
            try
            {
                this.link.flush();
            }
            catch (Exception error)
            {
                this._handle_error(error, this.next_access_number);
                throw;
            }
            finally
            {
                this._csw.Clear();
                this._dp_select = -1;
            }
        }

        public virtual Func<UInt32> read_reg(REG_APnDP_A3_A2 addr, bool now = true)
        {
            return this.readDP(addr, now);
        }

        public virtual void write_reg(REG_APnDP_A3_A2 addr, UInt32 data)
        {
            this.writeDP(addr, data);
        }

        public virtual void power_up_debug()
        {
            // select bank 0 (to access DRW and TAR)
            this.write_reg(DP_REG["SELECT"], 0);
            this.write_reg(DP_REG["CTRL_STAT"], CSYSPWRUPREQ | CDBGPWRUPREQ);
            while (true)
            {
                UInt32 r = this.read_reg(DP_REG["CTRL_STAT"])();
                if ((r & (CDBGPWRUPACK | CSYSPWRUPACK)) == (CDBGPWRUPACK | CSYSPWRUPACK))
                {
                    break;
                }
            }
            this.write_reg(DP_REG["CTRL_STAT"], CSYSPWRUPREQ | CDBGPWRUPREQ | TRNNORMAL | MASKLANE);
            this.write_reg(DP_REG["SELECT"], 0);
        }

        public virtual void power_down_debug()
        {
            // select bank 0 (to access DRW and TAR)
            this.write_reg(DP_REG["SELECT"], 0);
            this.write_reg(DP_REG["CTRL_STAT"], 0);
        }

        public virtual void reset()
        {
            try
            {
                this.link.reset();
            }
            finally
            {
                this._csw.Clear();
                this._dp_select = -1;
            }
        }

        public virtual void assert_reset(bool asserted)
        {
            this.link.assert_reset(asserted);
            this._csw.Clear();
            this._dp_select = -1;
        }

        public virtual void set_clock(UInt32 frequency)
        {
            this.link.set_clock(frequency);
        }

        public virtual void find_aps()
        {
            var ap_num = 0;
            while (true)
            {
                try
                {
                    UInt32 idr = this.readAP((UInt32)((ap_num << APSEL_SHIFT) | AP_REG["IDR"]))();
                    if (idr == 0)
                    {
                        break;
                    }
                    Trace.TraceInformation("AP#{0} IDR = 0x{1:X8}", ap_num, idr);
                }
                catch (Exception e)
                {
                    Trace.TraceError("Exception reading AP#{0} IDR: {1}", ap_num, e.GetType().Name + ": " + e.Message);
                    break;
                }
                ap_num += 1;
            }
        }

        public virtual Func<UInt32> readDP(REG_APnDP_A3_A2 addr, bool now = true)
        {
            Debug.Assert(Enum.IsDefined(typeof(REG_APnDP_A3_A2), addr));
            var num = this.next_access_number;
            Func<UInt32> result_cb;
            try
            {
                result_cb = this.link.read_reg(addr, now: false);
            }
            catch (Exception error)
            {
                this._handle_error(error, num);
                throw;
            }
            uint readDPCb()
            {
                try
                {
                    var result = result_cb();
                    if (LOG_DAP)
                    {
                        Trace.TraceInformation("readDP:%06d %s(addr=0x%08x) -> 0x%08x", num, now ? "" : "...", addr, result);
                    }
                    return result;
                }
                catch (Exception error)
                {
                    this._handle_error(error, num);
                    throw;
                }
            }
            if (now)
            {
                UInt32 result = readDPCb();
                return new Func<UInt32>(() => result);
            }
            else
            {
                if (LOG_DAP)
                {
                    Trace.TraceInformation("readDP:%06d (addr=0x%08x) -> ...", num, addr);
                }
                return readDPCb;
            }
        }

        public virtual bool writeDP(REG_APnDP_A3_A2 addr, UInt32 data)
        {
            Debug.Assert(Enum.IsDefined(typeof(REG_APnDP_A3_A2), addr));
            var num = this.next_access_number;
            // Skip writing DP SELECT register if its value is not changing.
            if (addr == DP_REG["SELECT"])
            {
                if (data == this._dp_select)
                {
                    if (LOG_DAP)
                    {
                        Trace.TraceInformation("writeDP:%06d cached (addr=0x%08x) = 0x%08x", num, addr, data);
                    }
                    return true;
                }
                this._dp_select = (int)data;
            }
            // Write the DP register.
            try
            {
                if (LOG_DAP)
                {
                    Trace.TraceInformation("writeDP:%06d (addr=0x%08x) = 0x%08x", num, addr, data);
                }
                this.link.write_reg(addr, data);
            }
            catch (Exception error)
            {
                this._handle_error(error, num);
                throw;
            }
            return true;
        }

        public virtual bool writeAP(UInt32 addr, UInt32 data)
        {
            // Debug.Assert(six.integer_types.Contains(type(addr)));
            var num = this.next_access_number;
            UInt32 ap_sel = addr & APSEL;
            byte bank_sel = (byte)(addr & APBANKSEL);
            byte ap_regaddr = (byte)(addr & APREG_MASK);
            // Don't need to write CSW if it's not changing value.
            if (ap_regaddr == AP_REG["CSW"])
            {
                if (this._csw.ContainsKey(ap_sel) && (data == this._csw[ap_sel]))
                {
                    if (LOG_DAP)
                    {
                        Trace.TraceInformation("writeAP:%06d cached (addr=0x%08x) = 0x%08x", num, addr, data);
                    }
                    return true;
                }
                this._csw[ap_sel] = data;
            }
            // Select the AP and bank.
            this.writeDP(DP_REG["SELECT"], ap_sel | bank_sel);
            // Perform the AP register write.
            REG_APnDP_A3_A2 ap_reg = _ap_addr_to_reg(WRITE | AP_ACC | (addr & A3_A2));
            try
            {
                if (LOG_DAP)
                {
                    Trace.TraceInformation("writeAP:%06d (addr=0x%08x) = 0x%08x", num, addr, data);
                }
                this.link.write_reg(ap_reg, data);
            }
            catch (Exception error)
            {
                this._handle_error(error, num);
                throw;
            }
            return true;
        }

        public virtual Func<UInt32> readAP(UInt32 addr, bool now = true)
        {
            // Debug.Assert(six.integer_types.Contains(type(addr)));
            var num = this.next_access_number;
            //object res = null;
            REG_APnDP_A3_A2 ap_reg = _ap_addr_to_reg(READ | AP_ACC |(addr & A3_A2));
            Func<UInt32> result_cb;
            try
            {
                UInt32 ap_sel = addr & APSEL;
                var bank_sel = addr & APBANKSEL;
                this.writeDP(DP_REG["SELECT"], ap_sel | bank_sel);
                result_cb = this.link.read_reg(ap_reg, now: false);
            }
            catch (Exception error)
            {
                this._handle_error(error, num);
                throw;
            }
            uint readAPCb()
            {
                try
                {
                    var result = result_cb();
                    if (LOG_DAP)
                    {
                        Trace.TraceInformation("readAP:%06d %s(addr=0x%08x) -> 0x%08x", num, now ? "" : "...", addr, result);
                    }
                    return result;
                }
                catch (Exception error)
                {
                    this._handle_error(error, num);
                    throw;
                }
            }
            if (now)
            {
                UInt32 result = readAPCb();
                return new Func<UInt32>(() => result);
            }
            else
            {
                if (LOG_DAP)
                {
                    Trace.TraceInformation("readAP:%06d (addr=0x%08x) -> ...", num, addr);
                }
                return readAPCb;
            }
        }

        public virtual void _handle_error(Exception error, int num)
        {
            if (LOG_DAP)
            {
                Trace.TraceInformation("error:%06d %s", num, error);
            }
            // Invalidate cached registers
            this._csw.Clear();
            this._dp_select = -1;
            // Clear sticky error for Fault errors only
            if (error is TransferFaultError)
            {
                this.clear_sticky_err();
            }
        }

        public virtual void clear_sticky_err()
        {
            EDapConnectPortModeByte mode = this.link.get_swj_mode();
            if (mode == EDapConnectPortModeByte.SWD)
            {
                this.link.write_reg(REG_APnDP_A3_A2.DP_0x0, 1 << 2);
            }
            else if (mode == EDapConnectPortModeByte.JTAG)
            {
                this.link.write_reg(DP_REG["CTRL_STAT"], CTRLSTAT_STICKYERR);
            }
            else
            {
                Debug.Assert(false);
            }
        }
    }
}
