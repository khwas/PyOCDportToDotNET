using openocd.CmsisDap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static openocd.Core.Memory;

namespace openocd.Core
{

    public enum EBreakpointType
    {
        BREAKPOINT_HW = 1,
        BREAKPOINT_SW = 2,
        BREAKPOINT_AUTO = 3,
    }

    public enum ETargetState
    {
        TARGET_RUNNING = 1,
        TARGET_HALTED = 2,
        TARGET_RESET = 3,
        TARGET_SLEEPING = 4,
        TARGET_LOCKUP = 5,
        TARGET_PROGRAM = 6,
    }

    public interface ITarget
    {
        byte core_number { get; set; }
        cmsis_svd.device svd_device { get; }
        void setAutoUnlock(object doAutoUnlock);
        bool isLocked();
        void setHaltOnConnect(bool halt);
        void setFlash(Flash.Flash flash);
        void init(bool bus_accessible = true);
        void disconnect();
        object info(object request);
        void flush();
        UInt32 readIDCode();
        void halt();
        void step(bool disable_interrupts = true);
        void resume();
        bool massErase();
        void writeMemory(UInt32 addr, UInt32 value, byte transfer_size = 32);
        // Shorthand to write a 32-bit word.
        void write32(UInt32 addr, UInt32 value);
        // Shorthand to write a 16-bit halfword.
        void write16(UInt32 addr, UInt16 value);
        // Shorthand to write a byte.
        void write8(UInt32 addr, byte value);
        Func<UInt32> readMemory(UInt32 addr, byte transfer_size = 32, bool now = true);
        // Shorthand to read a 32-bit word.
        Func<UInt32> read32(UInt32 addr, bool now = true);
        // Shorthand to read a 16-bit halfword.
        Func<UInt16> read16(UInt32 addr, bool now = true);
        // Shorthand to read a byte.
        Func<byte> read8(UInt32 addr, bool now = true);
        void writeBlockMemoryUnaligned8(UInt32 addr, List<byte> data);
        void writeBlockMemoryAligned32(UInt32 addr, List<UInt32> data);
        List<byte> readBlockMemoryUnaligned8(UInt32 addr, UInt32 size);
        List<UInt32> readBlockMemoryAligned32(UInt32 addr, UInt32 size);
        //sbyte register_name_to_index(string reg);
        UInt32 readCoreRegister(string id);
        void writeCoreRegister(string id, UInt32 data);
        UInt32 readCoreRegisterRaw(string reg);
        List<UInt32> readCoreRegistersRaw(List<string> reg_list);
        void writeCoreRegisterRaw(string reg, UInt32 data);
        void writeCoreRegistersRaw(List<string> reg_list, List<UInt32> data_list);
        Debugger.Breakpoints.Provider.Breakpoint findBreakpoint(UInt32 addr);
        bool setBreakpoint(UInt32 addr, EBreakpointType type = EBreakpointType.BREAKPOINT_AUTO);
        byte getBreakpointType(UInt32 addr);
        void removeBreakpoint(UInt32 addr);
        bool setWatchpoint(UInt32 addr, byte size, byte type);
        void removeWatchpoint(UInt32 addr, byte size, byte type);
        void reset(bool? software_reset = null);
        void resetStopOnReset(bool? software_reset = null);
        void setTargetState(ETargetState state);
        ETargetState getState();
        int run_token { get; }
        bool isRunning();
        bool isHalted();
        Core.Memory.MemoryMap getMemoryMap();
        void setVectorCatch(UInt32 enableMask);
        UInt32 getVectorCatch();
        // GDB functions
        string getTargetXML();
        object getTargetContext(byte? core = null);
        object getRootContext(object core = null);
        void setRootContext(object context, object core = null);
    }

    public abstract partial class Target : ITarget
    {
        public const byte WATCHPOINT_READ = 1;
        public const byte WATCHPOINT_WRITE = 2;
        public const byte WATCHPOINT_READ_WRITE = 3;
        public const byte CATCH_NONE = 0;
        public const byte CATCH_HARD_FAULT = 1 << 0;
        public const byte CATCH_BUS_FAULT = 1 << 1;
        public const byte CATCH_MEM_FAULT = 1 << 2;
        public const byte CATCH_INTERRUPT_ERR = 1 << 3;
        public const byte CATCH_STATE_ERR = 1 << 4;
        public const byte CATCH_CHECK_ERR = 1 << 5;
        public const byte CATCH_COPROCESSOR_ERR = 1 << 6;
        public const byte CATCH_CORE_RESET = 1 << 7;
        public const byte CATCH_ALL = CATCH_HARD_FAULT | CATCH_BUS_FAULT | CATCH_MEM_FAULT | CATCH_INTERRUPT_ERR | CATCH_STATE_ERR | CATCH_CHECK_ERR | CATCH_COPROCESSOR_ERR | CATCH_CORE_RESET;

