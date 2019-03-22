/***********************************************************************/
/*  This file is part of the uVision/ARM development tools             */
/*  Copyright (c) 2011-2013 Keil - An ARM Company.                     */
/*  All rights reserved.                                               */
/***********************************************************************/
/*                                                                     */
/*  FlashDev.C:  Device Description for                                */
/*               STMicroelectronics STM32F4xx Flash                    */
/*                                                                     */
/***********************************************************************/

#include "FlashOS.H"        // FlashOS Structures

#ifdef STM32F7x_2048


struct FlashDevice const FlashDevice  =  {
   FLASH_DRV_VERS,             // Driver Version, do not modify!
   "STM32F7x 2MB Flash",       // Device Name (1024kB)
   ONCHIP,                     // Device Type
   0x08000000,                 // Device Start Address
   0x00200000,                 // Device Size in Bytes (1024kB)
   512,                        // Programming Page Size
   0,                          // Reserved, must be 0
   0xFF,                       // Initial Content of Erased Memory
   1000,                       // Program Page Timeout 1000 mSec
   6000,                       // Erase Sector Timeout 6000 mSec

// Specify Size and Address of Sectors
   0x08000, 0x000000,          // Sector Size  32kB (4 Sectors)
   0x20000, 0x020000,          // Sector Size 128kB (1 Sectors)
   0x40000, 0x040000,          // Sector Size 256kB (3 Sectors)
   SECTOR_END
};

#endif // STM32F7x_2048
#ifdef STM32F7x_2048dual


struct FlashDevice const FlashDevice  =  {
   FLASH_DRV_VERS,             // Driver Version, do not modify!
   "STM32F7x dual bank 2MB Flash",       // Device Name (1024kB)
   ONCHIP,                     // Device Type
   0x08000000,                 // Device Start Address
   0x00200000,                 // Device Size in Bytes (1024kB)
   512,                        // Programming Page Size
   0,                          // Reserved, must be 0
   0xFF,                       // Initial Content of Erased Memory
   1000,                       // Program Page Timeout 1000 mSec
   6000,                       // Erase Sector Timeout 6000 mSec

// Specify Size and Address of Sectors
   0x04000, 0x000000,          // Sector Size  16kB (4 Sectors)
   0x10000, 0x010000,          // Sector Size  64kB (1 Sectors)
   0x20000, 0x020000,          // Sector Size 128kB (7 Sectors)
   0x04000, 0x100000,          // Sector Size  16kB (4 Sectors)
   0x10000, 0x110000,          // Sector Size  64kB (1 Sectors)
   0x20000, 0x120000,          // Sector Size 128kB (7 Sectors)
   SECTOR_END
};

#endif // STM32F7x_2048dual
/////////////
#ifdef STM32F7xTCM_2048


struct FlashDevice const FlashDevice  =  {
   FLASH_DRV_VERS,             // Driver Version, do not modify!
   "STM32F7x TCM 2MB Flash",       // Device Name (1024kB)
   ONCHIP,                     // Device Type
   0x00200000,                 // Device Start Address
   0x00200000,                 // Device Size in Bytes (1024kB)
   512,                        // Programming Page Size
   0,                          // Reserved, must be 0
   0xFF,                       // Initial Content of Erased Memory
   1000,                       // Program Page Timeout 1000 mSec
   6000,                       // Erase Sector Timeout 6000 mSec

// Specify Size and Address of Sectors
   0x08000, 0x000000,          // Sector Size  32kB (4 Sectors)
   0x20000, 0x020000,          // Sector Size 128kB (1 Sectors)
   0x40000, 0x040000,          // Sector Size 256kB (3 Sectors)
   SECTOR_END
};

#endif // STM32F7xTCM_2048

#ifdef STM32F7xTCM_2048dual
struct FlashDevice const FlashDevice  =  {
   FLASH_DRV_VERS,             // Driver Version, do not modify!
   "STM32F7x TCM dual bank 2MB Flash",       // Device Name (1024kB)
   ONCHIP,                     // Device Type
   0x00200000,                 // Device Start Address
   0x00200000,                 // Device Size in Bytes (1024kB)
   512,                        // Programming Page Size
   0,                          // Reserved, must be 0
   0xFF,                       // Initial Content of Erased Memory
   1000,                       // Program Page Timeout 1000 mSec
   6000,                       // Erase Sector Timeout 6000 mSec

// Specify Size and Address of Sectors
   0x04000, 0x000000,          // Sector Size  16kB (4 Sectors)
   0x10000, 0x010000,          // Sector Size  64kB (1 Sectors)
   0x20000, 0x020000,          // Sector Size 128kB (7 Sectors)
   0x04000, 0x100000,          // Sector Size  16kB (4 Sectors)
   0x10000, 0x110000,          // Sector Size  64kB (1 Sectors)
   0x20000, 0x120000,          // Sector Size 128kB (7 Sectors)
   SECTOR_END
};

