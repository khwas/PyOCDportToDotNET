using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// using Target = target.Target;
// 
// using dap = coresight.dap;
// 
// using ap = coresight.ap;
// 
// using cortex_m = coresight.cortex_m;

//using SVDFile = debug.svd.SVDFile;

//using debug.svd.SVDLoader;

//using debug;

//using CachingDebugContext = debug.cache.CachingDebugContext;

//using threading;

//using logging;

//using Element = xml.etree.ElementTree.Element;

//using SubElement = xml.etree.ElementTree.SubElement;

//using tostring = xml.etree.ElementTree.tostring;

//using static coresight.dap;
//using pyDAPAccess;
using System.Diagnostics;
using openocd.CoreSight;
using openocd.CmsisDap;

namespace openocd.Core
{

    public class CoreSightTarget: Target, ITarget
    {

        //internal DapAccess.IDapAccessLink link;
        private CoreSight.DebugAccessPort dp;
        private Dictionary<byte, CortexM> cores;
        private Dictionary<UInt32, MEM_AP> aps;
        //private string part_number;
        private byte _selected_core;
        Dictionary<object, object> _root_contexts;
        //internal svd.SVDLoader _svd_load_thread;
        //internal string _svd_location;
        //internal cmsis_svd.device _svd_device;
        //internal Memory.MemoryMap memory_map;
        //internal Flash.Flash flash;

        public CoreSightTarget(IDapAccessLink link, Memory.MemoryMap memoryMap = null): base(link, memoryMap)
        {
            this.part_number = this.GetType().Name;
            this.cores = new Dictionary<byte, CortexM>()
            {
            };
            this.aps = new Dictionary<UInt32, MEM_AP>()
            {
            };
            this.link = link;
            this.dp = new DebugAccessPort(link);
            this._selected_core = 0;
            //this._svd_load_thread = null;
            this._root_contexts = new Dictionary<object, object>()
            {
            };
        }

        public ITarget selected_core
        {
            get
            {
                return this.cores[this._selected_core];
            }
        }

        public virtual void select_core(byte num)
        {
            if (!this.cores.ContainsKey(num))
            {
                throw new ArgumentOutOfRangeException("invalid core number");
            }
            Trace.TraceInformation(String.Format("selected core #{0}", num));
            this._selected_core = num;
        }

        public override cmsis_svd.device svd_device
        {
            get
            {
                //if ((this._svd_device == null) && (this._svd_load_thread != null))
                //{
                //    Trace.TraceInformation("Waiting for SVD load to complete");
                //    this._svd_device = this._svd_load_thread.device;
                //}
                return this._svd_device;
            }
        }

        public virtual void loadSVD()
        {
            throw new NotImplementedException();
            //void svdLoadCompleted(cmsis_svd.device svdDevice)
            //{
            //    Trace.TraceInformation("Completed loading SVD");
            //    this._svd_device = svdDevice;
            //    this._svd_load_thread = null;
            //}
            //if ((this._svd_device == null) && (this._svd_location != null))
            //{
            //    Trace.TraceInformation("Started loading SVD");
            //    // Spawn thread to load SVD in background.
            //    this._svd_load_thread = new svd.SVDLoader(this._svd_location, svdLoadCompleted);
            //    this._svd_load_thread.load();
            //}
        }

        public virtual void add_ap(MEM_AP ap)
        {
            this.aps[ap.ap_num] = ap;
        }

        public virtual void add_core(CortexM core)
        {
            this.cores[core.core_number] = core;
            ///this.cores[core.core_number].setTargetContext(new CachingDebugContext(core, new Debugger.DebugContext(core)));
            this.cores[core.core_number].setTargetContext(new Debugger.DebugContext(core));
            this._root_contexts[core.core_number] = null;
        }

        public override void init(bool bus_accessible = true)
        {
            // Start loading the SVD file
            ///this.loadSVD();
            // Create the DP and turn on debug.
            this.dp.init();
            this.dp.power_up_debug();
            // Create an AHB-AP for the CPU.
            var ap0 = new AHB_AP(this.dp, 0);
            ap0.init(bus_accessible);
            this.add_ap(ap0);
            // Create CortexM core.
            CortexM core0 = new CortexM(this.link, this.dp, this.aps[0], this.memory_map);
            if (bus_accessible)
            {
                core0.init();
            }
            this.add_core(core0);
        }

        public override void disconnect()
        {
            foreach (var core in this.cores.Values)
            {
                core.disconnect();
            }
            this.dp.power_down_debug();
        }

        public override void setAutoUnlock(object doAutoUnlock) => throw new NotImplementedException();
        public override object info(object request) => throw new NotImplementedException();

        public override UInt32 readIDCode()
        {
            return this.dp.dpidr;
        }

        public override int run_token
        {
            get
            {
                return this.selected_core.run_token;
            }
        }

