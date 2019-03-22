using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//using Target = core.target.Target;

using openocd.Debugger.Breakpoints;

using System.Diagnostics;
using openocd.Core;
//using static coresight.ap;

namespace openocd.CoreSight
{

    public class HardwareBreakpoint
        : Provider.Breakpoint
    {

        internal readonly UInt32 comp_register_addr;

        public HardwareBreakpoint(UInt32 comp_register_addr, Provider.BreakpointProvider provider) : base(provider)
        {
            this.comp_register_addr = comp_register_addr;
            this.type = EBreakpointType.BREAKPOINT_HW;
        }
    }

    /// <summary>
    /// Flash Patch and Breakpoint Unit (FPB)
    /// </summary>
    public class FPB : Provider.BreakpointProvider
    {

        public const UInt32 FP_CTRL = 0xE0002000;
        public const byte FP_CTRL_KEY = 1 << 1;
        public const UInt32 FP_COMP0 = 0xE0002008;

        internal List<HardwareBreakpoint> hw_breakpoints;
        internal bool enabled;
        internal MEM_AP ap;
        internal int num_hw_breakpoint_used;
        internal byte nb_code;
        internal byte nb_lit;

        public FPB(MEM_AP ap)
        {
            this.ap = ap;
            this.hw_breakpoints = new List<HardwareBreakpoint>();
            this.nb_code = 0;
            this.nb_lit = 0;
            this.num_hw_breakpoint_used = 0;
            this.enabled = false;
        }

        // Inits the FPB.
        //
        // Reads the number of hardware breakpoints available on the core and disable the FPB
        // (Flash Patch and Breakpoint Unit), which will be enabled when the first breakpoint is set.
        public override void init()
        {
            // setup FPB (breakpoint)
            UInt32 fpcr = this.ap.readMemory(FPB.FP_CTRL)();
            this.nb_code = (byte)(((fpcr >> 8) & 0x70) | ((fpcr >> 4) & 0xF));
            this.nb_lit = (byte)((fpcr >> 7) & 0xf);
            Trace.TraceInformation("{0} hardware breakpoints, {1} literal comparators", this.nb_code, this.nb_lit);
            foreach (var i in Enumerable.Range(0, this.nb_code))
            {
                this.hw_breakpoints.Add(new HardwareBreakpoint((UInt32)(FPB.FP_COMP0 + 4 * i), this));
            }
            // disable FPB (will be enabled on first bp set)
            this.disable();
            foreach (var bp in this.hw_breakpoints)
            {
                this.ap.writeMemory(bp.comp_register_addr, 0);
            }
        }

        public override EBreakpointType bp_type()
        {
            return EBreakpointType.BREAKPOINT_HW;
        }

        public virtual void enable()
        {
            this.ap.writeMemory(FPB.FP_CTRL, FPB.FP_CTRL_KEY | 1);
            this.enabled = true;
            Trace.TraceInformation("fpb has been enabled");
            return;
        }

        public virtual void disable()
        {
            this.ap.writeMemory(FPB.FP_CTRL, FPB.FP_CTRL_KEY | 0);
            this.enabled = false;
            Trace.TraceInformation("fpb has been disabled");
            return;
        }

        public override int available_breakpoints()
        {
            return this.hw_breakpoints.Count - this.num_hw_breakpoint_used;
        }

        public override Provider.Breakpoint find_breakpoint(UInt32 addr)
        {
            return this.hw_breakpoints.FirstOrDefault(bp => bp.addr == addr);
        }

        // Set a hardware breakpoint at a specific location in flash.
        public override Provider.Breakpoint set_breakpoint(UInt32 addr)
        {
            if (!this.enabled)
            {
                this.enable();
            }
            if (addr >= 0x20000000)
            {
                // Hardware breakpoints are only supported in the range
                // 0x00000000 - 0x1fffffff on cortex-m devices
                Trace.TraceError("Breakpoint out of range 0x{0:X}", addr);
                return null;
            }
            if (this.available_breakpoints() == 0)
            {
                Trace.TraceError("No more available breakpoint!!, dropped bp at 0x{0:X}", addr);
                return null;
            }
            foreach (HardwareBreakpoint bp in this.hw_breakpoints)
            {
                if (!bp.enabled)
                {
                    bp.enabled = true;
                    UInt32 bp_match = 1 << 30;
                    if ((addr & 2) != 0)
                    {
                        bp_match = (UInt32)2 << 30;
                    }
                    this.ap.writeMemory(bp.comp_register_addr, addr & 0x1ffffffc | bp_match | 1);
                    bp.addr = addr;
                    this.num_hw_breakpoint_used += 1;
                    return bp;
                }
            }
            return null;
        }

        // Remove a hardware breakpoint at a specific location in flash.
        public override void remove_breakpoint(Provider.Breakpoint bp)
        {
            foreach (var hwbp in this.hw_breakpoints)
            {
                if (hwbp.enabled && hwbp.addr == bp.addr)
                {
                    hwbp.enabled = false;
                    this.ap.writeMemory(hwbp.comp_register_addr, 0);
                    this.num_hw_breakpoint_used -= 1;
                    return;
                }
            }
        }
    }
}
