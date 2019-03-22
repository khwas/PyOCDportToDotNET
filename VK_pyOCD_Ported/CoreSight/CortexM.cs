using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//using Element = xml.etree.ElementTree.Element;

//using SubElement = xml.etree.ElementTree.SubElement;

//using tostring = xml.etree.ElementTree.tostring;

//using DAPAccess = pyDAPAccess.DAPAccess;

//using utility; // = utility.conversion;
//
//using FPB = fpb.FPB;
//
//using DWT = dwt.DWT;
//
//using BreakpointManager = debug.breakpoints.manager.BreakpointManager;
//
//using SoftwareBreakpointProvider = debug.breakpoints.software.SoftwareBreakpointProvider;

//using logging;

//using @struct;

//using time = time.time;

//using sleep = time.sleep;


using System.Diagnostics;
//using pyDAPAccess;
//using static core.memory_map;
using System.Xml;
//using debug.breakpoints;
using System.Threading;
using openocd.Core;
using openocd.CmsisDap;
using openocd.Debugger.Breakpoints;

namespace openocd.CoreSight
{
    /// <summary>
    /// CMSIS-Core (Cortex-M)
    /// Cortex Microcontroller Software Interface Standard (CMSIS)
    /// https://www.keil.com/pack/doc/cmsis/Core/html/index.html
    /// </summary>
    public class CortexM : Target, ITarget
    {

        // CPUID PARTNO values
        public const UInt16 ARM_CortexM0 = 0xC20;
        public const UInt16 ARM_CortexM1 = 0xC21;
        public const UInt16 ARM_CortexM3 = 0xC23;
        public const UInt16 ARM_CortexM4 = 0xC24;
        public const UInt16 ARM_CortexM7 = 0xC27;
        public const UInt16 ARM_CortexM0p = 0xC60;

        public static Dictionary<UInt16, string> CORE_TYPE_NAME = new Dictionary<UInt16, string>()
        {
            {ARM_CortexM0, "Cortex-M0"},
            {ARM_CortexM1, "Cortex-M1"},
            {ARM_CortexM3, "Cortex-M3"},
            {ARM_CortexM4, "Cortex-M4"},
            {ARM_CortexM7, "Cortex-M7"},
            {ARM_CortexM0p,"Cortex-M0+"}
        };

        public static Dictionary<string, SByte> CORE_REGISTER = new Dictionary<string, SByte>()
        {
            {"r0",  0},
            {"r1",  1},
            {"r2",  2},
            {"r3",  3},
            {"r4",  4},
            {"r5",  5},
            {"r6",  6},
            {"r7",  7},
            {"r8",  8},
            {"r9",  9},
            {"r10", 10},
            {"r11", 11},
            {"r12", 12},
            {"sp",  13},
            {"r13", 13},
            {"lr",  14},
            {"r14", 14},
            {"pc",  15},
            {"r15", 15},
            {"xpsr",16},
            {"msp", 17},
            {"psp", 18},
            {"cfbp",20},
            {"control", -4},
            {"faultmask", -3},
            {"basepri", -2},
            {"primask", -1},
            {"fpscr", 33},
            {"s0", 64},
            {"s1", 65},
            {"s2", 66},
            {"s3", 67},
            {"s4", 68},
            {"s5", 69},
            {"s6", 70},
            {"s7", 71},
            {"s8", 72},
            {"s9", 73},
            {"s10",74},
            {"s11",75},
            {"s12",76},
            {"s13",77},
            {"s14",78},
            {"s15",79},
            {"s16",80},
            {"s17",81},
            {"s18",82},
            {"s19",83},
            {"s20",84},
            {"s21",85},
            {"s22",86},
            {"s23",87},
            {"s24",88},
            {"s25",89},
            {"s26",90},
            {"s27",91},
            {"s28",92},
            {"s29",93},
            {"s30",94},
            {"s31",95}
        };

        public static sbyte register_name_to_index(string reg)
        {
            try
            {
                return CORE_REGISTER[reg.ToLower()];
            }
            catch (KeyNotFoundException)
            {
                throw new KeyNotFoundException(String.Format("cannot find {0} core register", reg));
            }
        }

        // 
        //     This class has basic functions to access a Cortex M core:
        //        - init
        //        - read/write memory
        //        - read/write core registers
        //        - set/remove hardware breakpoints
        //     

        // Debug Fault Status Register
        public const UInt32 DFSR = 0xE000ED30;
        public const byte DFSR_EXTERNAL = (1 << 4);
        public const byte DFSR_VCATCH = (1 << 3);
        public const byte DFSR_DWTTRAP = (1 << 2);
        public const byte DFSR_BKPT = (1 << 1);
        public const byte DFSR_HALTED = (1 << 0);

        // Debug Exception and Monitor Control Register
        public const UInt32 DEMCR = 0xE000EDFC;

