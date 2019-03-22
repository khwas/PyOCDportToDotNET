using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Diagnostics;
using openocd.Core;

namespace openocd.Debugger.Breakpoints
{

    public class SoftwareBreakpoint
        : Provider.Breakpoint
    {

        public SoftwareBreakpoint(SoftwareBreakpointProvider provider) : base(provider)
        {
            this.type = EBreakpointType.BREAKPOINT_SW;
        }
    }

    public class SoftwareBreakpointProvider
        : Provider.BreakpointProvider
    {

        // BKPT #0 instruction.
        UInt16 BKPT_INSTR = 0xbe00;

        internal readonly Target _core;
        internal readonly Dictionary<UInt32, SoftwareBreakpoint> _breakpoints;

        public SoftwareBreakpointProvider(Target core)
        {
            this._core = core;
            this._breakpoints = new Dictionary<UInt32, SoftwareBreakpoint>()
            {
            };
        }

        public override void init()
        {
        }

        public override EBreakpointType bp_type()
        {
            return EBreakpointType.BREAKPOINT_SW;
        }

        public override bool do_filter_memory
        {
            get
            {
                return true;
            }
        }

        public override int available_breakpoints()
        {
            return -1;
        }

        public override Provider.Breakpoint find_breakpoint(uint addr)
        {
            return this._breakpoints.Values.FirstOrDefault(bp => bp.addr == addr);
        }

        public override Provider.Breakpoint set_breakpoint(UInt32 addr)
        {
            Debug.Assert(this._core.memory_map.getRegionForAddress(addr).isRam);
            Debug.Assert((addr & 1) == 0);
            try
            {
                // Read original instruction.
                UInt16 instr = this._core.read16(addr)();
                // Insert BKPT #0 instruction.
                this._core.write16(addr, this.BKPT_INSTR);
                // Create bp object.
                var bp = new SoftwareBreakpoint(this)
                {
                    enabled = true,
                    addr = addr,
                    original_instr = instr
                };
                // Save this breakpoint.
                this._breakpoints[addr] = bp;
                return bp;
            }
            catch
            {
                Trace.TraceInformation(String.Format("Failed to set sw bp at 0x{0:X08}", addr));
                return null;
            }
        }

        public override void remove_breakpoint(Provider.Breakpoint bp)
        {
            Debug.Assert(bp != null && bp is Provider.Breakpoint);
            try
            {
                // Restore original instruction.
                this._core.write16(bp.addr, bp.original_instr);
                // Remove from our list.
                this._breakpoints.Remove(bp.addr);
            }
            catch
            {
                Trace.TraceInformation(String.Format("Failed to remove sw bp at 0x{0:X08}", bp.addr));
            }
        }

        public override object filter_memory(UInt32 addr, byte size, object data)
        {
            foreach (var bp in this._breakpoints.Values)
            {
                if (size == 8)
                {
                    Debug.Assert(data is Byte);
                    if (bp.addr == addr)
                    {
                        data = bp.original_instr & 0xFF;
                    }
                    else if (bp.addr + 1 == addr)
                    {
                        data = bp.original_instr >> 8;
                    }
                }
                else if (size == 16)
                {
                    Debug.Assert(data is UInt16);
                    if (bp.addr == addr)
                    {
                        data = bp.original_instr;
                    }
                }
                else if (size == 32)
                {
                    Debug.Assert(data is UInt32);
                    if (bp.addr == addr)
                    {
                        data = ((UInt32)data & 0xffff0000) | (UInt32)bp.original_instr << 0;
                    }
                    else if (bp.addr == addr + 2)
                    {
                        data = ((UInt32)data & 0x0000ffff) | ((UInt32)bp.original_instr << 16);
                    }
                }
            }
            return data;
        }
    }
}
