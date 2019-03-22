using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace openocd.Debugger
{
    public class DebugContext
    {
        internal readonly Core.Target _core;
        public DebugContext(Core.Target core)
        {
            this._core = core;
        }

        public Core.Target core
        {
            get
            {
                return this._core;
            }
        }

        public virtual void writeMemory(UInt32 addr, UInt32 value, byte transfer_size = 32)
        {
            this._core.writeMemory(addr, value, transfer_size);
        }

        public virtual object readMemory(UInt32 addr, byte transfer_size = 32, bool now = true)
        {
            return this._core.readMemory(addr, transfer_size, now);
        }

        public virtual void writeBlockMemoryUnaligned8(UInt32 addr, List<byte> value)
        {
            this._core.writeBlockMemoryUnaligned8(addr, value);
        }

        public virtual void writeBlockMemoryAligned32(UInt32 addr, List<UInt32> data)
        {
            this._core.writeBlockMemoryAligned32(addr, data);
        }

        public virtual List<byte> readBlockMemoryUnaligned8(UInt32 addr, UInt32 size)
        {
            return this._core.readBlockMemoryUnaligned8(addr, size);
        }

        public virtual List<UInt32> readBlockMemoryAligned32(UInt32 addr, UInt32 size)
        {
            return this._core.readBlockMemoryAligned32(addr, size);
        }

        // @brief Shorthand to write a 32-bit word.
        public virtual void write32(UInt32 addr, UInt32 value)
        {
            this.writeMemory(addr, value, 32);
        }

        // @brief Shorthand to write a 16-bit halfword.
        public virtual void write16(UInt32 addr, UInt16 value)
        {
            this.writeMemory(addr, value, 16);
        }

        // @brief Shorthand to write a byte.
        public virtual void write8(UInt32 addr, byte value)
        {
            this.writeMemory(addr, value, 8);
        }

        // @brief Shorthand to read a 32-bit word.
        public virtual object read32(UInt32 addr, bool now = true)
        {
            return this.readMemory(addr, 32, now);
        }

        // @brief Shorthand to read a 16-bit halfword.
        public virtual object read16(UInt32 addr, bool now = true)
        {
            return this.readMemory(addr, 16, now);
        }

        // @brief Shorthand to read a byte.
        public virtual object read8(UInt32 addr, bool now = true)
        {
            return this.readMemory(addr, 8, now);
        }

        // 
        //         read CPU register
        //         Unpack floating point register values
        //         
        public virtual object readCoreRegister(string reg)
        {
            var regIndex = CoreSight.CortexM.register_name_to_index(reg);
            var regValue = this.readCoreRegisterRaw(reg);
            // Convert int to float.
            if (regIndex >= 64)
            {
                throw new NotImplementedException();
                //regValue = Utility.Conversion.u32BEToFloat32BE(regValue);
            }
            return regValue;
        }

        // 
        //         read a core register (r0 .. r16).
        //         If reg is a string, find the number associated to this register
        //         in the lookup table CORE_REGISTER
        //         
        public virtual object readCoreRegisterRaw(string reg)
        {
            var vals = this.readCoreRegistersRaw(new List<string> {
                    reg
                });
            return vals[0];
        }

        public virtual List<UInt32> readCoreRegistersRaw(List<string> reg_list)
        {
            return this._core.readCoreRegistersRaw(reg_list);
        }

        // 
        //         write a CPU register.
        //         Will need to pack floating point register values before writing.
        //         
        public virtual void writeCoreRegister(string reg, UInt32 data)
        {
            sbyte regIndex = CoreSight.CortexM.register_name_to_index(reg);
            // Convert float to int.
            if (regIndex >= 64)
            {
                throw new NotImplementedException();
                // data = Utility.Conversion.float32beToU32be((float)data);
            }
            this.writeCoreRegisterRaw(reg, data);
        }

        // 
        //         write a core register (r0 .. r16)
        //         If reg is a string, find the number associated to this register
        //         in the lookup table CORE_REGISTER
        //         
        public virtual void writeCoreRegisterRaw(string reg, UInt32 data)
        {
            this.writeCoreRegistersRaw(new List<string> {
                    reg
                }, new List<UInt32> {
                    data
                });
        }

        public virtual void writeCoreRegistersRaw(List<string> reg_list, List<UInt32> data_list)
        {
            this._core.writeCoreRegistersRaw(reg_list, data_list);
        }

        public virtual void flush()
        {
            this._core.flush();
        }
    }
}
