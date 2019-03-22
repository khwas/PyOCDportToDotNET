using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using invert32 = utility.mask.invert32;

//using logging;

using System.Diagnostics;
//using static coresight.ap;

namespace openocd.CoreSight
{
    public class RomTable
    { 

        public const UInt16 PIDR4 = 0xfd0;
        public const UInt16 PIDR0 = 0xfe0;
        public const UInt16 CIDR0 = 0xff0;
        public const UInt16 DEVTYPE = 0xfcc;
        public const UInt16 DEVID = 0xfc8;
        public const byte IDR_COUNT = 12;
        public const byte PIDR4_OFFSET = 0;
        public const byte PIDR0_OFFSET = 4;
        public const byte CIDR0_OFFSET = 8;

        public const UInt32 CIDR_PREAMBLE_MASK = 0xffff0fff;
        public const UInt32 CIDR_PREAMBLE_VALUE = 0xb105000d;

        public const UInt16 CIDR_COMPONENT_CLASS_MASK = 0xf000;
        public const byte CIDR_COMPONENT_CLASS_SHIFT = 12;

        public const byte CIDR_ROM_TABLE_CLASS = 0x1;
        public const byte CIDR_CORESIGHT_CLASS = 0x9;

        public const long PIDR_4KB_COUNT_MASK = 0xf000000000;
        public const byte PIDR_4KB_COUNT_SHIFT = 36;

        public const byte ROM_TABLE_ENTRY_PRESENT_MASK = 0x1;

        // Mask for ROM table entry size. 1 if 32-bit, 0 if 8-bit.
        public const byte ROM_TABLE_32BIT_MASK = 0x2;

        // 2's complement offset to debug component from ROM table base address.
        public const UInt32 ROM_TABLE_ADDR_OFFSET_NEG_MASK = 0x80000000;
        public const UInt32 ROM_TABLE_ADDR_OFFSET_MASK = 0xfffff000;
        public const byte ROM_TABLE_ADDR_OFFSET_SHIFT = 12;

        // 9 entries is enough entries to cover the standard Cortex-M4 ROM table for devices with ETM.
        public const byte ROM_TABLE_ENTRY_READ_COUNT = 9;
        public const UInt16 ROM_TABLE_MAX_ENTRIES = 960;

        // CoreSight devtype
        // Major Type [3:0]
        // Minor Type [7:4]
        //
        // CoreSight Major Types
        //  0 = Miscellaneous
        //  1 = Trace Sink
        //  2 = Trace Link
        //  3 = Trace Source
        //  4 = Debug Control
        //  5 = Debug Logic
        //
        // Known devtype values
        //  0x11 = TPIU
        //  0x13 = CPU trace source
        //  0x21 = ETB
        //  0x12 = Trace funnel
        //  0x14 = ECT
        public static Dictionary<long, string> PID_TABLE = new Dictionary<long, string>()
        {
            {0x4001bb932 , "MTB-M0+"},
            {0x00008e000 , "MTBDWT"},
            {0x4000bb9a6 , "CTI"},
            {0x4000bb4c0 , "ROM"},
            {0x4000bb008 , "SCS-M0+"},
            {0x4000bb00a , "DWT-M0+"},
            {0x4000bb00b , "BPU"},
            {0x4000bb00c , "SCS-M4"},
            {0x4003bb002 , "DWT"},
            {0x4002bb003 , "FPB"},
            {0x4003bb001 , "ITM"},
            {0x4000bb9a1 , "TPIU-M4"},
            {0x4000bb925 , "ETM-M4"},
            {0x4003bb907 , "ETB"},
            {0x4001bb908 , "CSTF"},
            {0x4000bb000 , "SCS-M3"},
            {0x4003bb923 , "TPIU-M3"},
            {0x4003bb924 , "ETM-M3"},
        };

        public class CoreSightComponent
        {
            internal MEM_AP ap;
            internal UInt32 address;
            internal UInt32 top_address;
            internal bool is_rom_table;
            internal UInt32 component_class;
            internal UInt32 cidr = 0;
            internal UInt32 pidr = 0;
            internal UInt32 devtype = 0;
            internal UInt32 devid = 0;
            internal int count_4kb = 0;
            internal string name = "";
            internal bool valid = false;
            public CoreSightComponent(MEM_AP ap, UInt32 top_addr)
            {
                this.ap = ap;
                this.address = top_addr;
                this.top_address = top_addr;
                this.component_class = 0;
                this.is_rom_table = false;
                this.cidr = 0;
                this.pidr = 0;
                this.devtype = 0;
                this.devid = 0;
                this.count_4kb = 0;
                this.name = "";
                this.valid = false;
            }