        // DWTENA in armv6 architecture reference manual
        public const UInt32 DEMCR_TRCENA = (1 << 24);
        public const UInt32 DEMCR_VC_HARDERR = (1 << 10);
        public const UInt32 DEMCR_VC_INTERR = (1 << 9);
        public const UInt32 DEMCR_VC_BUSERR = (1 << 8);
        public const UInt32 DEMCR_VC_STATERR = (1 << 7);
        public const UInt32 DEMCR_VC_CHKERR = (1 << 6);
        public const UInt32 DEMCR_VC_NOCPERR = (1 << 5);
        public const UInt32 DEMCR_VC_MMERR = (1 << 4);
        public const UInt32 DEMCR_VC_CORERESET = (1 << 0);

        // CPUID Register
        public const UInt32 CPUID = 0xE000ED00;

        // CPUID masks
        public const UInt32 CPUID_IMPLEMENTER_MASK = 0xff000000;
        public const byte CPUID_IMPLEMENTER_POS = 24;
        public const UInt32 CPUID_VARIANT_MASK = 0x00f00000;
        public const byte CPUID_VARIANT_POS = 20;
        public const UInt32 CPUID_ARCHITECTURE_MASK = 0x000f0000;
        public const byte CPUID_ARCHITECTURE_POS = 16;
        public const UInt32 CPUID_PARTNO_MASK = 0x0000fff0;
        public const byte CPUID_PARTNO_POS = 4;
        public const UInt32 CPUID_REVISION_MASK = 0x0000000f;
        public const byte CPUID_REVISION_POS = 0;

        public const byte CPUID_IMPLEMENTER_ARM = 0x41;
        public const byte ARMv6M = 0xC;
        public const byte ARMv7M = 0xF;

        // Debug Core Register Selector Register
        public const UInt32 DCRSR = 0xE000EDF4;
        public const UInt32 DCRSR_REGWnR = (1 << 16);
        public const byte DCRSR_REGSEL = 0x1F;

        // Debug Halting Control and Status Register
        public const UInt32 DHCSR = 0xE000EDF0;
        public const UInt32 C_DEBUGEN = (1 << 0);
        public const UInt32 C_HALT = (1 << 1);
        public const UInt32 C_STEP = (1 << 2);
        public const UInt32 C_MASKINTS = (1 << 3);
        public const UInt32 C_SNAPSTALL = (1 << 5);
        public const UInt32 S_REGRDY = (1 << 16);
        public const UInt32 S_HALT = (1 << 17);
        public const UInt32 S_SLEEP = (1 << 18);
        public const UInt32 S_LOCKUP = (1 << 19);
        public const UInt32 S_RETIRE_ST = (1 << 24);
        public const UInt32 S_RESET_ST = (1 << 25);

        // Debug Core Register Data Register
        public const UInt32 DCRDR = 0xE000EDF8;

        // Coprocessor Access Control Register
        public const UInt32 CPACR = 0xE000ED88;
        public const UInt32 CPACR_CP10_CP11_MASK = (3 << 20) | (3 << 22);

        public const UInt32 NVIC_AIRCR = (0xE000ED0C);
        public const UInt32 NVIC_AIRCR_VECTKEY = (0x5FA << 16);
        public const UInt32 NVIC_AIRCR_VECTRESET = (1 << 0);
        public const UInt32 NVIC_AIRCR_SYSRESETREQ = (1 << 2);

        public const UInt32 DBGKEY = ((UInt32)0xA05F << 16);

        public class RegisterInfo
        {
            internal readonly string name;
            internal readonly sbyte reg_num;
            internal readonly Dictionary<string, string> gdb_xml_attrib;

            public RegisterInfo(string name, byte bitsize, string reg_type, string reg_group)
            {
                this.name = name;
                this.reg_num = CORE_REGISTER[name];
                this.gdb_xml_attrib = new Dictionary<string, string>
                {
                    { "name",  name },
                    { "bitsize", bitsize.ToString() },
                    {  "type", reg_type.ToString() },
                    {  "group", reg_group.ToString() },
                };
            }
        }

        public List<RegisterInfo> regs_general = new List<RegisterInfo>
            {
                new RegisterInfo("r0", 32, "int", "general"),
                new RegisterInfo("r1", 32, "int", "general"),
                new RegisterInfo("r2", 32, "int", "general"),
                new RegisterInfo("r3", 32, "int", "general"),
                new RegisterInfo("r4", 32, "int", "general"),
                new RegisterInfo("r5", 32, "int", "general"),
                new RegisterInfo("r6", 32, "int", "general"),
                new RegisterInfo("r7", 32, "int", "general"),
                new RegisterInfo("r8", 32, "int", "general"),
                new RegisterInfo("r9", 32, "int", "general"),
                new RegisterInfo("r10", 32, "int", "general"),
                new RegisterInfo("r11", 32, "int", "general"),
                new RegisterInfo("r12", 32, "int", "general"),
                new RegisterInfo("sp", 32, "data_ptr", "general"),
                new RegisterInfo("lr", 32, "int", "general"),
                new RegisterInfo("pc", 32, "code_ptr", "general"),
                new RegisterInfo("xpsr", 32, "int", "general"),
                new RegisterInfo("msp", 32, "int", "general"),
                new RegisterInfo("psp", 32, "int", "general"),
                new RegisterInfo("primask", 32, "int", "general"),
                new RegisterInfo("control", 32, "int", "general"),
            };