#endif // STM32F7xTCM_2048dual
/////////////
#ifdef STM32F7x_1024


struct FlashDevice const FlashDevice  =  {
   FLASH_DRV_VERS,             // Driver Version, do not modify!
   "STM32F7x 1MB Flash",       // Device Name (1024kB)
   ONCHIP,                     // Device Type
   0x08000000,                 // Device Start Address
   0x00100000,                 // Device Size in Bytes (1024kB)
   512,                        // Programming Page Size
   0,                          // Reserved, must be 0
   0xFF,                       // Initial Content of Erased Memory
   1000,                       // Program Page Timeout 1000 mSec
   6000,                       // Erase Sector Timeout 6000 mSec

// Specify Size and Address of Sectors
   0x08000, 0x000000,          // Sector Size  32kB (4 Sectors)
   0x20000, 0x020000,          // Sector Size 128kB (1 Sectors)
   0x40000, 0x040000,          // Sector Size 256kB (3 Sectors)
   SECTOR_END
};

#endif // STM32F7x_1024


#ifdef STM32F7x_1024dual


struct FlashDevice const FlashDevice  =  {
   FLASH_DRV_VERS,             // Driver Version, do not modify!
   "STM32F7x dual bank 1MB Flash",       // Device Name (1024kB)
   ONCHIP,                     // Device Type
   0x08000000,                 // Device Start Address
   0x00100000,                 // Device Size in Bytes (1024kB)
   512,                        // Programming Page Size
   0,                          // Reserved, must be 0
   0xFF,                       // Initial Content of Erased Memory
   1000,                       // Program Page Timeout 1000 mSec
   6000,                       // Erase Sector Timeout 6000 mSec

// Specify Size and Address of Sectors
   0x04000, 0x000000,          // Sector Size  16kB (4 Sectors)
   0x10000, 0x010000,          // Sector Size  64kB (1 Sectors)
   0x20000, 0x020000,          // Sector Size 128kB (3 Sectors)		
   0x04000, 0x080000,          // Sector Size  16kB (4 Sectors)
   0x10000, 0x090000,          // Sector Size  64kB (1 Sectors)
   0x20000, 0x0A0000,          // Sector Size 128kB (3 Sectors)	
	
	
   SECTOR_END
};

#endif // STM32F7x_1024dual








#ifdef STM32F7x_512


struct FlashDevice const FlashDevice  =  {
   FLASH_DRV_VERS,             // Driver Version, do not modify!
   "STM32F7x 512KB Flash",      // Device Name (512kB)
   ONCHIP,                     // Device Type
   0x08000000,                 // Device Start Address
   0x00080000,                 // Device Size in Bytes (512kB)
   512,                        // Programming Page Size
   0,                          // Reserved, must be 0
   0xFF,                       // Initial Content of Erased Memory
   1000,                       // Program Page Timeout 1000 mSec
   6000,                       // Erase Sector Timeout 6000 mSec

// Specify Size and Address of Sectors
   0x08000, 0x000000,          // Sector Size  32kB (4 Sectors)
   0x20000, 0x020000,          // Sector Size 128kB (1 Sectors)
   0x40000, 0x040000,          // Sector Size 256kB (3 Sectors)
   SECTOR_END
};

#endif // STM32F7x_512

#ifdef FLASH_TCM

struct FlashDevice const FlashDevice  =  {
   FLASH_DRV_VERS,             // Driver Version, do not modify!
   "STM32F7xx_Flash_TCM 1MB Flash",      // Device Name (1024kB)
   ONCHIP,                     // Device Type
   0x00200000,                 // Device Start Address
   0x00100000,                 // Device Size in Bytes (1024kB)
   512,                        // Programming Page Size
   0,                          // Reserved, must be 0
   0xFF,                       // Initial Content of Erased Memory
   100,                        // Program Page Timeout 100 mSec
   6000,                       // Erase Sector Timeout 6000 mSec

// Specify Size and Address of Sectors
   0x08000, 0x000000,          // Sector Size  32kB (4 Sectors)
   0x20000, 0x020000,          // Sector Size 128kB (1 Sectors)
   0x40000, 0x040000,          // Sector Size 256kB (3 Sectors)
   SECTOR_END
};

#endif // FLASH_TCM