            public virtual void read_id_registers()
            {
                // Read Component ID and Peripheral ID registers. This is done as a single block read
                // for performance reasons.
                var regs = this.ap.readBlockMemoryAligned32(this.top_address + PIDR4, IDR_COUNT);
                this.cidr = this._extract_id_register_value(regs, CIDR0_OFFSET);
                this.pidr = this._extract_id_register_value(regs, PIDR4_OFFSET) << 32 | this._extract_id_register_value(regs, PIDR0_OFFSET);
                // Check if the component has a valid CIDR value
                if ((this.cidr & CIDR_PREAMBLE_MASK) != CIDR_PREAMBLE_VALUE)
                {
                    Trace.TraceWarning("Invalid coresight component, cidr=0x{0:X}", this.cidr);
                    return;
                }
                this.name = PID_TABLE.ContainsKey(this.pidr) ? PID_TABLE[this.pidr] : "";
                var component_class = (this.cidr & CIDR_COMPONENT_CLASS_MASK) >> CIDR_COMPONENT_CLASS_SHIFT;
                var is_rom_table = component_class == CIDR_ROM_TABLE_CLASS;
                var count_4kb = 1 << (int)((this.pidr & PIDR_4KB_COUNT_MASK) >> PIDR_4KB_COUNT_SHIFT);
                if (count_4kb > 1)
                {
                    var address = this.top_address - 4096 * (count_4kb - 1);
                }
                // From section 10.4 of ARM Debug InterfaceArchitecture Specification ADIv5.0 to ADIv5.2
                // In a ROM Table implementation:
                // - The Component class field, CIDR1.CLASS is 0x1, identifying the component as a ROM Table.
                // - The PIDR4.SIZE field must be 0. This is because a ROM Table must occupy a single 4KB block of memory.
                if (is_rom_table && count_4kb != 1)
                {
                    Trace.TraceWarning("Invalid rom table size=%x * 4KB", count_4kb);
                    return;
                }
                if (component_class == CIDR_CORESIGHT_CLASS)
                {
                    List<UInt32> _tup_1 = this.ap.readBlockMemoryAligned32(this.top_address + DEVID, 2);
                    this.devid = _tup_1[0];
                    this.devtype = _tup_1[1];
                }
                this.component_class = component_class;
                this.is_rom_table = is_rom_table;
                this.count_4kb = count_4kb;
                this.valid = true;
            }

            public virtual UInt32 _extract_id_register_value(List<UInt32> regs, UInt32 offset)
            {
                UInt32 result = 0;
                foreach (var i in Enumerable.Range(0, 3))
                {
                    var value = regs[(int)(offset + i)];
                    result |= (UInt32)((value & 255) << i * 8);
                }
                return result;
            }

            public override string ToString()
            {
                if (!this.valid)
                {
                    return String.Format("<{0:X08}:{1} cidr={2:X}, pidr={3:X}, component invalid>", this.address, this.name, this.cidr, this.pidr);
                }
                if (this.component_class == CIDR_CORESIGHT_CLASS)
                {
                    return String.Format("<{0:X08}:{1} cidr={2:X}, pidr={3:X}, class={4}, devtype={5:X}, devid={6:X}>", this.address, this.name, this.cidr, this.pidr, this.component_class, this.devtype, this.devid);
                }
                else
                {
                    return String.Format("<{0:X08}:{1} cidr={2:X}, pidr={3:X}, class={4}>", this.address, this.name, this.cidr, this.pidr, this.component_class);
                }
            }
        }

        public class ROMTable : CoreSightComponent
        {

            ROMTable parent;
            UInt32 number;
            UInt32 entry_size;
            public List<CoreSightComponent> components;

