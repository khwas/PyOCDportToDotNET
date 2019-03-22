using openocd.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace openocd.Debugger.Breakpoints
{
    public static class Provider
    {

        public class Breakpoint
        {
            internal EBreakpointType type;
            internal bool enabled;
            public UInt32 addr;
            internal byte func;
            internal UInt16 original_instr;
            internal BreakpointProvider provider;
            public Breakpoint(BreakpointProvider provider)
            {
                this.type = EBreakpointType.BREAKPOINT_HW;
                this.enabled = false;
                this.addr = 0;
                this.original_instr = 0;
                this.provider = provider;
            }

            public virtual object @__repr__()
            {
                return String.Format("<{0}@0x%{1:X8} type={2} addr=0x{3:X8}>", this.GetType().Name, "?", //id(this), 
                    this.type, this.addr);
            }
        }

        public interface IBreakpointProvider
        {
            void init();
            EBreakpointType bp_type();
            bool do_filter_memory { get; }
            int available_breakpoints();
            Breakpoint find_breakpoint(UInt32 addr);
            Breakpoint set_breakpoint(UInt32 addr);
            void remove_breakpoint(Breakpoint bp);
            object filter_memory(UInt32 addr, byte size, object data);
            void flush();
        }

        public abstract class BreakpointProvider: IBreakpointProvider
        {

            public abstract void init();

            public abstract EBreakpointType bp_type();

            public virtual bool do_filter_memory
            {
                get
                {
                    return false;
                }
            }

            public abstract int available_breakpoints();

            public abstract Breakpoint find_breakpoint(UInt32 addr);

            public abstract Breakpoint set_breakpoint(UInt32 addr);

            public abstract void remove_breakpoint(Breakpoint bp);

            public virtual object filter_memory(UInt32 addr, byte size, object data)
            {
                return data;
            }

            public virtual void flush()
            {
            }
        }
    }

}
