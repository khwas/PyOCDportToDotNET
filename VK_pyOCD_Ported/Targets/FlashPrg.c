/***********************************************************************/
/*  This file is part of the uVision/ARM development tools             */
/*  Copyright (c) 2011-2013 Keil - An ARM Company.                     */
/*  All rights reserved.                                               */
/***********************************************************************/
/*                                                                     */
/*  FlashPrg.c:  Flash Programming Functions adapted for               */
/*               STMicroelectronics STM32F4xx Flash                    */
/*                                                                     */
/***********************************************************************/

#include "FlashOS.H"        // FlashOS Structures

typedef volatile unsigned char    vu8;
typedef          unsigned char     u8;
typedef volatile unsigned short   vu16;
typedef          unsigned short    u16;
typedef volatile unsigned long    vu32;
typedef          unsigned long     u32;

#define M8(adr)  (*((vu8  *) (adr)))
#define M16(adr) (*((vu16 *) (adr)))
#define M32(adr) (*((vu32 *) (adr)))

// Peripheral Memory Map
#define IWDG_BASE         0x40003000
#define FLASH_BASE        0x40023C00

#define IWDG            ((IWDG_TypeDef *) IWDG_BASE)
#define FLASH           ((FLASH_TypeDef*) FLASH_BASE)

// Independent WATCHDOG
typedef struct {
  vu32 KR;
  vu32 PR;
  vu32 RLR;
  vu32 SR;
} IWDG_TypeDef;

// Flash Registers
typedef struct {
  vu32 ACR;
  vu32 KEYR;
  vu32 OPTKEYR;
  vu32 SR;
  vu32 CR;
  vu32 OPTCR;
} FLASH_TypeDef;


// Flash Keys
#define RDPRT_KEY       0x00A5
#define FLASH_KEY1      0x45670123
#define FLASH_KEY2      0xCDEF89AB
#define FLASH_OPTKEY1   0x08192A3B
#define FLASH_OPTKEY2   0x4C5D6E7F

// Flash Control Register definitions
#define FLASH_PG                ((unsigned int)0x00000001)
#define FLASH_SER               ((unsigned int)0x00000002)
#define FLASH_MER               ((unsigned int)0x00000004)
#define FLASH_SNB_POS           ((unsigned int)0x00000003)
#define FLASH_SNB_MSK           ((unsigned int)0x000000F8)
#define FLASH_PSIZE_POS         ((unsigned int)0x00000008)
#define FLASH_PSIZE_MSK         ((unsigned int)0x00000300)
#define FLASH_MERB              ((unsigned int)0x00008000)
#define FLASH_STRT              ((unsigned int)0x00010000)
#define FLASH_LOCK              ((unsigned int)0x80000000)

// Flash Option Control Register definitions
#define FLASH_OPTLOCK           ((unsigned int)0x00000001)
#define FLASH_OPTSTRT           ((unsigned int)0x00000002)


#define FLASH_PSIZE_Byte        ((unsigned int)0x00000000)
#define FLASH_PSIZE_HalfWord    ((unsigned int)0x00000100)
#define FLASH_PSIZE_Word        ((unsigned int)0x00000200)
#define FLASH_PSIZE_DoubleWord  ((unsigned int)0x00000300)
#define CR_PSIZE_MASK           ((unsigned int)0xFFFFFCFF)


// Flash Status Register definitions
#define FLASH_EOP               ((unsigned int)0x00000001)
#define FLASH_OPERR             ((unsigned int)0x00000002)
#define FLASH_WRPERR            ((unsigned int)0x00000010)
#define FLASH_PGAERR            ((unsigned int)0x00000020)
#define FLASH_PGPERR            ((unsigned int)0x00000040)
#define FLASH_PGSERR            ((unsigned int)0x00000080)
#define FLASH_BSY               ((unsigned int)0x00010000)

#define FLASH_PGERR             (FLASH_PGSERR | FLASH_PGPERR | FLASH_PGAERR | FLASH_WRPERR)

void BKPT(void) {
    __asm("BKPT #0");
}

void DSB(void) {
    __asm("DSB");
}


/*
 * Get Sector Number
 *    Parameter:      adr:  Sector Address
 *    Return Value:   Sector Number
 */
#if defined(STM32F7x_2048dual) || defined(STM32F7xTCM_2048dual)
unsigned long GetSecNum (unsigned long adr) {
  unsigned long n; 
  n = (adr >> 12) & 0x000FF;                            // only bits 12..19
	
  if    (n >= 0x20) {
    n = 4 + (n >> 5);                                    // 128kB Sector
  }
  else if (n >= 0x10) {
    n = 3 + (n >> 4);                                    //  64kB Sector
  }
  else                {
    n = 0 + (n >> 2);                                    //  16kB Sector
  }

  if (adr & 0x00100000)
    n |=0x00000010;                                      // sector in second half
	 return (n);                                            // Sector Number

   
}