        public List<RegisterInfo> regs_system_armv7_only = new List<RegisterInfo>
            {
                new RegisterInfo("basepri", 32, "int", "general"),
                new RegisterInfo("faultmask", 32, "int", "general"),
            };

        public List<RegisterInfo> regs_float = new List<RegisterInfo>
            {
                new RegisterInfo("fpscr", 32, "int", "float"),
                new RegisterInfo("s0", 32, "float", "float"),
                new RegisterInfo("s1", 32, "float", "float"),
                new RegisterInfo("s2", 32, "float", "float"),
                new RegisterInfo("s3", 32, "float", "float"),
                new RegisterInfo("s4", 32, "float", "float"),
                new RegisterInfo("s5", 32, "float", "float"),
                new RegisterInfo("s6", 32, "float", "float"),
                new RegisterInfo("s7", 32, "float", "float"),
                new RegisterInfo("s8", 32, "float", "float"),
                new RegisterInfo("s9", 32, "float", "float"),
                new RegisterInfo("s10", 32, "float", "float"),
                new RegisterInfo("s11", 32, "float", "float"),
                new RegisterInfo("s12", 32, "float", "float"),
                new RegisterInfo("s13", 32, "float", "float"),
                new RegisterInfo("s14", 32, "float", "float"),
                new RegisterInfo("s15", 32, "float", "float"),
                new RegisterInfo("s16", 32, "float", "float"),
                new RegisterInfo("s17", 32, "float", "float"),
                new RegisterInfo("s18", 32, "float", "float"),
                new RegisterInfo("s19", 32, "float", "float"),
                new RegisterInfo("s20", 32, "float", "float"),
                new RegisterInfo("s21", 32, "float", "float"),
                new RegisterInfo("s22", 32, "float", "float"),
                new RegisterInfo("s23", 32, "float", "float"),
                new RegisterInfo("s24", 32, "float", "float"),
                new RegisterInfo("s25", 32, "float", "float"),
                new RegisterInfo("s26", 32, "float", "float"),
                new RegisterInfo("s27", 32, "float", "float"),
                new RegisterInfo("s28", 32, "float", "float"),
                new RegisterInfo("s29", 32, "float", "float"),
                new RegisterInfo("s30", 32, "float", "float"),
                new RegisterInfo("s31", 32, "float", "float"),
            };

        private UInt32 arch;
        private UInt16 core_type;
        //internal bool has_fpu;
        private DebugAccessPort dp;
        private MEM_AP ap;
        //internal byte core_number;
        private int _run_token;
        private object _target_context;
        private FPB fpb;
        private DWT dwt;
        private Debugger.Breakpoints.Provider.BreakpointProvider sw_bp;
        //private bool halt_on_connect;
        private BreakpointManager bp_manager;
        private string targetXML;
        private List<RegisterInfo> register_list;

        public override int run_token => 0;

        public CortexM(IDapAccessLink link,
            DebugAccessPort dp,
            MEM_AP ap,
            Memory.MemoryMap memoryMap = null,
            byte core_num = 0) : base(link, memoryMap)
        {
            this.arch = 0;
            this.core_type = 0;
            this.has_fpu = false;
            this.dp = dp;
            this.ap = ap;
            this.core_number = core_num;
            this._run_token = 0;
            this._target_context = null;
            // Set up breakpoints manager.
            this.fpb = new FPB(this.ap);
            this.dwt = new DWT(this.ap);
            this.sw_bp = new SoftwareBreakpointProvider(this);
            this.bp_manager = new BreakpointManager(this);
            this.bp_manager.add_provider(this.fpb, EBreakpointType.BREAKPOINT_HW);
            this.bp_manager.add_provider(this.sw_bp, EBreakpointType.BREAKPOINT_SW);
        }

        // 
        //         Cortex M initialization. The bus must be accessible when this method is called.
        //         
        public override void init(bool bus_accessible = true)
        {
            if (this.halt_on_connect)
            {
                this.halt();
            }
            this.readCoreType();
            this.checkForFPU();
            this.buildTargetXML();
            this.fpb.init();
            this.dwt.init();
            this.sw_bp.init();
        }

        public override void disconnect()
        {
            // Remove breakpoints.
            this.bp_manager.remove_all_breakpoints();
            // Disable other debug blocks.
            this.write32(CortexM.DEMCR, 0);
            // Disable core debug.
            this.write32(CortexM.DHCSR, CortexM.DBGKEY | 0);
        }