        public override void flush()
        {
            this.dp.flush();
        }

        public override void halt()
        {
            this.selected_core.halt();
        }

        public override void step(bool disable_interrupts = true)
        {
            this.selected_core.step(disable_interrupts);
        }

        public override void resume()
        {
            this.selected_core.resume();
        }

        public override bool massErase()
        {
            if (this.flash != null)
            {
                this.flash.init();
                this.flash.eraseAll();
                return true;
            }
            else
            {
                return false;
            }
        }

        public override void writeMemory(UInt32 addr, UInt32 value, byte transfer_size = 32)
        {
            this.selected_core.writeMemory(addr, value, transfer_size);
        }

        public override Func<UInt32> readMemory(UInt32 addr, byte transfer_size = 32, bool now = true)
        {
            return this.selected_core.readMemory(addr, transfer_size, now);
        }

        public override void writeBlockMemoryUnaligned8(UInt32 addr, List<byte> data)
        {
            this.selected_core.writeBlockMemoryUnaligned8(addr, data);
        }

        public override void writeBlockMemoryAligned32(UInt32 addr, List<UInt32> data)
        {
            this.selected_core.writeBlockMemoryAligned32(addr, data);
        }

        public override List<byte> readBlockMemoryUnaligned8(UInt32 addr, UInt32 size)
        {
            return this.selected_core.readBlockMemoryUnaligned8(addr, size);
        }

        public override List<UInt32> readBlockMemoryAligned32(UInt32 addr, UInt32 size)
        {
            return this.selected_core.readBlockMemoryAligned32(addr, size);
        }

        public override UInt32 readCoreRegister(string id)
        {
            return this.selected_core.readCoreRegister(id);
        }

        public override void writeCoreRegister(string id, UInt32 data)
        {
            this.selected_core.writeCoreRegister(id, data);
        }

        public override UInt32 readCoreRegisterRaw(string reg)
        {
            return this.selected_core.readCoreRegisterRaw(reg);
        }

        public override List<UInt32> readCoreRegistersRaw(List<string> reg_list)
        {
            return this.selected_core.readCoreRegistersRaw(reg_list);
        }

        public override void writeCoreRegisterRaw(string reg, UInt32 data)
        {
            this.selected_core.writeCoreRegisterRaw(reg, data);
        }

        public override void writeCoreRegistersRaw(List<string> reg_list_s, List<UInt32> data_list)
        {
            this.selected_core.writeCoreRegistersRaw(reg_list_s, data_list);
        }

        public override Debugger.Breakpoints.Provider.Breakpoint findBreakpoint(UInt32 addr)
        {
            return this.selected_core.findBreakpoint(addr);
        }

        public override bool setBreakpoint(UInt32 addr, EBreakpointType type = EBreakpointType.BREAKPOINT_AUTO)
        {
            return this.selected_core.setBreakpoint(addr, type);
        }

        public override byte getBreakpointType(UInt32 addr)
        {
            return this.selected_core.getBreakpointType(addr);
        }

        public override void removeBreakpoint(UInt32 addr)
        {
            this.selected_core.removeBreakpoint(addr);
        }

        public override bool setWatchpoint(UInt32 addr, byte size, byte type)
        {
            return this.selected_core.setWatchpoint(addr, size, type);
        }

        public override void removeWatchpoint(UInt32 addr, byte size, byte type)
        {
            this.selected_core.removeWatchpoint(addr, size, type);
        }

        public override void reset(bool? software_reset = null)
        {
            this.selected_core.reset(software_reset);
        }

        public override void resetStopOnReset(bool? software_reset = null)
        {
            this.selected_core.resetStopOnReset(software_reset);
        }

        public override void setTargetState(ETargetState state)
        {
            this.selected_core.setTargetState(state);
        }

        public override ETargetState getState()
        {
            return this.selected_core.getState();
        }

        public override void setVectorCatch(UInt32 enableMask)
        {
            //return 
            this.selected_core.setVectorCatch(enableMask);
        }

        public override UInt32 getVectorCatch()
        {
            return this.selected_core.getVectorCatch();
        }

        // GDB functions
        public override string getTargetXML()
        {
            return this.selected_core.getTargetXML();
        }

        public override object getTargetContext(byte? core = null)
        {
            if (core == null)
            {
                core = this._selected_core;
            }
            return this.cores[(byte)core].getTargetContext();
        }

        public override object getRootContext(object core = null)
        {
            if (core == null)
            {
                core = this._selected_core;
            }
            if (this._root_contexts[core] == null)
            {
                return this.getTargetContext();
            }
            else
            {
                return this._root_contexts[core];
            }
        }

        public override void setRootContext(object context, object core = null)
        {
            if (core == null)
            {
                core = this._selected_core;
            }
            this._root_contexts[core] = context;
        }
    }
}