//#endif

#elif defined(STM32F7x_2048) || defined(STM32F7xTCM_2048)
unsigned long GetSecNum (unsigned long adr) {
  unsigned long n;

  n = (adr >> 12) & 0x00FFF;                            // only bits 8..19
	  if    (n >= 0x3F) {
    n = 4 + (n >> 6);                                   // 128 and 256kB Sectors
  }
  else                {
    n = 0 + (n >> 3);                                   //  32kB Sectors
  }

  return (n);   
}


#elif defined(STM32F7x_1024dual) 
unsigned long GetSecNum (unsigned long adr) {
  unsigned long n;

  n = (adr >> 14) & 0x000FF;                            

	  if   (n >= 0x24) 
		{
       n = 12 + (n >> 3);                                   
    }        			 			
    else if (n >= 0x20) 
		{
			n = 12 + (n & 0x0000F);  
		}
	  else if (n >= 0x08)
		{
			n = 4 + ( n >> 3);    
		}	
	  else if (n >= 0x04)
		{
			n = 4 ;    
		}			
	  else 
		{
    n = n;                                   
    }

  return (n);   
}





#else
//#endif
unsigned long GetSecNum (unsigned long adr) {
  unsigned long n;

  n = (adr >> 12) & 0x000FF;                            // only bits 12..19
                                        // Sector Number
	if    (n >= 0x40) {
    n = 4 + (n >> 6);                                   // 256kB Sectors
  }
  else                {
    n = 0 + (n >> 3);                                   //  128KB and 32kB Sectors
  }

  return (n);   
}
#endif

/*
 *  Initialize Flash Programming Functions
 *    Parameter:      adr:  Device Base Address
 *                    clk:  Clock Frequency (Hz)
 *                    fnc:  Function Code (1 - Erase, 2 - Program, 3 - Verify)
 *    Return Value:   0 - OK,  1 - Failed
 */

int Init (unsigned long adr, unsigned long clk, unsigned long fnc) {
 
  FLASH->KEYR = FLASH_KEY1;                             // Unlock Flash
  FLASH->KEYR = FLASH_KEY2;
  FLASH->ACR  = 0x00000000;                             // Zero Wait State, no Cache, no Prefetch
  FLASH->SR  |= FLASH_PGERR;                            // Reset Error Flags

  if ((FLASH->OPTCR & 0x20) == 0x00) {                  // Test if IWDG is running (IWDG in HW mode)
    // Set IWDG time out to ~32.768 second
    IWDG->KR  = 0x5555;                                 // Enable write access to IWDG_PR and IWDG_RLR     
    IWDG->PR  = 0x06;                                   // Set prescaler to 256  
    IWDG->RLR = 4095;                                   // Set reload value to 4095
  }

  return (0);
}



/*
 *  De-Initialize Flash Programming Functions
 *    Parameter:      fnc:  Function Code (1 - Erase, 2 - Program, 3 - Verify)
 *    Return Value:   0 - OK,  1 - Failed
 */

int UnInit (unsigned long fnc) {

  FLASH->CR |=  FLASH_LOCK;                             // Lock Flash
  return (0);
}

/*
 *  Erase complete Flash Memory
 *    Return Value:   0 - OK,  1 - Failed
 */

int EraseChip (void) {
	FLASH->CR |=  FLASH_MER; 
	
	#if defined(STM32F7x_2048dual) || defined(STM32F7xTCM_2048dual)
	FLASH->CR |=  FLASH_MERB; 
#endif
  FLASH->CR |=  FLASH_STRT;                             // Start Erase

  while (FLASH->SR & FLASH_BSY) {
    IWDG->KR = 0xAAAA;                                  // Reload IWDG
  }

  FLASH->CR &= ~FLASH_MER;                              // Mass Erase Disabled


  return (0);                                           // Done
}

/*
 *  Erase Sector in Flash Memory
 *    Parameter:      adr:  Sector Address
 *    Return Value:   0 - OK,  1 - Failed
 */