        public virtual void buildTargetXML()
        {
            // Build register_list and targetXML
            this.register_list = new List<RegisterInfo>();
            XmlDocument xml_doc = new XmlDocument();
            XmlElement xml_root = (XmlElement)xml_doc.AppendChild(xml_doc.CreateElement("target"));
            var xml_regs_general = xml_root.AppendChild(xml_doc.CreateElement("feature"));
            xml_regs_general.Attributes.Append(xml_doc.CreateAttribute("name")).Value = "org.gnu.gdb.arm.m-profile";
            foreach (var reg in this.regs_general)
            {
                this.register_list.Add(reg);
                XmlNode reg_node = xml_regs_general.AppendChild(xml_doc.CreateElement("reg"));
                foreach (var kv in reg.gdb_xml_attrib)
                {
                    reg_node.Attributes.Append(xml_doc.CreateAttribute(kv.Key)).Value = kv.Value;
                }
            }
            // Check if target has ARMv7 registers
            if (new UInt16[] { ARM_CortexM3, ARM_CortexM4, ARM_CortexM7 }.Contains(this.core_type))
            {
                foreach (RegisterInfo reg in this.regs_system_armv7_only)
                {
                    this.register_list.Add(reg);
                    XmlNode reg_node = xml_regs_general.AppendChild(xml_doc.CreateElement("reg"));
                    foreach (var kv in reg.gdb_xml_attrib)
                    {
                        reg_node.Attributes.Append(xml_doc.CreateAttribute(kv.Key)).Value = kv.Value;
                    }
                }
            }
            // Check if target has FPU registers
            if (this.has_fpu)
            {
                //xml_regs_fpu = SubElement(xml_root, "feature", name="org.gnu.gdb.arm.vfp")
                foreach (var reg in this.regs_float)
                {
                    this.register_list.Add(reg);
                    XmlNode reg_node = xml_regs_general.AppendChild(xml_doc.CreateElement("reg"));
                    foreach (var kv in reg.gdb_xml_attrib)
                    {
                        reg_node.Attributes.Append(xml_doc.CreateAttribute(kv.Key)).Value = kv.Value;
                    }
                }
            }
            this.targetXML = @"<?xml version=""1.0""?><!DOCTYPE feature SYSTEM ""gdb-target.dtd"">" + xml_root.ToString();
        }

        // Read the CPUID register and determine core type.
        public virtual void readCoreType()
        {
            // Read CPUID register
            UInt32 cpuid = this.read32(CortexM.CPUID)();
            byte implementer = (byte)((cpuid & CortexM.CPUID_IMPLEMENTER_MASK) >> CortexM.CPUID_IMPLEMENTER_POS);
            if (implementer != CortexM.CPUID_IMPLEMENTER_ARM)
            {
                Trace.TraceWarning("CPU implementer is not ARM!");
            }
            this.arch = (cpuid & CortexM.CPUID_ARCHITECTURE_MASK) >> CortexM.CPUID_ARCHITECTURE_POS;
            this.core_type = (UInt16)((cpuid & CortexM.CPUID_PARTNO_MASK) >> CortexM.CPUID_PARTNO_POS);
            Trace.TraceInformation("CPU core is {0}", CORE_TYPE_NAME[this.core_type]);
        }

        // Determine if a Cortex-M4 has an FPU.
        //
        // The core type must have been identified prior to calling this function.
        public virtual void checkForFPU()
        {
            if (this.core_type != ARM_CortexM4 && this.core_type != ARM_CortexM7)
            {
                this.has_fpu = false;
                return;
            }
            var originalCpacr = this.read32(CortexM.CPACR)();
            var cpacr = originalCpacr | CortexM.CPACR_CP10_CP11_MASK;
            this.write32(CortexM.CPACR, cpacr);
            cpacr = this.read32(CortexM.CPACR)();
            this.has_fpu = (cpacr & CortexM.CPACR_CP10_CP11_MASK) != 0;
            // Restore previous value.
            this.write32(CortexM.CPACR, originalCpacr);
            if (this.has_fpu)
            {
                Trace.TraceInformation("FPU present");
            }
        }

        // 
        //         return the IDCODE of the core
        //         
        public override UInt32 readIDCode() => this.dp.read_id_code();
        public override void flush() => this.dp.flush();

        // 
        //         write a memory location.
        //         By default the transfer size is a word
        //         
        public override void writeMemory(UInt32 addr, UInt32 value, byte transfer_size = 32)
        {
            this.ap.writeMemory(addr, value, transfer_size);
        }

        // 
        //         read a memory location. By default, a word will
        //         be read
        //         
        public override Func<UInt32> readMemory(UInt32 addr, byte transfer_size = 32, bool now = true)
        {
            Func<UInt32> result = this.ap.readMemory(addr, transfer_size, now);
            // Read callback returned for async reads.
            Func<UInt32> readMemoryCb = new Func<UInt32>(() =>
            {
                return (UInt32)this.bp_manager.filter_memory(addr, transfer_size, result());
            });
            if (now)
            {
                UInt32 resultSync = readMemoryCb();
                return new Func<UInt32>(() => resultSync);
            }
            else
            {
                return readMemoryCb;
            }
        }

