using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//using DAPAccess = pyDAPAccess.DAPAccess;

// using ROMTable = rom_table.ROMTable;

//using dap;

//using _ap_addr_to_reg = dap._ap_addr_to_reg;

//using READ = dap.READ;

//using WRITE = dap.WRITE;

//using AP_ACC = dap.AP_ACC;

//using APSEL_SHIFT = dap.APSEL_SHIFT;

//using LOG_DAP = dap.LOG_DAP;

//using conversion = utility.conversion;

//using logging;




using System.Diagnostics;
using openocd.CmsisDap; //using pyDAPAccess;
//using utility;

namespace openocd.CoreSight
{

    public class AccessPort
    {
        public const byte AP_ROM_TABLE_ADDR_REG = 0xF8; // 248;
        public const byte AP_ROM_TABLE_FORMAT_MASK = 0x2;
        public const byte AP_ROM_TABLE_ENTRY_PRESENT_MASK = 0x1;
        public static Dictionary<UInt32, UInt16> MEM_AP_IDR_TO_WRAP_SIZE = new Dictionary<UInt32, UInt16>
        {
        { 0x24770011 , 0x1000 },    // Used on m4 & m3 - Documented in arm_cortexm4_processor_trm_100166_0001_00_en.pdf
                                    //                   and arm_cortexm3_processor_trm_100165_0201_00_en.pdf
        { 0x44770001 , 0x400},      // Used on m1 - Documented in DDI0413D_cortexm1_r1p0_trm.pdf
        { 0x04770031 , 0x400},      // Used on m0+? at least on KL25Z, KL46, LPC812
        { 0x04770021 , 0x400},      // Used on m0? used on nrf51, lpc11u24
        { 0x64770001 , 0x400},      // Used on m7
        { 0x74770001 , 0x400 },     // Used on m0+ on KL28Z
        };
        //  AP Control and Status Word definitions
        public const UInt32 CSW_SIZE = 0x00000007;
        public const UInt32 CSW_SIZE8 = 0x00000000;
        public const UInt32 CSW_SIZE16 = 0x00000001;
        public const UInt32 CSW_SIZE32 = 0x00000002;
        public const UInt32 CSW_ADDRINC = 0x00000030;
        public const UInt32 CSW_NADDRINC = 0x00000000;
        public const UInt32 CSW_SADDRINC = 0x00000010;
        public const UInt32 CSW_PADDRINC = 0x00000020;
        public const UInt32 CSW_DBGSTAT = 0x00000040;
        public const UInt32 CSW_TINPROG = 0x00000080;
        public const UInt32 CSW_HPROT = 0x02000000;
        public const UInt32 CSW_MSTRTYPE = 0x20000000;
        public const UInt32 CSW_MSTRCORE = 0x00000000;
        public const UInt32 CSW_MSTRDBG = 0x20000000;
        public const UInt32 CSW_RESERVED = 0x01000000;

        public const UInt32 CSW_VALUE = CSW_RESERVED | CSW_MSTRDBG | CSW_HPROT | CSW_DBGSTAT | CSW_SADDRINC;

        public static Dictionary<int, UInt32> TRANSFER_SIZE = new Dictionary<int, UInt32>()
        {
            { 8, CSW_SIZE8},
            {16, CSW_SIZE16},
            {32, CSW_SIZE32}
        };

        // Debug Exception and Monitor Control Register
        public const UInt32 DEMCR = 0xE000EDFC;
        public const UInt32 DEMCR_TRCENA = 1 << 24;

        internal readonly DebugAccessPort dp;
        internal readonly UInt32 ap_num;

        private bool inited_primary;
        private bool inited_secondary;
        internal readonly IDapAccessLink link;
        internal UInt32 idr;
        public UInt32 rom_addr { get; private set; }
        private bool has_rom_table;
        public RomTable.ROMTable rom_table;

        public AccessPort(DebugAccessPort dp, UInt32 ap_num)
        {
            this.dp = dp;
            this.ap_num = ap_num;
            this.link = dp.link;
            this.idr = 0;
            this.rom_addr = 0;
            this.has_rom_table = false;
            this.rom_table = null;
            this.inited_primary = false;
            this.inited_secondary = false;
            if (DebugAccessPort.LOG_DAP)
            {
                //this.logger = this.dp.logger.getChild(String.Format("ap%d", ap_num));
            }
        }

        public virtual void init(bool bus_accessible = true)
        {
            if (!this.inited_primary)
            {
                this.idr = this.read_reg(DebugAccessPort.AP_REG["IDR"])();
                // Init ROM table
                this.rom_addr = this.read_reg(AP_ROM_TABLE_ADDR_REG)();
                this.has_rom_table = (this.rom_addr != 0xffffffff) && ((this.rom_addr & AP_ROM_TABLE_ENTRY_PRESENT_MASK) != 0);
                this.rom_addr &= 0xfffffffc; // clear format and present bits
                this.inited_primary = true;
            }
            if (!this.inited_secondary && this.has_rom_table && bus_accessible)
            {
                this.init_rom_table();
                this.inited_secondary = true;
            }
        }

