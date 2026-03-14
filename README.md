# Z88dk_128K_memory_visualizer_v1

Version 1

use to visualize routine size and free memory for a 128k C program for Z88dk

This is a specialized tool which is suitable for very specific compiling scenarios.  This is tested only with z88dk v2.4.

I tested this with sdcc_iy but should also work with sdcc_ix.
I do not know if this will work with sccz80.

The way I usually compile is each memory section is compiled seperately.
I seperate my compiles into different folders and sections

The sections are as such

contended  $5DC0 to $7FFF
uncontended $8000 to $BFFF
RAM0 (home ram) $C000 to $FFFF
RAM1 $C000 to $FFFF
RAM3 $C000 to $FFFF
RAM4 $C000 to $FFFF
RAM6 $C000 to $FFFF

note that I don't use RAM2 or RAM5 as I normally avoid these, in the future I might add those, but for now they are excluded.

----------------------------------------------------------------------------------------------

I will in the future might provide a programming example (This will be key to understanding my compiling technique)

----------------------------------------------------------------------------------------------

In each of the RAM banks, I make sure to have a short assembler marker called       ramtop.asm
in each of the RAMTOP.ASM I include the following

SECTION BANK_01
PUBLIC _ram1top
_ram1top:
    defs 0
with a unique marker for each of the RAM banks

each of the RAM BANKS should have the following
ramX.lst, with the X being the specific RAM BANK

The contents of each should be:
ram1.c
BANK1.asm
ramtop.asm
; .lst files commented with semi-colon or hash
# must have blank line after this

notice that the ramtop.asm is last, this is used for seeing the available space.

----------------------------------------------------------------------------------------------

for the contended ramtop.asm, I use the following

SECTION CONTENDED_TOP

PUBLIC _contended_top
_contended_top:
    defs 0
----------------------------------------------------------------------------------------------

for the uncontended ramtop.asm, I use the following:
SECTION BSS_END

PUBLIC _uncontended_top
_uncontended_top:
    defs 0
    
----------------------------------------------------------------------------------------------

When you compile a section, the compiling string is:
zcc +zx -vn -clib=sdcc_iy @ram1.lst -c -o RAM1.o -m

This will create an object file that should be copied to the uncontended folder

once all of the object compiled files are created, merge them all together and use the following compiling string
zcc +zx -vn -m -startup=1 -clib=sdcc_iy -lm ramALL.o -o compiled -pragma-include:zpragma.inc

The zpragma should look something like:
#pragma output __MMAP = -1
// memory map is provided in file "mmap.inc"

#pragma output CRT_ORG_CODE = 32768
// main binary program start        32768

#pragma output REGISTER_SP = 0xC000
// keep stack out of top 16k        0xBFFF  attention

#pragma output CRT_STACK_SIZE = 0xFD
----------------------------------------------------------------------------------------------
The mmap.inc should be something like:

;; Custom memory map
;;
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; memory model
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

;; contended   = 24000 (5DC0) to 32767 (7FFF)
;; uncontended = 32768 (8000) to 49151 (BFFF)
;; bankable    = 49152 (C000) to 65535 (FFFF)
;; bankable apply to 0, 1, 3, 4, 6
;; bank 2 for 8000 to C000 and can mess with program flow
;; banks 5, 7 are for screen flipping

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; memory model
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

SECTION BANK_00
org 0xc000		;	49152

SECTION BANK_01
org 0xc000		;	49152

SECTION BANK_03
org 0xc000		;	49152

SECTION BANK_04
org 0xc000		;	49152

SECTION BANK_06
org 0xc000		;	49152

;;-------------------------------------

SECTION UNCONTENDED
org 0x8000      ;	32768

;;  SECTION BSS_END
SECTION IM2_VECTOR_PLACEMENT
org 0xBDBD      ;   48573

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

SECTION CONTENDED
org 0x5ED8      ;	24280   0x5E88
SECTION CONTENDED_TOP

;;	$BFFF = 49151 is very top end of RAM that does not get banked
;;	$c000 = 49152 very bottom end of RAM that is Bankable

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;
;; sections defined
;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

;;placing stack at 0xBFFE for FF bytes
;so stack runs for 0xBEFF to BFFE


;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

;SECTION data_user
;;INCLUDEs are at the bottom

;SECTION code_user
; code_user is for read-only code

;SECTION bss_user
; bss_user is for zeroed ram variables

;SECTION data_user
; data_user is for initially non-zero ram variables

;SECTION smc_user
; smc_user is for self-modifying code

;SECTION rodata_user
; rodata_user is for constant data

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

SECTION CODE
org __crt_org_code






section code_crt_init
section code_crt_main
section code_crt_exit
section code_crt_return
section code_crt_common

section code_driver
section code_font
section code_clib
  include "../../clib_code.inc"
section code_lib
section code_compiler
section code_user

section rodata_driver
section rodata_font
section rodata_clib
  include "../../clib_rodata.inc"
  ;;section rodata_error_strings
  ;;section rodata_error_string_end
  ;;defb 0
section rodata_lib
section rodata_compiler
section rodata_user

SECTION CODE_END

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

SECTION DATA

IF __crt_org_data

   org __crt_org_data

ELSE

   IF __crt_model

      "DATA section address must be specified for rom models"

   ENDIF

ENDIF

defb 0

section smc_driver
section smc_font
section smc_clib
  include "../../clib_smc.inc"
section smc_lib
section smc_compiler
section smc_user

section data_driver
section data_font
section data_clib
  include "../../clib_data.inc"
  ;;section data_fcntl_stdio_heap_head
  ;;section data_fcntl_stdio_heap_body
  ;;section data_fcntl_stdio_heap_tail
  ;;section data_fcntl_fdtable_body
section data_lib
section data_compiler
section data_user

SECTION DATA_END

;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

SECTION BSS

IF __crt_org_bss

   org __crt_org_bss

ELSE

   IF __crt_model

      org -1

   ENDIF

ENDIF

defb 0

section BSS_UNINITIALIZED

section bss_driver
section bss_font
section bss_clib
  include "../../clib_bss.inc"
section bss_lib
section bss_compiler
section bss_user

SECTION BSS_END

;; end memory model ;;;;;;;;;;;;;;;;;;;;;;;;;;;;;;

----------------------------------------------------------------------------------------------

Once a program is compiled, you should get a .MAP file at the root directory of the program you are writing.

----------------------------------------------------------------------------------------------

Launch the z88dk memory visualizer

Load the memory map (the map needs to have .map at the end)
Wait a few moments as the program parces the map file
You will now have a list of each of the routines and tabs at the top to view each of the routines in the area they are programmed.
The tabs are lableled CONTENDED UNCONTENDED RAM0 RAM1 RAM3 RAM4 RAM6 and LIBRARY

Select the Visual Memory button

You will see 7 bars for each of the memory sections

If you want to drill deeper with zoom, Right click  on a memory section and Select Zoom: XXXX
right click on a memory chunk and use your mouse wheel to zoom in and out.