        // 
        //         read a block of unaligned bytes in memory. Returns
        //         an array of byte values
        //         
        public override List<byte> readBlockMemoryUnaligned8(UInt32 addr, UInt32 size)
        {
            var data = this.ap.readBlockMemoryUnaligned8(addr, size);
            return this.bp_manager.filter_memory_unaligned_8(addr, size, data);
        }

        // 
        //         write a block of unaligned bytes in memory.
        //         
        public override void writeBlockMemoryUnaligned8(UInt32 addr, List<byte> data)
        {
            this.ap.writeBlockMemoryUnaligned8(addr, data);
        }

        // 
        //         write a block of aligned words in memory.
        //         
        public override void writeBlockMemoryAligned32(UInt32 addr, List<UInt32> data)
        {
            this.ap.writeBlockMemoryAligned32(addr, data);
        }

        // 
        //         read a block of aligned words in memory. Returns
        //         an array of word values
        //         
        public override List<UInt32> readBlockMemoryAligned32(UInt32 addr, UInt32 size)
        {
            var data = this.ap.readBlockMemoryAligned32(addr, size);
            return this.bp_manager.filter_memory_aligned_32(addr, size, data);
        }

        // 
        //         halt the core
        //         
        public override void halt()
        {
            this.writeMemory(CortexM.DHCSR, CortexM.DBGKEY | CortexM.C_DEBUGEN | CortexM.C_HALT);
            this.dp.flush();
        }

        // 
        //         perform an instruction level step.  This function preserves the previous
        //         interrupt mask state
        //         
        public override void step(bool disable_interrupts = true)
        {
            // Was 'if self.getState() != TARGET_HALTED:'
            // but now value of dhcsr is saved
            UInt32 dhcsr = this.readMemory(CortexM.DHCSR)();
            if ((dhcsr & (CortexM.C_STEP | CortexM.C_HALT)) == 0)
            {
                Trace.TraceError("cannot step: target not halted");
                return;
            }
            this.clearDebugCauseBits();
            // Save previous interrupt mask state
            bool interrupts_masked = (CortexM.C_MASKINTS & dhcsr) != 0;
            // Mask interrupts - C_HALT must be set when changing to C_MASKINTS
            if (!interrupts_masked && disable_interrupts)
            {
                this.writeMemory(CortexM.DHCSR, CortexM.DBGKEY | CortexM.C_DEBUGEN | CortexM.C_HALT | CortexM.C_MASKINTS);
            }
            // Single step using current C_MASKINTS setting
            if (disable_interrupts || interrupts_masked)
            {
                this.writeMemory(CortexM.DHCSR, CortexM.DBGKEY | CortexM.C_DEBUGEN | CortexM.C_MASKINTS | CortexM.C_STEP);
            }
            else
            {
                this.writeMemory(CortexM.DHCSR, CortexM.DBGKEY | CortexM.C_DEBUGEN | CortexM.C_STEP);
            }
            // Wait for halt to auto set (This should be done before the first read)
            while ((this.readMemory(CortexM.DHCSR)() & CortexM.C_HALT) == 0)
            {
            }
            // Restore interrupt mask state
            if (!interrupts_masked && disable_interrupts)
            {
                // Unmask interrupts - C_HALT must be set when changing to C_MASKINTS
                this.writeMemory(CortexM.DHCSR, CortexM.DBGKEY | CortexM.C_DEBUGEN | CortexM.C_HALT);
            }
            this.dp.flush();
            this._run_token += 1;
        }

        public virtual void clearDebugCauseBits()
        {
            this.writeMemory(CortexM.DFSR, CortexM.DFSR_DWTTRAP | CortexM.DFSR_BKPT | CortexM.DFSR_HALTED);
        }

        // 
        //         reset a core. After a call to this function, the core
        //         is running
        //         
        public override void reset(bool? software_reset = null)
        {
            if (software_reset == null)
            {
                // Default to software reset if nothing is specified
                software_reset = true;
            }
            this._run_token += 1;
            if ((bool)software_reset)
            {
                // Perform the reset.
                try
                {
                    this.writeMemory(CortexM.NVIC_AIRCR, CortexM.NVIC_AIRCR_VECTKEY | CortexM.NVIC_AIRCR_SYSRESETREQ);
                    // Without a flush a transfer error can occur
                    this.dp.flush();
                }
                catch
                {
                    this.dp.flush();
                }
            }
            else
            {
                this.dp.reset();
            }
            // Now wait for the system to come out of reset. Keep reading the DHCSR until
            // we get a good response with S_RESET_ST cleared, or we time out.
            var startTime = DateTime.Now;
            while (DateTime.Now - startTime < TimeSpan.FromSeconds(2.0))
            {
                try
                {
                    var dhcsr = this.read32(CortexM.DHCSR)();
                    if ((dhcsr & CortexM.S_RESET_ST) == 0)
                    {
                        break;
                    }
                }
                catch
                {
                    this.dp.flush();
                    Thread.Sleep(10);
                }
            }
        }

