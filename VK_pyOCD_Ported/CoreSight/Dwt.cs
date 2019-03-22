using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using openocd.Core;
using openocd.Debugger.Breakpoints;

namespace openocd.CoreSight
{
    /// <summary>
    /// Data Watchpoint and Trace (DWT) unit
    /// </summary>

    public class Watchpoint
        : HardwareBreakpoint
    {

        internal byte size;
        public Watchpoint(UInt32 comp_register_addr, DWT provider)
            : base(comp_register_addr, provider)
        {
            this.addr = 0;
            this.size = 0;
            this.func = 0;
        }
    }

    public class DWT : Provider.BreakpointProvider
    {
        // Need a local copy to prevent circular import.
        // Debug Exception and Monitor Control Register
        public const UInt32 DEMCR = 0xE000EDFC;
        // DWTENA in armv6 architecture reference manual
        public const UInt32 DEMCR_TRCENA = (1 << 24);
        public const UInt32 DEMCR_VC_HARDERR = (1 << 10);
        public const UInt32 DEMCR_VC_BUSERR = (1 << 8);
        public const UInt32 DEMCR_VC_CORERESET = (1 << 0);


        internal MEM_AP ap;
        internal List<Watchpoint> watchpoints;
        internal UInt32 watchpoint_used;
        internal bool dwt_configured;

        //  DWT (data watchpoint & trace)
        public const UInt32 DWT_CTRL = 0xE0001000;
        public const UInt32 DWT_COMP_BASE = 0xE0001020;
        public const byte DWT_MASK_OFFSET = 4;
        public const byte DWT_FUNCTION_OFFSET = 8;
        public const byte DWT_COMP_BLOCK_SIZE = 0x10;

        public static Dictionary<byte, byte> WATCH_TYPE_TO_FUNCT = new Dictionary<byte, byte>()
            {
                {Target.WATCHPOINT_READ,  5},
                {Target.WATCHPOINT_WRITE, 6},
                {Target.WATCHPOINT_READ_WRITE, 7}
            };

        public static Dictionary<byte, UInt32> WATCH_SIZE_TO_MASK = new Dictionary<byte, uint>()
            // Enumerable.Range(0, 32).Select(i => Tuple.Create(i, (UInt32)Math.Pow(2, i))).ToDictionary();
            {
                {  0, 1 <<  0 },
                {  1, 1 <<  1 },
                {  2, 1 <<  2 },
                {  3, 1 <<  3 },
                {  4, 1 <<  4 },
                {  5, 1 <<  5 },
                {  6, 1 <<  6 },
                {  7, 1 <<  7 },
                {  8, 1 <<  8 },
                {  9, 1 <<  9 },
                { 10, 1 << 10 },
                { 11, 1 << 11 },
                { 12, 1 << 12 },
                { 13, 1 << 13 },
                { 14, 1 << 14 },
                { 15, 1 << 15 },
                { 16, 1 << 16 },
                { 17, 1 << 17 },
                { 18, 1 << 18 },
                { 19, 1 << 19 },
                { 20, 1 << 20 },
                { 21, 1 << 21 },
                { 22, 1 << 22 },
                { 23, 1 << 23 },
                { 24, 1 << 24 },
                { 25, 1 << 25 },
                { 26, 1 << 26 },
                { 27, 1 << 27 },
                { 28, 1 << 28 },
                { 29, 1 << 29 },
                { 30, 1 << 30 },
                { 31, (UInt32)1 << 31 },
            };

        public DWT(MEM_AP ap)
        {
            this.ap = ap;
            this.watchpoints = new List<Watchpoint>();
            this.watchpoint_used = 0;
            this.dwt_configured = false;
        }

