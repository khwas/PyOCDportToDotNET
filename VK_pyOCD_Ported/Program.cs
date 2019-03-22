using MadWizard.WinUSBNet;
using openocd.CmsisDap.Backend;
using openocd.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace openocd.CmsisDap
{
    class Program
    {
        static void Main(string[] args)
        {
           // {
           //     byte[] bin = File.ReadAllBytes(@"c:\Projects\MOAP\RandD\VK_PyOCD\VK_pyOCD_Ported\Targets\STM32F7x_1024.bin");
           //     Debug.Assert(bin.Length % 4 == 0);
           //     UInt32[] uints = new UInt32[bin.Length / 4];
           //     Buffer.BlockCopy(bin, 0, uints, 0, bin.Length);
           //     foreach (UInt32 uin in uints)
           //     {
           //         Trace.TraceInformation("0x{0:X8}", uin);
           //     }
           // }
           
            // // List<IDapAccessLink> links = DapAccessLink.get_connected_devices();
            // // Debug.Assert(links.Count > 0);

            // GUID is an example, specify your own unique GUID in the .inf file
            USBDeviceInfo[] details = USBDevice.GetDevices("{CDB3B5AD-293B-4663-AA36-1AAE46463776}");
            USBDeviceInfo match = details.First(info => info.VID == 0xC251 && info.PID == 0xF00A);
            BackendWinUsb backend = new BackendWinUsb(match);
            IDapAccessLink link = new DapAccessLink("WINUSB1", backend); // links.First();
            
            // IDapAccessLink link = links.First();
            link.open();
            link.set_clock(300000); // Typically 1.8..2.0 MHz is fastest speed allowed
            link.connect();
            ////link.set_deferred_transfer(true);
            link.set_deferred_transfer(false);
            {
                var ver = link.identify(EDapInfoIDByte.FW_VER);
                if (ver is string)
                {
                    ver = ((string)ver).TrimEnd('\0');
                }
                Trace.TraceInformation("CMSIS-DAP Firmware Version: {0}", ver);
            }

            {
                object capabilities = link.identify(EDapInfoIDByte.CAPABILITIES);
                //                 Available transfer protocols to target:
                // 
                //                 Info0 - Bit 0: 1 = SWD Serial Wire Debug communication is implemented (0 = SWD Commands not implemented).
                // Info0 - Bit 1: 1 = JTAG communication is implemented (0 = JTAG Commands not implemented).
                // Serial Wire Trace(SWO) support:
                // 
                //                 Info0 - Bit 2: 1 = SWO UART - UART Serial Wire Output is implemented (0 = not implemented).
                // Info0 - Bit 3: 1 = SWO Manchester - Manchester Serial Wire Output is implemented (0 = not implemented).
                // Command extensions for transfer protocol:
                // 
                // Info0 - Bit 4: 1 = Atomic Commands - Atomic Commands support is implemented (0 = Atomic Commands not implemented).
                // Time synchronisation via Test Domain Timer:
                // 
                // Info0 - Bit 5: 1 = Test Domain Timer -debug unit support for Test Domain Timer is implemented (0 = not implemented).
                // SWO Streaming Trace support:
                // 
                // Info0 - Bit 6: 1 = SWO Streaming Trace is implemented (0 = not implemented).
                UInt16 flags = (capabilities is byte) ? (byte)capabilities : (UInt16)capabilities;
                if ((flags & 0x0001) != 0)
                {
                    Trace.TraceInformation("CAPABILITY: SWD Serial Wire Debug communication is implemented");
                }
                if ((flags & 0x0002) != 0)
                {
                    Trace.TraceInformation("CAPABILITY: JTAG communication is implemented");
                }
                if ((flags & 0x0004) != 0)
                {
                    Trace.TraceInformation("CAPABILITY: SWO UART - UART Serial Wire Output is implemented");
                }
                if ((flags & 0x0008) != 0)
                {
                    Trace.TraceInformation("CAPABILITY: SWO Manchester - Manchester Serial Wire Output is implemented");
                }
                if ((flags & 0x0010) != 0)
                {
                    Trace.TraceInformation("CAPABILITY: Atomic Commands - Atomic Commands support is implemented");
                }
                if ((flags & 0x0020) != 0)
                {
                    Trace.TraceInformation("CAPABILITY: Test Domain Timer -debug unit support for Test Domain Timer is implemented");
                }
                if ((flags & 0x0040) != 0)
                {
                    Trace.TraceInformation("CAPABILITY: SWO Streaming Trace is implemented");
                }
            }

            string uniqueID = link.get_unique_id();

            Targets.Target_W7500.W7500 w = new Targets.Target_W7500.W7500(link);
            //Targets.Target_STM32F7x_1024.STM32F7x_1024 w = new Targets.Target_STM32F7x_1024.STM32F7x_1024(link);
            w.init();
            w.halt();
            {
                ETargetState s = w.getState();
                Debug.Assert(s == ETargetState.TARGET_HALTED);
            }
             w.resetStopOnReset();
             {
                 ETargetState s = w.getState();
                 Debug.Assert(s == ETargetState.TARGET_HALTED);
             }

            {
                UInt32 idcode = w.readIDCode();
                //                     STM32                   Wiznet
                Debug.Assert(idcode == 0x5BA02477 || idcode == 0x0bb11477);

            }
            //58.341]  < sequence name = "DebugCoreStart" Pname = "" disable = "false" info = "" >
            //      [15:43:58.341] < block atomic = "false" info = "" >
            //      [15:43:58.341]      
            w.write32(0xE000EDF0, 0xA05F0001);                                        // Enable Core Debug via DHCSR
                                                                                      //[15:43:58.342]        // -> [Write32(0xE000EDF0, 0xA05F0001)] (__dp=0, __ap=0)
                                                                                      //[15:43:58.342] Write32(0xE0042004, DbgMCU_CR);                                         // DBGMCU_CR: Configure MCU Debug
                                                                                      // [15:43:58.342]        // -> [Write32(0xE0042004, 0x00000007)] (__dp=0, __ap=0)
            w.write32(0xE0042004, 0x00000007);
            //[15:43:58.342] Write32(0xE0042008, DbgMCU_APB1_Fz);                                    // DBGMCU_APB1_FZ: Configure APB1 Peripheral Freeze Behavior
            //[15:43:58.343]        // -> [Write32(0xE0042008, 0x00000000)] (__dp=0, __ap=0)
            w.write32(0xE0042008, 0x00000000);
            //[15:43:58.343] Write32(0xE004200C, DbgMCU_APB2_Fz);                                    // DBGMCU_APB1_FZ: Configure APB2 Peripheral Freeze Behavior
            w.write32(0xE004200C, 0x00000000);
            //[15:43:58.343]        // -> [Write32(0xE004200C, 0x00000000)] (__dp=0, __ap=0)
            //[15:43:58.343]    </block>
            //[15:43:58.344]  </sequence>
        
        
            //#define PERIPH_BASE            0x40000000U /*!< Base address of : AHB/ABP Peripherals                                                   */
            // AHB1PERIPH_BASE       (PERIPH_BASE + 0x00020000U)
            // RCC_BASE              (AHB1PERIPH_BASE + 0x3800U)
            /* Reset the RCC clock configuration to the default reset state ------------*/
            /*!< RCC clock control register,                                  Address offset: 0x00 */
            /* Set HSION bit */
            // w.write32(0x40023800, 1);// RCC->CR |= (uint32_t)0x00000001;
            // /* Reset CFGR register RCC clock configuration register,                            Address offset: 0x08 */
            // w.write32(0x40023808, 0); //RCC->CFGR = 0x00000000;
            //                           // /* Reset HSEON, CSSON and PLLON bits */
            // w.write32(0x40023800, 0xFEF6FFFF); //RCC->CR &= (uint32_t)0xFEF6FFFF;
            //                                    /* Reset PLLCFGR register RCC PLL configuration register,                              Address offset: 0x04 */
            // 
            // w.write32(0x40023804, 0x24003010); // RCC->PLLCFGR = 0x24003010;
            //                                    /* Reset HSEBYP bit */
            // w.write32(0x40023800, w.read32(0x40023804)() & 0xFFFBFFFF); //// RCC->CR &= (uint32_t)0xFFFBFFFF;
            //                                                             /* Disable all interrupts RCC clock interrupt register,                                Address offset: 0x0C */
            // 
            // w.write32(0x4002380C, 0); // RCC->CIR = 0x00000000;
        
        
        
            // 0xE0042000 DEBUG_MCU base 
            // 0x08 APB1FZ 
            w.write32(0xE0042008, 0); // DbgMCU_APB1_Fz = 0x00000000;
            w.write32(0xE004200C, 0); // DbgMCU_APB2_Fz = 0x00000000;
        
            //UInt32 scbVtor = w.read32(0xE000ED08)();
            // 0xE000ED08 // SCB->VTOR vector table offset
            w.write32(0xE000ED08, 0x20010000);
        
            {
                /////
                //// #define SCB_SHCSR_MEMFAULTENA_Pos          16U                                            /*!< SCB SHCSR: MEMFAULTENA Position */
                //// #define SCB_SHCSR_MEMFAULTENA_Msk          (1UL << SCB_SHCSR_MEMFAULTENA_Pos)             /*!< SCB SHCSR: MEMFAULTENA Mask */
                //// SHCSR;                  /*!< Offset: 0x024 (R/W)  System Handler Control and State Register */
                //unchecked
                //{
                //    w.write32(0xE000ED24, (UInt32)~(1 << 16));
                //}
                //w.write32(0xE000ED94, 0); // MPU->CR disable
                UInt32 mpuEnabled = w.read32(0xE000ED94)();
            }
        
        
        
        
            w.write32(0xE0042004, 0x00000007 );
            //w.write32(0xE0042004, 0x00000027);
        
            // # Stop watchdog counters during halt
            // # DBGMCU_APB1_FZ |= DBG_IWDG_STOP | DBG_WWDG_STOP
            //w.write32(0xE0042008, 0x00001800 );
            //w.write32(0xE004200C, 0x00001800);
        
            {
                // 0xE000EDF0 Core Debug base
                // 0x00C DEMCR
                // Core Debug -> DEMCR & ~ (1 << DEMCR_MON_EN_Pos )  bit offset 16
                UInt32 demcr = w.read32(0xE000EDFC)();
                demcr &= ~((UInt32)1<<16);
                w.write32(0xE000EDFC, demcr);
            }

            //Flash.Flash flash = new Targets.Target_STM32F7x_1024.Flash_STM32F7x_1024(w);
            Flash.Flash flash = new Targets.Target_W7500.Flash_w7500(w);
            flash.setFlashAlgoDebug(false); // flash.setFlashAlgoDebug(true); 
            
            w.setFlash(flash);
            {
                w.flash.init();
                Stopwatch sw = new Stopwatch();
                sw.Start();
                w.flash.eraseAll();
                sw.Stop();
                // Trace.TraceInformation("Chip erase speed is {0:0.000} s", sw.Elapsed.TotalSeconds);
            }
            {
                List<byte> l = new List<byte>();
                Stopwatch sw = new Stopwatch();
                sw.Start();
                //l.AddRange(w.readBlockMemoryUnaligned8(0x00000000, 49508));
                l.AddRange(w.readBlockMemoryUnaligned8(0x00000000, 128*1024));
                sw.Stop();
                Trace.TraceInformation("Reading speed is {0:0.000} kB/s", ((double)128 * 1024 / 1024.0) / sw.Elapsed.TotalSeconds);
                 if (l.Any(b => b != 0xFF))
                 {
                     Trace.TraceError("Erasing failed ..");
                 };
                //byte[] bytes = l.ToArray();
                //File.WriteAllBytes(@"C:\temp\flash.bin", bytes);
            }
            
            // 
            byte[] bytes = File.ReadAllBytes(@"c:\temp\flash.bin");
            {
                w.flash.flashBinary(
                    bytes,
                    smart_flash: false,
                    chip_erase: true, // meaning that chip erase is already done
                    fast_verify: false);
            }
            
 
            {
                List<byte> l = new List<byte>();
                Stopwatch sw = new Stopwatch();
                sw.Start();
                l.AddRange(w.readBlockMemoryUnaligned8(0x00000000, (UInt32)bytes.Length));
                //l.AddRange(w.readBlockMemoryUnaligned8(0x08000000, (UInt32)bytes.Length));
                sw.Stop();
                Trace.TraceInformation("Reading speed is {0:0.000} kB/s", ((double)bytes.Length / 1024.0) / sw.Elapsed.TotalSeconds);
                var ar = l.ToArray();
                File.WriteAllBytes(@"C:\TEMP\BIN.BIN", ar);
            }
            /*

            //var i = w.isRunning();
            // w.setBreakpoint(0x00007840);
            // w.resume();
            // while (!w.isHalted())
            // {
            //     Trace.TraceInformation("... waiting for breakpoint ...");
            //     //Thread.Sleep(1);
            // }
            //w.flush();
            //var isRunning = w.isRunning();
            //Debug.Assert(!isRunning);
            //w.halt();
            //var isHalted = w.isHalted();
            //Debug.Assert(isHalted);
            // w.removeBreakpoint(0x00007840);
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                //// for (int i = 0; i < 1000; i++)
                //// {
                ////     //var x = CoreSight.CortexM.CORE_REGISTER;
                ////     UInt32 pc = w.readCoreRegister("pc");
                ////     //Trace.TraceInformation("pc: {0:X8}", pc);
                ////     w.step(true);
                //// }
                double totalBytes = 0;
                for (int i = 0; i < 10; i++)
                {
                    List<UInt32> z = w.readBlockMemoryAligned32(0x20000000, 0x1000);
                    totalBytes += z.Count * 4;
                }
                sw.Stop();
                Trace.TraceInformation("Reading speed is {0:0.000} bytes/s", totalBytes / sw.Elapsed.TotalSeconds);
            }
            //var i = w.isRunning();
            */
        }
    }
}