            public ROMTable(MEM_AP ap, UInt32? top_addr = null, ROMTable parent_table = null)
                : base(ap, top_addr ?? ap.rom_addr)
            {
                // // If no table address is provided, use the root ROM table for the AP.
                // if (top_addr == null)
                // {
                //     top_addr = ap.rom_addr;
                // }
                // super(ROMTable, this).@__init__(ap, top_addr);
                this.parent = parent_table;
                this.number = this.parent != null ? this.parent.number + 1 : 0;
                this.entry_size = 0;
                this.components = new List<CoreSightComponent>();
            }

            public virtual void init()
            {
                this.read_id_registers();
                if (!this.is_rom_table)
                {
                    Trace.TraceWarning("Warning: ROM table @ 0x{0:X8} has unexpected CIDR component class (0x{1:X})", this.address, this.component_class);
                    return;
                }
                if (this.count_4kb != 1)
                {
                    Trace.TraceWarning("Warning: ROM table @ 0x{0:X8} is larger than 4kB ({1} 4kb pages)", this.address, this.count_4kb);
                }
                this.read_table();
            }

            public virtual void read_table()
            {
                Trace.TraceInformation("ROM table #{0} @ 0x{1:X08} cidr={2:X} pidr={3:X}", this.number, this.address, this.cidr, this.pidr);
                this.components = new List<CoreSightComponent>();
                // Switch to the 8-bit table entry reader if we already know the entry size.
                if (this.entry_size == 8)
                {
                    this.read_table_8();
                }
                var entryAddress = this.address;
                var foundEnd = false;
                UInt32 entriesRead = 0;
                while (!foundEnd && entriesRead < ROM_TABLE_MAX_ENTRIES)
                {
                    // Read several entries at a time for performance.
                    UInt32 readCount = (UInt32)Math.Min(ROM_TABLE_MAX_ENTRIES - entriesRead, ROM_TABLE_ENTRY_READ_COUNT);
                    var entries = this.ap.readBlockMemoryAligned32(entryAddress, readCount);
                    entriesRead += readCount;
                    // Determine entry size if unknown.
                    if (this.entry_size == 0)
                    {
                        this.entry_size = (UInt32)((entries[0] & ROM_TABLE_32BIT_MASK) != 0 ? 32 : 8);
                        if (this.entry_size == 8)
                        {
                            // Read 8-bit table.
                            this.read_table_8();
                            return;
                        }
                    }
                    foreach (var entry in entries)
                    {
                        // Zero entry indicates the end of the table.
                        if (entry == 0)
                        {
                            foundEnd = true;
                            break;
                        }
                        this.handle_table_entry(entry);
                        entryAddress += 4;
                    }
                }
            }

            public virtual void read_table_8()
            {
                var entryAddress = this.address;
                while (true)
                {
                    // Read the full 32-bit table entry spread across four bytes.
                    UInt32 entry = this.ap.read8(entryAddress)();
                    entry |= (UInt32)this.ap.read8(entryAddress + 4)() << 8;
                    entry |= (UInt32)this.ap.read8(entryAddress + 8)() << 16;
                    entry |= (UInt32)this.ap.read8(entryAddress + 12)() << 24;
                    // Zero entry indicates the end of the table.
                    if (entry == 0)
                    {
                        break;
                    }
                    this.handle_table_entry(entry);
                    entryAddress += 16;
                }
            }

            public virtual void handle_table_entry(UInt32 entry)
            {
                // Nonzero entries can still be disabled, so check the present bit before handling.
                if ((entry & ROM_TABLE_ENTRY_PRESENT_MASK) == 0)
                {
                    return;
                }
                // Get the component's top 4k address.
                var offset = entry & ROM_TABLE_ADDR_OFFSET_MASK;
                if ((entry & ROM_TABLE_ADDR_OFFSET_NEG_MASK) != 0)
                {
                    offset = ~offset; // invert32(offset);
                }
                var address = this.address + offset;
                // Create component instance.
                var cmp = new CoreSightComponent(this.ap, address);
                cmp.read_id_registers();
                Trace.TraceInformation("[{0}]{1}", this.components.Count, cmp.ToString());
                // Recurse into child ROM tables.
                if (cmp.is_rom_table)
                {
                    cmp = new ROMTable(this.ap, address, parent_table: this);
                    ((ROMTable)cmp).init();
                }
                this.components.Add(cmp);
            }
        }
    }
}
