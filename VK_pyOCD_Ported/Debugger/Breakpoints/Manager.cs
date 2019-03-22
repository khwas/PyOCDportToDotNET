using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using openocd.Core;
//using coresight;

namespace openocd.Debugger.Breakpoints
{

    public class BreakpointManager
    {

        public const byte MIN_HW_BREAKPOINTS = 0;

        private Core.Target _core;
        private Provider.BreakpointProvider _fpb;
        private Dictionary<UInt32, Provider.Breakpoint> _breakpoints;
        private Dictionary<EBreakpointType, Provider.BreakpointProvider> _providers;

        public BreakpointManager(Core.Target core)
        {
            this._breakpoints = new Dictionary<UInt32, Provider.Breakpoint>
            {
            };
            this._core = core;
            this._fpb = null;
            this._providers = new Dictionary<EBreakpointType, Provider.BreakpointProvider>
            {
            };
        }

        public virtual void add_provider(Provider.BreakpointProvider provider, EBreakpointType type)
        {
            this._providers[type] = provider;
            if (type == EBreakpointType.BREAKPOINT_HW)
            {
                this._fpb = provider;
            }
        }

        public virtual Provider.Breakpoint find_breakpoint(UInt32 addr)
        {
            return this._breakpoints.ContainsKey(addr) ? this._breakpoints[addr] : null;
        }

        // Set a hardware or software breakpoint at a specific location in memory.
        //
        // @retval True Breakpoint was set.
        // @retval False Breakpoint could not be set.
        public virtual bool set_breakpoint(UInt32 addr, EBreakpointType type = EBreakpointType.BREAKPOINT_AUTO)
        {
            Trace.TraceInformation("set bkpt type {0} at 0x{1:x}", type, addr);
            // Clear Thumb bit in case it is set.
            addr = (UInt32)(addr & ~1);
            var in_hw_bkpt_range = addr < 0x20000000;
            var fbp_available = this._fpb != null && this._fpb.available_breakpoints() > 0;
            var fbp_below_min = this._fpb == null || this._fpb.available_breakpoints() <= MIN_HW_BREAKPOINTS;
            // Check for an existing breakpoint at this address.
            Provider.Breakpoint bp = this.find_breakpoint(addr);
            if (bp != null)
            {
                return true;
            }
            bool is_flash;
            bool is_ram;
            if (this._core.memory_map == null)
            {
                // No memory map - fallback to hardware breakpoints.
                type = EBreakpointType.BREAKPOINT_HW;
                is_flash = false;
                is_ram = false;
            }
            else
            {
                // Look up the memory type for the requested address.
                Core.Memory.MemoryRegion region = this._core.memory_map.getRegionForAddress(addr);
                if (region != null)
                {
                    is_flash = region.isFlash;
                    is_ram = region.isRam;
                }
                else
                {
                    // No memory region - fallback to hardware breakpoints.
                    type = EBreakpointType.BREAKPOINT_HW;
                    is_flash = false;
                    is_ram = false;
                }
            }
            // Determine best type to use if auto.
            if (type == EBreakpointType.BREAKPOINT_AUTO)
            {
                // Use sw breaks for:
                //  1. Addresses outside the supported FPBv1 range of 0-0x1fffffff
                //  2. RAM regions by default.
                //  3. Number of remaining hw breaks are at or less than the minimum we want to keep.
                //
                // Otherwise use hw.
                if ((!in_hw_bkpt_range) || is_ram || fbp_below_min)
                {
                    type = EBreakpointType.BREAKPOINT_SW;
                }
                else
                {
                    type = EBreakpointType.BREAKPOINT_HW;
                }
                Trace.TraceInformation("using type {0} for auto bp", type);
            }
            // Revert to sw bp if out of hardware breakpoint range.
            if (type == EBreakpointType.BREAKPOINT_HW && !in_hw_bkpt_range)
            {
                if (is_ram)
                {
                    Trace.TraceInformation("using sw bp instead because of unsupported addr");
                    type = EBreakpointType.BREAKPOINT_SW;
                }
                else
                {
                    Trace.TraceInformation("could not fallback to software breakpoint");
                    return false;
                }
            }
            // Revert to hw bp if region is flash.
            if (is_flash)
            {
                if (in_hw_bkpt_range && fbp_available)
                {
                    Trace.TraceInformation("using hw bp instead because addr is flash");
                    type = EBreakpointType.BREAKPOINT_HW;
                }
                else
                {
                    Trace.TraceInformation("could not fallback to hardware breakpoint");
                    return false;
                }
            }
            // Set the bp.
            try
            {
                var provider = this._providers[type];
                bp = provider.set_breakpoint(addr);
            }
            catch (KeyNotFoundException)
            {
                throw new Exception(String.Format("Unknown breakpoint type {0}", type));
            }
            if (bp == null)
            {
                return false;
            }
            // Save the bp.
            this._breakpoints[addr] = bp;
            return true;
        }