        internal IDapAccessLink link;
        internal Flash.Flash flash;
        internal bool halt_on_connect;
        public bool has_fpu;
        internal string part_number;
        public readonly MemoryMap memory_map;
        internal cmsis_svd.device _svd_device;
        internal string _svd_location; // debug.svd.SVDFile _svd_location;
        public byte core_number { get; set; }

        public Target(IDapAccessLink link, MemoryMap memoryMap = null)
        {
            this.link = link;
            this.flash = null;
            this.part_number = "";
            this.memory_map = memoryMap ?? new MemoryMap();
            this.halt_on_connect = true;
            this.has_fpu = false;
            this._svd_location = null;
            this._svd_device = null;
        }

        public virtual cmsis_svd.device svd_device
        {
            get
            {
                return this._svd_device;
            }
        }

        public virtual void setAutoUnlock(object doAutoUnlock) { }

        public virtual bool isLocked() => false;

        public virtual void setHaltOnConnect(bool halt) => this.halt_on_connect = halt;

        public virtual void setFlash(Flash.Flash flash) => this.flash = flash;

        public abstract void init(bool bus_accessible = true);

        public virtual void disconnect() { }

        public abstract object info(object request);

        public virtual void flush() => this.link.flush();

        public abstract UInt32 readIDCode();

        public abstract void halt();

        public abstract void step(bool disable_interrupts = true);

        public abstract void resume();

        public abstract bool massErase();

        public abstract void writeMemory(UInt32 addr, UInt32 value, byte transfer_size = 32);

        // Shorthand to write a 32-bit word.
        public void write32(UInt32 addr, UInt32 value) => this.writeMemory(addr, value, 32);

        // Shorthand to write a 16-bit halfword.
        public void write16(UInt32 addr, UInt16 value) => this.writeMemory(addr, value, 16);

        // Shorthand to write a byte.
        public void write8(UInt32 addr, byte value) => this.writeMemory(addr, value, 8);

        public abstract Func<UInt32> readMemory(UInt32 addr, byte transfer_size = 32, bool now = true);

        // Shorthand to read a 32-bit word.
        public Func<UInt32> read32(UInt32 addr, bool now = true) => this.readMemory(addr, 32, now);

        // Shorthand to read a 16-bit halfword.
        public Func<UInt16> read16(UInt32 addr, bool now = true) => new Func<UInt16>(() => (UInt16)this.readMemory(addr, 16, now)());

        // Shorthand to read a byte.
        public Func<byte> read8(UInt32 addr, bool now = true) => new Func<byte>(() => (byte)this.readMemory(addr, 8, now)());

        public abstract void writeBlockMemoryUnaligned8(UInt32 addr, List<byte> data);

        public abstract void writeBlockMemoryAligned32(UInt32 addr, List<UInt32> data);

        public abstract List<byte> readBlockMemoryUnaligned8(UInt32 addr, UInt32 size);

        public abstract List<UInt32> readBlockMemoryAligned32(UInt32 addr, UInt32 size);

        //public abstract sbyte register_name_to_index(string reg);

        public abstract UInt32 readCoreRegister(string id);

        public abstract void writeCoreRegister(string id, UInt32 data);

        public abstract UInt32 readCoreRegisterRaw(string reg);

        public abstract List<UInt32> readCoreRegistersRaw(List<string> reg_list);

        public abstract void writeCoreRegisterRaw(string reg, UInt32 data);

        public abstract void writeCoreRegistersRaw(List<string> reg_list_s, List<UInt32> data_list);

        public abstract Debugger.Breakpoints.Provider.Breakpoint findBreakpoint(UInt32 addr);
        public abstract bool setBreakpoint(UInt32 addr, EBreakpointType type = EBreakpointType.BREAKPOINT_AUTO);

        public abstract byte getBreakpointType(UInt32 addr);

        public abstract void removeBreakpoint(UInt32 addr);

        public abstract bool setWatchpoint(UInt32 addr, byte size, byte type);

        public abstract void removeWatchpoint(UInt32 addr, byte size, byte type);

        public abstract void reset(bool? software_reset = null);

        public abstract void resetStopOnReset(bool? software_reset = null);

        public abstract void setTargetState(ETargetState state);

        public abstract ETargetState getState();

        public abstract int run_token { get; }
        //{
        //    get
        //    {
        //        return 0;
        //    }
        //}

        public bool isRunning() => this.getState() == ETargetState.TARGET_RUNNING;

        public bool isHalted() => this.getState() == ETargetState.TARGET_HALTED;

        public Core.Memory.MemoryMap getMemoryMap() => this.memory_map;

        public abstract void setVectorCatch(UInt32 enableMask);

        public abstract UInt32 getVectorCatch();

        // GDB functions
        public abstract string getTargetXML();

        public abstract object getTargetContext(byte? core = null);

        public abstract object getRootContext(object core = null);

        public abstract void setRootContext(object context, object core = null);
    }

}