        // 
        //         perform a reset and stop the core on the reset handler
        //         
        public override void resetStopOnReset(bool? software_reset = null)
        {
            Trace.TraceInformation("reset stop on Reset");
            // halt the target
            this.halt();
            // Save CortexM.DEMCR
            var demcr = this.readMemory(CortexM.DEMCR)();
            // enable the vector catch
            this.writeMemory(CortexM.DEMCR, demcr | CortexM.DEMCR_VC_CORERESET);
            this.reset(software_reset);
            // wait until the unit resets
            while (this.isRunning())
            {
            }
            // restore vector catch setting
            this.writeMemory(CortexM.DEMCR, demcr);
        }

        public override void setTargetState(ETargetState state)
        {
            if (state == ETargetState.TARGET_PROGRAM)
            {
                this.resetStopOnReset(true);
                // Write the thumb bit in case the reset handler
                // points to an ARM address
                this.writeCoreRegister("xpsr", 0x1000000);
            }
        }

        public override ETargetState getState()
        {
            UInt32 dhcsr = this.readMemory(CortexM.DHCSR)();
            if ((dhcsr & CortexM.S_RESET_ST) != 0)
            {
                // Reset is a special case because the bit is sticky and really means
                // "core was reset since last read of DHCSR". We have to re-read the
                // DHCSR, check if S_RESET_ST is still set and make sure no instructions
                // were executed by checking S_RETIRE_ST.
                var newDhcsr = this.readMemory(CortexM.DHCSR)();
                if (((newDhcsr & CortexM.S_RESET_ST) != 0) && ((newDhcsr & CortexM.S_RETIRE_ST) == 0))
                {
                    return ETargetState.TARGET_RESET;
                }
            }
            if ((dhcsr & CortexM.S_LOCKUP) != 0)
            {
                return ETargetState.TARGET_LOCKUP;
            }
            else if ((dhcsr & CortexM.S_SLEEP) != 0)
            {
                return ETargetState.TARGET_SLEEPING;
            }
            else if ((dhcsr & CortexM.S_HALT) != 0)
            {
                return ETargetState.TARGET_HALTED;
            }
            else
            {
                return ETargetState.TARGET_RUNNING;
            }
        }

        // 
        //         resume the execution
        //         
        public override void resume()
        {
            if (this.getState() != ETargetState.TARGET_HALTED)
            {
                Trace.TraceInformation("cannot resume: target not halted");
                return;
            }
            this._run_token += 1;
            this.clearDebugCauseBits();
            this.writeMemory(CortexM.DHCSR, CortexM.DBGKEY | CortexM.C_DEBUGEN);
            this.dp.flush();
        }

        public override Debugger.Breakpoints.Provider.Breakpoint findBreakpoint(UInt32 addr)
        {
            return this.bp_manager.find_breakpoint(addr);
        }