        // Remove a breakpoint at a specific location.
        public virtual void remove_breakpoint(UInt32 addr)
        {
            try
            {
                Trace.TraceInformation("remove bkpt at 0x{0:x}", addr);
                // Clear Thumb bit in case it is set.
                addr = (UInt32)(addr & ~1);
                // Get bp and remove from dict.
                Provider.Breakpoint bp = this._breakpoints[addr];
                this._breakpoints.Remove(addr);
                Debug.Assert(bp.provider != null);
                bp.provider.remove_breakpoint(bp);
            }
            catch (KeyNotFoundException)
            {
                Trace.TraceInformation(String.Format("Tried to remove breakpoint 0x{0:X08} that wasn't set", addr));
            }
        }

        public virtual byte? get_breakpoint_type(UInt32 addr)
        {
            Provider.Breakpoint bp = this.find_breakpoint(addr);
            return bp != null ? (byte?)bp.type : null;
        }

        public virtual object filter_memory(UInt32 addr, byte transfer_size, object data)
        {
            foreach (var provider in this._providers.Values.Where(p => p.do_filter_memory))
            {
                data = provider.filter_memory(addr, transfer_size, data);
            }
            return data;
        }

        public virtual List<byte> filter_memory_unaligned_8(UInt32 addr, UInt32 size, List<byte> data)
        {
            Debug.Assert(size == data.Count);
            byte[] result = new byte[size];
            foreach (var provider in this._providers.Values.Where(p => p.do_filter_memory))
            {
                foreach (var _tup_1 in data.Select((_p_1, _p_2) => Tuple.Create(_p_2, _p_1)))
                {
                    var i = _tup_1.Item1;
                    var d = _tup_1.Item2;
                    result[i] = (byte)provider.filter_memory((UInt32)(addr + i), 8, d);
                }
            }
            return result.ToList();
        }

        public virtual List<UInt32> filter_memory_aligned_32(UInt32 addr, UInt32 size, List<UInt32> data)
        {
            Debug.Assert(size == data.Count);
            UInt32[] result = new UInt32[size];
            foreach (var provider in this._providers.Values.Where(p => p.do_filter_memory))
            {
                foreach (var _tup_1 in data.Select((_p_1, _p_2) => Tuple.Create(_p_2, _p_1)))
                {
                    var i = _tup_1.Item1;
                    var d = _tup_1.Item2;
                    result[i] = (UInt32)provider.filter_memory((UInt32)(addr + i), 32, d);
                }
            }
            return result.ToList();
        }

        public virtual void remove_all_breakpoints()
        {
            foreach (Provider.Breakpoint bp in this._breakpoints.Values)
            {
                bp.provider.remove_breakpoint(bp);
            }
            this._breakpoints.Clear();
            this._flush_all();
        }

        public virtual void _flush_all()
        {
            // Flush all providers.
            foreach (var provider in this._providers.Values)
            {
                provider.flush();
            }
        }

        public virtual void flush()
        {
            try
            {
                // Flush all providers.
                this._flush_all();
            }
            catch
            {
            }
        }
    }
}