        // Inits the DWT.
        //
        // Reads the number of hardware watchpoints available on the core  and makes sure that they
        // are all disabled and ready for future use.
        public override void init()
        {
            var demcr = this.ap.readMemory(DEMCR)();
            demcr = demcr | DEMCR_TRCENA;
            this.ap.writeMemory(DEMCR, demcr);
            var dwt_ctrl = this.ap.readMemory(DWT.DWT_CTRL)();
            var watchpoint_count = dwt_ctrl >> 28 & 15;
            Trace.TraceInformation("{0} hardware watchpoints", watchpoint_count);
            for (UInt32 i = 0; i < watchpoint_count; i++)
            {
                this.watchpoints.Add(new Watchpoint(DWT.DWT_COMP_BASE + DWT.DWT_COMP_BLOCK_SIZE * i, this));
                this.ap.writeMemory(DWT.DWT_COMP_BASE + DWT.DWT_COMP_BLOCK_SIZE * i + DWT.DWT_FUNCTION_OFFSET, 0);
            }
            this.dwt_configured = true;
        }

        public virtual Watchpoint find_watchpoint(UInt32 addr, byte size, byte type)
        {
            foreach (var watch in this.watchpoints)
            {
                if (watch.addr == addr && watch.size == size && watch.func == DWT.WATCH_TYPE_TO_FUNCT[type])
                {
                    return watch;
                }
            }
            return null;
        }

        // Set a hardware watchpoint.
        public virtual bool set_watchpoint(UInt32 addr, byte size, byte type)
        {
            if (this.dwt_configured == false)
            {
                this.init();
            }
            if (this.find_watchpoint(addr, size, type) != null)
            {
                return true;
            }
            if (!DWT.WATCH_TYPE_TO_FUNCT.ContainsKey(type))
            {
                Trace.TraceError("Invalid watchpoint type %i", type);
                return false;
            }
            foreach (var watch in this.watchpoints)
            {
                if (watch.func == 0)
                {
                    watch.addr = addr;
                    watch.func = DWT.WATCH_TYPE_TO_FUNCT[type];
                    watch.size = size;
                    if (!DWT.WATCH_SIZE_TO_MASK.ContainsKey(size))
                    {
                        Trace.TraceError("Watchpoint of size %d not supported by device", size);
                        return false;
                    }
                    var mask = DWT.WATCH_SIZE_TO_MASK[size];
                    this.ap.writeMemory(watch.comp_register_addr + DWT.DWT_MASK_OFFSET, mask);
                    if (this.ap.readMemory(watch.comp_register_addr + DWT.DWT_MASK_OFFSET)() != mask)
                    {
                        Trace.TraceError("Watchpoint of size %d not supported by device", size);
                        return false;
                    }
                    this.ap.writeMemory(watch.comp_register_addr, addr);
                    this.ap.writeMemory(watch.comp_register_addr + DWT.DWT_FUNCTION_OFFSET, watch.func);
                    this.watchpoint_used += 1;
                    return true;
                }
            }
            Trace.TraceError("No more available watchpoint!!, dropped watch at 0x%X", addr);
            return false;
        }

        // Remove a hardware watchpoint.
        public virtual void remove_watchpoint(UInt32 addr, byte size, byte type)
        {
            Watchpoint watch = this.find_watchpoint(addr, size, type);
            if (watch == null)
            {
                return;
            }
            watch.func = 0;
            this.ap.writeMemory(watch.comp_register_addr + DWT.DWT_FUNCTION_OFFSET, 0);
            this.watchpoint_used -= 1;
        }

        public override Provider.Breakpoint find_breakpoint(UInt32 addr)
        {
            return this.watchpoints.FirstOrDefault(wp => wp.addr == addr);
        }

        public override int available_breakpoints()
        {
            return this.watchpoints.Count();
        }

        public override void remove_breakpoint(Provider.Breakpoint bp)
        {
            throw new NotImplementedException();
        }

        public override Provider.Breakpoint set_breakpoint(UInt32 addr)
        {
            throw new NotImplementedException();
        }

        public override EBreakpointType bp_type()
        {
            throw new NotImplementedException();
        }
    }
}