#ifdef FLASH_MEM
int EraseSector (unsigned long adr) {
  unsigned long n;

  n = GetSecNum(adr);                                   // Get Sector Number
  FLASH->SR |= FLASH_PGERR;                             // Reset Error Flags

  FLASH->CR  =  FLASH_SER;                              // Sector Erase Enabled 
  FLASH->CR |=  ((n << FLASH_SNB_POS) & FLASH_SNB_MSK); // Sector Number
  FLASH->CR |=  FLASH_STRT;                             // Start Erase

  while (FLASH->SR & FLASH_BSY) {
    IWDG->KR = 0xAAAA;                                  // Reload IWDG
  }

  FLASH->CR &= ~FLASH_SER;                              // Page Erase Disabled 

  if (FLASH->SR & FLASH_PGERR) {                        // Check for Error
    FLASH->SR |= FLASH_PGERR;                           // Reset Error Flags
    return (1);                                         // Failed
  }
  return (0);                                           // Done
}
#endif // FLASH_MEM


#if defined(FLASH_TCM) || defined(STM32F7xTCM_2048) || defined(STM32F7xTCM_2048dual)
int EraseSector (unsigned long adr) {
  unsigned long n;

  n = GetSecNum(0x08000000+(adr-0x00200000));           // Get Sector Number
  FLASH->SR |= FLASH_PGERR;                             // Reset Error Flags

  FLASH->CR  =  FLASH_SER;                              // Sector Erase Enabled 
  FLASH->CR |=  ((n << FLASH_SNB_POS) & FLASH_SNB_MSK); // Sector Number
  FLASH->CR |=  FLASH_STRT;                             // Start Erase

  while (FLASH->SR & FLASH_BSY) {
    IWDG->KR = 0xAAAA;                                  // Reload IWDG
  }

  FLASH->CR &= ~FLASH_SER;                              // Page Erase Disabled 

  if (FLASH->SR & FLASH_PGERR) {                        // Check for Error
    FLASH->SR |= FLASH_PGERR;                           // Reset Error Flags
    return (1);                                         // Failed
  }
  return (0);                                           // Done
}
#endif // FLASH_TCM

/*
 *  Program Page in Flash Memory
 *    Parameter:      adr:  Page Start Address
 *                    sz:   Page Size
 *                    buf:  Page Data
 *    Return Value:   0 - OK,  1 - Failed
 */
#ifdef FLASH_MEM
int ProgramPage (unsigned long adr, unsigned long sz, unsigned char *buf) {	

  sz = (sz + 3) & ~3;                                   // Adjust size for Words
  FLASH->SR |= FLASH_PGERR;                             // Reset Error Flags
  FLASH->CR  =  0;                                      // reset CR 

  while (sz) {

    FLASH->CR |= (FLASH_PG              |               // Programming Enabled
                  FLASH_PSIZE_Word);                    // Programming Enabled (Word)
		FLASH->OPTCR |= 0x00FF0000;                         // Allow writes to all sectors

    M32(0x08000000 + adr) = *((u32 *)buf);                           // Program Double Word
    DSB();
    while (FLASH->SR & FLASH_BSY){
      IWDG->KR = 0xAAAA;                                // Reload IWDG
    }

    FLASH->CR &= ~FLASH_PG;                             // Programming Disabled

    if (FLASH->SR & FLASH_PGERR) {                      // Check for Error
      FLASH->SR |= FLASH_PGERR;                         // Reset Error Flags
      return (1);                                       // Failed
    }

    adr += 4;                                           // Go to next Word
    buf += 4;
    sz  -= 4;
  }

  return (0);                                           // Done
}
#endif // FLASH_MEM


#if defined(FLASH_TCM) || defined(STM32F7xTCM_2048) || defined(STM32F7xTCM_2048dual)
int ProgramPage (unsigned long adr, unsigned long sz, unsigned char *buf) {	

  sz = (sz + 3) & ~3;                                   // Adjust size for Words
  FLASH->SR |= FLASH_PGERR;                             // Reset Error Flags
  FLASH->CR  =  0;                                      // reset CR 

  while (sz) {

    FLASH->CR |= (FLASH_PG              |               // Programming Enabled
                  FLASH_PSIZE_Word);                    // Programming Enabled (Word)

    M32(0x08000000+(adr-0x200000)) = *((u32 *)buf);     // Program Double Word
    DSB();
    while (FLASH->SR & FLASH_BSY){
      IWDG->KR = 0xAAAA;                                // Reload IWDG
    }

  //  for(;i<1000;i++);
    FLASH->CR &= ~FLASH_PG;                             // Programming Disabled

    if (FLASH->SR & FLASH_PGERR) {                      // Check for Error
      FLASH->SR |= FLASH_PGERR;                         // Reset Error Flags
      return (1);                                       // Failed
    }

    adr += 4;                                           // Go to next Word
    buf += 4;
    sz  -= 4;
  }

  return (0);                                           // Done
}
#endif // FLASH_TCM