        public virtual void init_rom_table()
        {
            this.rom_table = new RomTable.ROMTable(this as MEM_AP);
            this.rom_table.init();
        }

        public virtual Func<UInt32> read_reg(UInt32 addr, bool now = true)
        {
            return this.dp.readAP(this.ap_num << DebugAccessPort.APSEL_SHIFT | addr, now);
        }

        public virtual void write_reg(UInt32 addr, UInt32 data)
        {
            this.dp.writeAP(this.ap_num << DebugAccessPort.APSEL_SHIFT | addr, data);
        }
    }

    public class MEM_AP
        : AccessPort
    {

        internal UInt32 auto_increment_page_size;

        public MEM_AP(DebugAccessPort dp, UInt32 ap_num)
            : base(dp, ap_num)
        {
            // Default to the smallest size supported by all targets.
            // A size smaller than the supported size will decrease performance
            // due to the extra address writes, but will not create any
            // read/write errors.
            this.auto_increment_page_size = 1024;
        }

        public override void init(bool bus_accessible = true)
        {
            base.init(bus_accessible);
            // Look up the page size based on AP ID.
            try
            {
                this.auto_increment_page_size = MEM_AP_IDR_TO_WRAP_SIZE[this.idr];
            }
            catch (KeyNotFoundException)
            {
                Trace.TraceWarning(String.Format("Unknown MEM-AP IDR: 0x{0:X}", this.idr));
            }
        }

        // Write a single memory location.
        //
        // By default the transfer size is a word
        public virtual void writeMemory(UInt32 addr, UInt32 data, byte transfer_size = 32)
        {
            var num = this.dp.next_access_number;
            if (DebugAccessPort.LOG_DAP)
            {
                Trace.TraceInformation("writeMem:{0:000000} (addr=0x{1:X8}, size={2}) = 0x{3:X8} {{", num, addr, transfer_size, data);
            }
            this.write_reg(DebugAccessPort.AP_REG["CSW"], CSW_VALUE | TRANSFER_SIZE[transfer_size]);
            if (transfer_size == 8)
            {
                data = (data << (int)((addr & 3) << 3));
            }
            else if (transfer_size == 16)
            {
                data = data << (int)((addr & 2) << 3);
            }
            try
            {
                this.write_reg(DebugAccessPort.AP_REG["TAR"], addr);
                this.write_reg(DebugAccessPort.AP_REG["DRW"], data);
            }
            catch (Exception error)
            {
                // Annotate error with target address.
                this._handle_error(error, num);
                throw new Exception(string.Format("Fault Address {0:X8}", addr), error); // error.fault_address = addr;
            }
            if (DebugAccessPort.LOG_DAP)
            {
                Trace.TraceInformation("writeMem:{0:000000} }}", num);
            }
        }

        // Read a memory location.
        //
        // By default, a word will be read.
        public virtual Func<UInt32> readMemory(UInt32 addr, UInt32 transfer_size = 32, bool now = true)
        {
            var num = this.dp.next_access_number;
            if (DebugAccessPort.LOG_DAP)
            {
                Trace.TraceInformation("readMem:{0:000000} (addr=0x{1:X8}, size={2}) {{", num, addr, transfer_size);
            }
            Func<UInt32> result_cb;
            try
            {
                this.write_reg(DebugAccessPort.AP_REG["CSW"], CSW_VALUE | TRANSFER_SIZE[(int)transfer_size]);
                this.write_reg(DebugAccessPort.AP_REG["TAR"], addr);
                result_cb = this.read_reg(DebugAccessPort.AP_REG["DRW"], now: false);
            }
            catch (Exception error)
            {
                // Annotate error with target address.
                this._handle_error(error, num);
                throw new Exception(string.Format("Fault Address {0:X8}", addr), error); // error.fault_address = addr;
            }
            uint readMemCb()
            {
                UInt32 res;
                try
                {
                    res = result_cb();
                    if (transfer_size == 8)
                    {
                        res = (res >> (int)((addr & 3) << 3)) & 0xFF;
                    }
                    else if (transfer_size == 16)
                    {
                        res = (res >> (int)((addr & 2) << 3)) & 0xFFFF;
                    }
                    if (DebugAccessPort.LOG_DAP)
                    {
                        Trace.TraceInformation("readMem:{0:000000} {1}(addr=0x{2:X8}, size={3}) -> 0x{4:X8} }}", num, now ? "" : "...", addr, transfer_size, res);
                    }
                }
                catch (Exception error)
                {
                    // Annotate error with target address.
                    this._handle_error(error, num);
                    throw new Exception(string.Format("Fault Address {0:X8}", addr), error); // error.fault_address = addr;
                }
                return res;
            }
            if (now)
            {
                var result = readMemCb();
                return new Func<UInt32>(() => result);
            }
            else
            {
                return readMemCb;
            }
        }

        // write aligned word ("data" are words)
        public virtual void _writeBlock32(UInt32 addr, List<UInt32> data)
        {
            var num = this.dp.next_access_number;
            if (DebugAccessPort.LOG_DAP)
            {
                Trace.TraceInformation("_writeBlock32:%06d (addr=0x%08x, size=%d) {", num, addr, data.Count);
            }
            // put address in TAR
            this.write_reg(DebugAccessPort.AP_REG["CSW"], CSW_VALUE | CSW_SIZE32);
            this.write_reg(DebugAccessPort.AP_REG["TAR"], addr);
            try
            {
                REG_APnDP_A3_A2 reg = DebugAccessPort._ap_addr_to_reg(this.ap_num << DebugAccessPort.APSEL_SHIFT | DebugAccessPort.WRITE | DebugAccessPort.AP_ACC | DebugAccessPort.AP_REG["DRW"]);
                this.link.reg_write_repeat((UInt16)data.Count, reg, data);
            }
            catch (Exception error)
            {
                // Annotate error with target address.
                this._handle_error(error, num);
                throw new Exception(string.Format("Fault Address {0:X8}", addr), error); // error.fault_address = addr;
            }
            if (DebugAccessPort.LOG_DAP)
            {
                Trace.TraceInformation("_writeBlock32:%06d }", num);
            }
        }

        // read aligned word (the size is in words)
        public virtual List<UInt32> _readBlock32(UInt32 addr, UInt16 size)
        {
            var num = this.dp.next_access_number;
            if (DebugAccessPort.LOG_DAP)
            {
                Trace.TraceInformation("_readBlock32:%06d (addr=0x%08x, size=%d) {", num, addr, size);
            }
            // put address in TAR
            this.write_reg(DebugAccessPort.AP_REG["CSW"], CSW_VALUE | CSW_SIZE32);
            this.write_reg(DebugAccessPort.AP_REG["TAR"], addr);
            List<UInt32> resp;
            try
            {
                REG_APnDP_A3_A2 reg = DebugAccessPort._ap_addr_to_reg((this.ap_num << DebugAccessPort.APSEL_SHIFT) | DebugAccessPort.READ | DebugAccessPort.AP_ACC | DebugAccessPort.AP_REG["DRW"]);
                resp = this.link.reg_read_repeat(size, reg)();
            }
            catch (Exception error)
            {
                // Annotate error with target address.
                this._handle_error(error, num);
                throw new Exception(string.Format("Fault Address {0:X8}", addr), error); // error.fault_address = addr;
            }
            if (DebugAccessPort.LOG_DAP)
            {
                Trace.TraceInformation("_readBlock32:%06d }", num);
            }
            return resp;
        }

        // Shorthand to write a 32-bit word.
        public virtual void write32(UInt32 addr, UInt32 value)
        {
            this.writeMemory(addr, value, 32);
        }

        // Shorthand to write a 16-bit halfword.
        public virtual void write16(UInt32 addr, UInt16 value)
        {
            this.writeMemory(addr, value, 16);
        }

        // Shorthand to write a byte.
        public virtual void write8(UInt32 addr, byte value)
        {
            this.writeMemory(addr, value, 8);
        }

        // Shorthand to read a 32-bit word.
        public virtual Func<UInt32> read32(UInt32 addr, bool now = true)
        {
            return new Func<UInt32>(() => this.readMemory(addr, 32, now)());
        }

        // Shorthand to read a 16-bit halfword.
        public virtual Func<UInt16> read16(UInt32 addr, bool now = true)
        {
            return new Func<UInt16>(() => (UInt16)this.readMemory(addr, 16, now)());
        }

        // Shorthand to read a byte.
        public virtual Func<byte> read8(UInt32 addr, bool now = true)
        {
            return new Func<Byte>(() => (Byte)this.readMemory(addr, 8, now)());
        }

        // Read a block of unaligned bytes in memory.
        // @return an array of byte values
        public virtual List<byte> readBlockMemoryUnaligned8(UInt32 addr, UInt32 size)
        {
            List<byte> res = new List<byte>();
            // try to read 8bits data
            if (size > 0 && ((addr & 1) != 0))
            {
                byte mem = (byte)this.readMemory(addr, 8)();
                res.Add(mem);
                size -= 1;
                addr += 1;
            }
            // try to read 16bits data
            if (size > 1 && ((addr & 2) != 0))
            {
                UInt16 mem = (UInt16)this.readMemory(addr, 16)();
                res.Add((byte)(mem & 0xFF));
                res.Add((byte)((mem >> 8) & 0xFF));
                size -= 2;
                addr += 2;
            }
            // try to read aligned block of 32bits
            if (size >= 4)
            {
                List<UInt32> mem = this.readBlockMemoryAligned32(addr, size / 4);
                res.AddRange(Utility.Conversion.u32leListToByteList(mem));
                size -= (UInt32)(4 * mem.Count);
                addr += (UInt32)(4 * mem.Count);
            }
            if (size > 1)
            {
                UInt16 mem = (UInt16)this.readMemory(addr, 16)();
                res.Add((byte)(mem & 0xFF));
                res.Add((byte)((mem >> 8) & 0xFF));
                size -= 2;
                addr += 2;
            }
            if (size > 0)
            {
                byte mem = (byte)this.readMemory(addr, 8)();
                res.Add(mem);
                size -= 1;
                addr += 1;
            }
            Debug.Assert(size == 0);
            return res;
        }

        // Write a block of unaligned bytes in memory.
        public virtual void writeBlockMemoryUnaligned8(UInt32 addr, List<byte> data)
        {
            UInt32 size = (UInt32)data.Count;
            UInt32 idx = 0;
            //try to write 8 bits data
            if (size > 0 && ((addr & 1) != 0))
            {
                this.writeMemory(addr, data[(int)idx], 8);
                size -= 1;
                addr += 1;
                idx += 1;
            }
            // try to write 16 bits data
            if (size > 1 && ((addr & 2) != 0))
            {
                this.writeMemory(addr, (UInt16)(data[(int)idx] | data[(int)idx + 1] << 8), 16);
                size -= 2;
                addr += 2;
                idx += 2;
            }
            // write aligned block of 32 bits
            if (size >= 4)
            {
                var data32 = Utility.Conversion.byteListToU32leList(data.GetRange((int)idx, (int)(size & ~0x03)));
                this.writeBlockMemoryAligned32(addr, data32);
                addr += (UInt32)(size & ~0x03);
                idx += (UInt32)(size & ~0x03);
                size -= (UInt32)(size & ~0x03);
            }
            // try to write 16 bits data
            if (size > 1)
            {
                this.writeMemory(addr, (UInt16)(data[(int)idx] | data[(int)idx + 1] << 8), 16);
                size -= 2;
                addr += 2;
                idx += 2;
            }
            //try to write 8 bits data
            if (size > 0)
            {
                this.writeMemory(addr, data[(int)idx], 8);
                size -= 1;
                addr += 1;
                idx += 1;
            }
            return;
        }

        // Write a block of aligned words in memory.
        public virtual void writeBlockMemoryAligned32(UInt32 addr, List<UInt32> data)
        {
            UInt32 size = (UInt32)data.Count;
            while (size > 0)
            {
                UInt32 n = this.auto_increment_page_size - (addr & (this.auto_increment_page_size - 1));
                if (size * 4 < n)
                {
                    n = (size * 4) & 0xfffffffc;
                }
                this._writeBlock32(addr, data.GetRange(0, (int)n / 4)); // VK: Floor division
                data = data.GetRange((int)n / 4, data.Count - (int)n / 4);
                size -= n / 4;
                addr += n;
            }
            return;
        }

        // Read a block of aligned words in memory.
        //
        // @return An array of word values
        public virtual List<UInt32> readBlockMemoryAligned32(UInt32 addr, UInt32 size)
        {
            List<UInt32> resp = new List<UInt32>();
            while (size > 0)
            {
                UInt32 n = this.auto_increment_page_size - (addr & (this.auto_increment_page_size - 1));
                if (size * 4 < n)
                {
                    n = (size * 4) & 0xfffffffc;
                }
                resp.AddRange(this._readBlock32(addr, (UInt16)(n / 4)));
                size -= n / 4;
                addr += n;
            }
            return resp;
        }

        public virtual void _handle_error(Exception error, int num)
        {
            this.dp._handle_error(error, num);
        }
    }

    public class AHB_AP : MEM_AP
    {

        public AHB_AP(DebugAccessPort dp, UInt32 ap_num) : base(dp, ap_num)
        {
        }

        public override void init_rom_table()
        {
            // Turn on DEMCR.TRCENA before reading the ROM table. Some ROM table entries will
            // come back as garbage if TRCENA is not set.
            try
            {
                var demcr = this.read32(DEMCR)();
                this.write32(DEMCR, demcr | DEMCR_TRCENA);
                this.dp.flush();
            }
            catch
            {
                // Ignore exception and read whatever we can of the ROM table.
            }
            base.init_rom_table();
        }
    }
}