        // 
        //         read CPU register
        //         Unpack floating point register values
        //         
        public override UInt32 readCoreRegister(string reg)
        {
            var regIndex = register_name_to_index(reg);
            UInt32 regValue = this.readCoreRegisterRaw(reg);
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
        public override UInt32 readCoreRegisterRaw(string reg)
        {
            var vals = this.readCoreRegistersRaw(new List<string> {
                    reg
                });
            return vals[0];
        }

        // 
        //         Read one or more core registers
        // 
        //         Read core registers in reg_list and return a list of values.
        //         If any register in reg_list is a string, find the number
        //         associated to this register in the lookup table CORE_REGISTER.
        //         
        public override List<UInt32> readCoreRegistersRaw(List<string> reg_list_s)
        {
            Func<UInt32> reg_cb;
            Func<UInt32> dhcsr_cb;
            //object reg;
            // convert to index only
            List<sbyte> reg_list = reg_list_s.Select(r => register_name_to_index(r)).ToList();
            // Sanity check register values
            foreach (var reg in reg_list)
            {
                if (!CORE_REGISTER.Values.Contains(reg))
                {
                    throw new ArgumentOutOfRangeException(String.Format("unknown reg: {0}", reg));
                }
                else if ((reg >= 64 || reg == 33) && !this.has_fpu)
                {
                    throw new ArgumentOutOfRangeException("attempt to read FPU register without FPU");
                }
            }
            // Begin all reads and writes
            List<Func<UInt32>> dhcsr_cb_list = new List<Func<UInt32>>();
            List<Func<UInt32>> reg_cb_list = new List<Func<UInt32>>();
            foreach (sbyte reg in reg_list)
            {
                sbyte val;
                if (reg < 0 && reg >= -4)
                {
                    val = CORE_REGISTER["cfbp"];
                }
                else
                {
                    val = reg;
                }
                // write id in DCRSR
                this.writeMemory(CortexM.DCRSR, (UInt32)val);
                // Technically, we need to poll S_REGRDY in DHCSR here before reading DCRDR. But
                // we're running so slow compared to the target that it's not necessary.
                // Read it and assert that S_REGRDY is set
                dhcsr_cb = this.readMemory(CortexM.DHCSR, now: false);
                reg_cb = this.readMemory(CortexM.DCRDR, now: false);
                dhcsr_cb_list.Add(dhcsr_cb);
                reg_cb_list.Add(reg_cb);
            }
            // Read all results
            var reg_vals = new List<UInt32>();
            for (int i = 0; i < reg_list.Count; i++)
            {
                sbyte reg = reg_list[i];
                reg_cb = reg_cb_list[i];
                dhcsr_cb = dhcsr_cb_list[i];
                var dhcsr_val = dhcsr_cb();
                Debug.Assert((dhcsr_val & CortexM.S_REGRDY) != 0);
                var val = reg_cb();
                // Special handling for registers that are combined into a single DCRSR number.
                if (reg < 0 && reg >= -4)
                {
                    val = val >> (-reg - 1) * 8 & 0xFF;
                }
                reg_vals.Add(val);
            }
            return reg_vals;
        }

        // 
        //         write a CPU register.
        //         Will need to pack floating point register values before writing.
        //         
        public override void writeCoreRegister(string reg, UInt32 data)
        {
            var regIndex = register_name_to_index(reg);
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
        public override void writeCoreRegisterRaw(string reg, UInt32 data)
        {
            this.writeCoreRegistersRaw(new List<string> {
                    reg
                }, new List<UInt32> {
                    data
                });
        }

        // 
        //         Write one or more core registers
        // 
        //         Write core registers in reg_list with the associated value in
        //         data_list.  If any register in reg_list is a string, find the number
        //         associated to this register in the lookup table CORE_REGISTER.
        //         
        public override void writeCoreRegistersRaw(List<string> reg_list_s, List<UInt32> data_list)
        {
            Debug.Assert(reg_list_s.Count == data_list.Count);
            // convert to index only
            List<sbyte> reg_list = reg_list_s.Select(reg => register_name_to_index(reg)).ToList();
            // Sanity check register values
            foreach (var reg in reg_list)
            {
                if (!CORE_REGISTER.Values.Contains(reg))
                {
                    throw new ArgumentOutOfRangeException(String.Format("unknown reg: {0}", reg));
                }
                else if ((reg >= 64 || reg == 33) && !this.has_fpu)
                {
                    throw new ArgumentOutOfRangeException("attempt to write FPU register without FPU");
                }
            }
            object specialRegValue = null;
            // Read special register if it is present in the list
            foreach (var reg in reg_list)
            {
                if (reg < 0 && reg >= -4)
                {
                    specialRegValue = this.readCoreRegister("cfbp");
                    break;
                }
            }
            // Write out registers
            List<Func<UInt32>> dhcsr_cb_list = new List<Func<UInt32>>();
            foreach (Tuple<sbyte, UInt32> _tup_1 in reg_list.Zip(data_list, (r, d) => new Tuple<sbyte, UInt32>(r, d)))
            {
                sbyte reg = _tup_1.Item1;
                UInt32 data = _tup_1.Item2;
                if (reg < 0 && reg >= -4)
                {
                    // Mask in the new special register value so we don't modify the other register
                    // values that share the same DCRSR number.
                    var shift = (-reg - 1) * 8;
                    var mask = -1 ^ 255 << shift;
                    data = (UInt32)((UInt32)specialRegValue & mask | (data & 255) << shift);
                    specialRegValue = data;
                    reg = CORE_REGISTER["cfbp"];
                }
                // write DCRDR
                this.writeMemory(CortexM.DCRDR, data);
                // write id in DCRSR and flag to start write transfer
                Debug.Assert(reg >= 0);
                this.writeMemory(CortexM.DCRSR, ((UInt32)(byte)reg) | DCRSR_REGWnR);
                // Technically, we need to poll S_REGRDY in DHCSR here to ensure the
                // register write has completed.
                // Read it and assert that S_REGRDY is set
                Func<UInt32> dhcsr_cb = this.readMemory(DHCSR, now: false);
                dhcsr_cb_list.Add(dhcsr_cb);
            }
            // Make sure S_REGRDY was set for all register
            // writes
            foreach (var dhcsr_cb in dhcsr_cb_list)
            {
                var dhcsr_val = dhcsr_cb();
                Debug.Assert((dhcsr_val & S_REGRDY) != 0);
            }
        }

        // Set a hardware or software breakpoint at a specific location in memory.
        //
        // @retval True Breakpoint was set.
        // @retval False Breakpoint could not be set.
        public override bool setBreakpoint(UInt32 addr, EBreakpointType type = EBreakpointType.BREAKPOINT_AUTO)
        {
            return this.bp_manager.set_breakpoint(addr, type);
        }

        // Remove a breakpoint at a specific location.
        public override void removeBreakpoint(UInt32 addr)
        {
            this.bp_manager.remove_breakpoint(addr);
        }

        public override byte getBreakpointType(UInt32 addr)
        {
            return (byte)this.bp_manager.get_breakpoint_type(addr);
        }

        public virtual object availableBreakpoint()
        {
            return this.fpb.available_breakpoints();
        }

        public virtual Watchpoint findWatchpoint(UInt32 addr, byte size, byte type)
        {
            return this.dwt.find_watchpoint(addr, size, type);
        }

        // 
        //         set a hardware watchpoint
        //         
        public override bool setWatchpoint(UInt32 addr, byte size, byte type)
        {
            return this.dwt.set_watchpoint(addr, size, type);
        }

        // 
        //         remove a hardware watchpoint
        //         
        public override void removeWatchpoint(UInt32 addr, byte size, byte type)
        {
            this.dwt.remove_watchpoint(addr, size, type);
        }

        // [staticmethod]
        public static UInt32 _map_to_vector_catch_mask(UInt32 mask)
        {
            UInt32 result = 0;
            if ((mask & Target.CATCH_HARD_FAULT) != 0)
            {
                result |= CortexM.DEMCR_VC_HARDERR;
            }
            if ((mask & Target.CATCH_BUS_FAULT) != 0)
            {
                result |= CortexM.DEMCR_VC_BUSERR;
            }
            if ((mask & Target.CATCH_MEM_FAULT) != 0)
            {
                result |= CortexM.DEMCR_VC_MMERR;
            }
            if ((mask & Target.CATCH_INTERRUPT_ERR) != 0)
            {
                result |= CortexM.DEMCR_VC_INTERR;
            }
            if ((mask & Target.CATCH_STATE_ERR) != 0)
            {
                result |= CortexM.DEMCR_VC_STATERR;
            }
            if ((mask & Target.CATCH_CHECK_ERR) != 0)
            {
                result |= CortexM.DEMCR_VC_CHKERR;
            }
            if ((mask & Target.CATCH_COPROCESSOR_ERR) != 0)
            {
                result |= CortexM.DEMCR_VC_NOCPERR;
            }
            if ((mask & Target.CATCH_CORE_RESET) != 0)
            {
                result |= CortexM.DEMCR_VC_CORERESET;
            }
            return result;
        }

        // [staticmethod]
        public static UInt32 _map_from_vector_catch_mask(UInt32 mask)
        {
            UInt32 result = 0;
            if ((mask & CortexM.DEMCR_VC_HARDERR) != 0)
            {
                result |= Target.CATCH_HARD_FAULT;
            }
            if ((mask & CortexM.DEMCR_VC_BUSERR) != 0)
            {
                result |= Target.CATCH_BUS_FAULT;
            }
            if ((mask & CortexM.DEMCR_VC_MMERR) != 0)
            {
                result |= Target.CATCH_MEM_FAULT;
            }
            if ((mask & CortexM.DEMCR_VC_INTERR) != 0)
            {
                result |= Target.CATCH_INTERRUPT_ERR;
            }
            if ((mask & CortexM.DEMCR_VC_STATERR) != 0)
            {
                result |= Target.CATCH_STATE_ERR;
            }
            if ((mask & CortexM.DEMCR_VC_CHKERR) != 0)
            {
                result |= Target.CATCH_CHECK_ERR;
            }
            if ((mask & CortexM.DEMCR_VC_NOCPERR) != 0)
            {
                result |= Target.CATCH_COPROCESSOR_ERR;
            }
            if ((mask & CortexM.DEMCR_VC_CORERESET) != 0)
            {
                result |= Target.CATCH_CORE_RESET;
            }
            return result;
        }

        public override void setVectorCatch(UInt32 enableMask)
        {
            var demcr = this.readMemory(CortexM.DEMCR)();
            demcr |= CortexM._map_to_vector_catch_mask(enableMask);
            demcr |= ~CortexM._map_to_vector_catch_mask(~enableMask);
            this.writeMemory(CortexM.DEMCR, demcr);
        }

        public override UInt32 getVectorCatch()
        {
            var demcr = this.readMemory(CortexM.DEMCR)();
            return CortexM._map_from_vector_catch_mask(demcr);
        }

        // GDB functions
        public override string getTargetXML()
        {
            return this.targetXML;
        }

        public virtual bool isDebugTrap()
        {
            var debugEvents = this.readMemory(CortexM.DFSR)() & (CortexM.DFSR_DWTTRAP | CortexM.DFSR_BKPT | CortexM.DFSR_HALTED);
            return debugEvents != 0;
        }

        public override object getTargetContext(byte? core = null)
        {
            return this._target_context;
        }

        public void setTargetContext(object context)
        {
            this._target_context = context;
        }

        public override object info(object request) => throw new NotImplementedException();
        public override bool massErase() => throw new NotImplementedException();

        public override object getRootContext(object core = null) => throw new NotImplementedException();

        public override void setRootContext(object context, object core = null) => throw new NotImplementedException();
    }
}
