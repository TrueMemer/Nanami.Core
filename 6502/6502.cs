using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nanami
{
    public class CPU
    {
        // Регистры
        private byte A;
        private byte X, Y;
        public UInt16 PC;
        private byte SP;

        public byte[] RAM = new byte[65536];

        // Флаги
        private byte C = 0; // carry
        private byte Z = 0; // zero
        private byte I = 0; // interrupt disable
        private byte D = 0; // decimal mode
        private byte B = 0; // break command
        private byte U = 1; // unused
        private byte V = 0; // overflow
        private byte N = 0; // negative

        public UInt64 Cycles;
        public int Stall;
        private InterruptType Interrupt;

        // Информация, которая нужна при выполнении инструкций
        private struct StepInfo
        {
            public UInt16 Address;
            public UInt16 PC;
            public byte Mode;
        }

        // Типы прерываний
        private enum InterruptType
        {
            None,
            NMI,
            IRQ
        }

        private enum AddressingMode : int
        {
            None,
            Absolute,
            AbsoluteX,
            AbsoluteY,
            Acculumator,
            Immediate,
            Implied,
            IndexedIndirect,
            Indirect,
            IndirectIndexed,
            Relative,
            ZeroPage,
            ZeroPageX,
            ZeroPageY
        }


        // instructionModes indicates the addressing mode for each instruction
        byte[] instructionModes = new byte[256] {
            6, 7, 6, 7, 11, 11, 11, 11, 6, 5, 4, 5, 1, 1, 1, 1,
            10, 9, 6, 9, 12, 12, 12, 12, 6, 3, 6, 3, 2, 2, 2, 2,
            1, 7, 6, 7, 11, 11, 11, 11, 6, 5, 4, 5, 1, 1, 1, 1,
            10, 9, 6, 9, 12, 12, 12, 12, 6, 3, 6, 3, 2, 2, 2, 2,
            6, 7, 6, 7, 11, 11, 11, 11, 6, 5, 4, 5, 1, 1, 1, 1,
            10, 9, 6, 9, 12, 12, 12, 12, 6, 3, 6, 3, 2, 2, 2, 2,
            6, 7, 6, 7, 11, 11, 11, 11, 6, 5, 4, 5, 8, 1, 1, 1,
            10, 9, 6, 9, 12, 12, 12, 12, 6, 3, 6, 3, 2, 2, 2, 2,
            5, 7, 5, 7, 11, 11, 11, 11, 6, 5, 6, 5, 1, 1, 1, 1,
            10, 9, 6, 9, 12, 12, 13, 13, 6, 3, 6, 3, 2, 2, 3, 3,
            5, 7, 5, 7, 11, 11, 11, 11, 6, 5, 6, 5, 1, 1, 1, 1,
            10, 9, 6, 9, 12, 12, 13, 13, 6, 3, 6, 3, 2, 2, 3, 3,
            5, 7, 5, 7, 11, 11, 11, 11, 6, 5, 6, 5, 1, 1, 1, 1,
            10, 9, 6, 9, 12, 12, 12, 12, 6, 3, 6, 3, 2, 2, 2, 2,
            5, 7, 5, 7, 11, 11, 11, 11, 6, 5, 6, 5, 1, 1, 1, 1,
            10, 9, 6, 9, 12, 12, 12, 12, 6, 3, 6, 3, 2, 2, 2, 2,
        };

        // instructionSizes indicates the size of each instruction in bytes
        byte[] instructionSizes = new byte[256]{
            1, 2, 0, 0, 2, 2, 2, 0, 1, 2, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 3, 1, 0, 3, 3, 3, 0,
            3, 2, 0, 0, 2, 2, 2, 0, 1, 2, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 3, 1, 0, 3, 3, 3, 0,
            1, 2, 0, 0, 2, 2, 2, 0, 1, 2, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 3, 1, 0, 3, 3, 3, 0,
            1, 2, 0, 0, 2, 2, 2, 0, 1, 2, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 3, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 0, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 3, 1, 0, 0, 3, 0, 0,
            2, 2, 2, 0, 2, 2, 2, 0, 1, 2, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 3, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 2, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 3, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 2, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 3, 1, 0, 3, 3, 3, 0,
        };

        // instructionCycles indicates the number of cycles used by each instruction,
        // not including conditional cycles
        byte[] instructionCycles = new byte[256] {
            7, 6, 2, 8, 3, 3, 5, 5, 3, 2, 2, 2, 4, 4, 6, 6,
            2, 5, 2, 8, 4, 4, 6, 6, 2, 4, 2, 7, 4, 4, 7, 7,
            6, 6, 2, 8, 3, 3, 5, 5, 4, 2, 2, 2, 4, 4, 6, 6,
            2, 5, 2, 8, 4, 4, 6, 6, 2, 4, 2, 7, 4, 4, 7, 7,
            6, 6, 2, 8, 3, 3, 5, 5, 3, 2, 2, 2, 3, 4, 6, 6,
            2, 5, 2, 8, 4, 4, 6, 6, 2, 4, 2, 7, 4, 4, 7, 7,
            6, 6, 2, 8, 3, 3, 5, 5, 4, 2, 2, 2, 5, 4, 6, 6,
            2, 5, 2, 8, 4, 4, 6, 6, 2, 4, 2, 7, 4, 4, 7, 7,
            2, 6, 2, 6, 3, 3, 3, 3, 2, 2, 2, 2, 4, 4, 4, 4,
            2, 6, 2, 6, 4, 4, 4, 4, 2, 5, 2, 5, 5, 5, 5, 5,
            2, 6, 2, 6, 3, 3, 3, 3, 2, 2, 2, 2, 4, 4, 4, 4,
            2, 5, 2, 5, 4, 4, 4, 4, 2, 4, 2, 4, 4, 4, 4, 4,
            2, 6, 2, 8, 3, 3, 5, 5, 2, 2, 2, 2, 4, 4, 6, 6,
            2, 5, 2, 8, 4, 4, 6, 6, 2, 4, 2, 7, 4, 4, 7, 7,
            2, 6, 2, 8, 3, 3, 5, 5, 2, 2, 2, 2, 4, 4, 6, 6,
            2, 5, 2, 8, 4, 4, 6, 6, 2, 4, 2, 7, 4, 4, 7, 7,
        };

        // instructionPageCycles indicates the number of cycles used by each
        // instruction when a page is crossed
        byte[] instructionPageCycles = new byte[256]{
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 0, 1, 0, 0, 0, 0, 0, 1, 0, 1, 1, 1, 1, 1,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 0,
        };

        // instructionNames indicates the name of each instruction
        string[] instructionNames = new string[256] {
            "BRK", "ORA", "KIL", "SLO", "NOP", "ORA", "ASL", "SLO",
            "PHP", "ORA", "ASL", "ANC", "NOP", "ORA", "ASL", "SLO",
            "BPL", "ORA", "KIL", "SLO", "NOP", "ORA", "ASL", "SLO",
            "CLC", "ORA", "NOP", "SLO", "NOP", "ORA", "ASL", "SLO",
            "JSR", "AND", "KIL", "RLA", "BIT", "AND", "ROL", "RLA",
            "PLP", "AND", "ROL", "ANC", "BIT", "AND", "ROL", "RLA",
            "BMI", "AND", "KIL", "RLA", "NOP", "AND", "ROL", "RLA",
            "SEC", "AND", "NOP", "RLA", "NOP", "AND", "ROL", "RLA",
            "RTI", "EOR", "KIL", "SRE", "NOP", "EOR", "LSR", "SRE",
            "PHA", "EOR", "LSR", "ALR", "JMP", "EOR", "LSR", "SRE",
            "BVC", "EOR", "KIL", "SRE", "NOP", "EOR", "LSR", "SRE",
            "CLI", "EOR", "NOP", "SRE", "NOP", "EOR", "LSR", "SRE",
            "RTS", "ADC", "KIL", "RRA", "NOP", "ADC", "ROR", "RRA",
            "PLA", "ADC", "ROR", "ARR", "JMP", "ADC", "ROR", "RRA",
            "BVS", "ADC", "KIL", "RRA", "NOP", "ADC", "ROR", "RRA",
            "SEI", "ADC", "NOP", "RRA", "NOP", "ADC", "ROR", "RRA",
            "NOP", "STA", "NOP", "SAX", "STY", "STA", "STX", "SAX",
            "DEY", "NOP", "TXA", "XAA", "STY", "STA", "STX", "SAX",
            "BCC", "STA", "KIL", "AHX", "STY", "STA", "STX", "SAX",
            "TYA", "STA", "TXS", "TAS", "SHY", "STA", "SHX", "AHX",
            "LDY", "LDA", "LDX", "LAX", "LDY", "LDA", "LDX", "LAX",
            "TAY", "LDA", "TAX", "LAX", "LDY", "LDA", "LDX", "LAX",
            "BCS", "LDA", "KIL", "LAX", "LDY", "LDA", "LDX", "LAX",
            "CLV", "LDA", "TSX", "LAS", "LDY", "LDA", "LDX", "LAX",
            "CPY", "CMP", "NOP", "DCP", "CPY", "CMP", "DEC", "DCP",
            "INY", "CMP", "DEX", "AXS", "CPY", "CMP", "DEC", "DCP",
            "BNE", "CMP", "KIL", "DCP", "NOP", "CMP", "DEC", "DCP",
            "CLD", "CMP", "NOP", "DCP", "NOP", "CMP", "DEC", "DCP",
            "CPX", "SBC", "NOP", "ISC", "CPX", "SBC", "INC", "ISC",
            "INX", "SBC", "NOP", "SBC", "CPX", "SBC", "INC", "ISC",
            "BEQ", "SBC", "KIL", "ISC", "NOP", "SBC", "INC", "ISC",
            "SED", "SBC", "NOP", "ISC", "NOP", "SBC", "INC", "ISC",
        };

        private byte Read(UInt16 address)
        {
            if (address < 0x2000)
            {
                return RAM[address % 0x0800];
            }
            /*
            if (address >= 0x6000)
            {
                return Mapper.Read(address);
            }
            */

            // TODO: Чтение регистров из PPU и APU, для контроллеров и I/O

            return 0;
        }

        private UInt16 Read16(UInt16 address)
        {
            UInt16 lo = Convert.ToUInt16(Read(address));
            UInt16 hi = Convert.ToUInt16(Read(Convert.ToUInt16(address + 1)));

            return (UInt16)(hi << 8 | lo);
        }

        private UInt16 Read16Bug(UInt16 address)
        {
            var a = address;
            var b = (UInt16)((a & 0xFF00) | (UInt16)((byte)a + 1));

            var lo = Read(a);
            var hi = Read(b);

            return (UInt16)((UInt16)(hi) << 8 | (UInt16)(lo));
        }

        private void Write(UInt16 address, byte value)
        {
            if (address < 0x2000)
            {
                RAM[address % 0x0800] = value;
            }

            // TODO: Чтение регистров в PPU и APU, для контроллеров и I/O
        }

        private void Push(byte value)
        {
            Write((UInt16)(0x100 | (UInt16)SP), value);
            SP--;
        }

        private void Push16(UInt16 value)
        {
            var hi = (byte)(value >> 8);
            var lo = (byte)(value & 0xFF);
            Push(hi);
            Push(lo);
        }

        private byte Pull()
        {
            SP++;
            return Read((byte)(0x100 | SP));
        }

        private UInt16 Pull16()
        {
            var lo = (UInt16)(Pull());
            var hi = (UInt16)(Pull());
            return (byte)(hi << 8 | lo);
        }

        public void TriggerNMI()
        {
            Interrupt = InterruptType.NMI;
        }

        private List<Action<StepInfo>> table;

        public void Init()
        {
            // create table
            table = new List<Action<StepInfo>>(256) {
                BRK, ORA, KIL, SLO, NOP, ORA, ASL, SLO,
		        PHP, ORA, ASL, ANC, NOP, ORA, ASL, SLO,
		        BPL, ORA, KIL, SLO, NOP, ORA, ASL, SLO,
		        CLC, ORA, NOP, SLO, NOP, ORA, ASL, SLO,
		        JSR, AND, KIL, RLA, BIT, AND, ROL, RLA,
		        PLP, AND, ROL, ANC, BIT, AND, ROL, RLA,
		        BMI, AND, KIL, RLA, NOP, AND, ROL, RLA,
		        SEC, AND, NOP, RLA, NOP, AND, ROL, RLA,
		        RTI, EOR, KIL, SRE, NOP, EOR, LSR, SRE,
		        PHA, EOR, LSR, ALR, JMP, EOR, LSR, SRE,
		        BVC, EOR, KIL, SRE, NOP, EOR, LSR, SRE,
		        CLI, EOR, NOP, SRE, NOP, EOR, LSR, SRE,
		        RTS, ADC, KIL, RRA, NOP, ADC, ROR, RRA,
		        PLA, ADC, ROR, ARR, JMP, ADC, ROR, RRA,
		        BVS, ADC, KIL, RRA, NOP, ADC, ROR, RRA,
		        SEI, ADC, NOP, RRA, NOP, ADC, ROR, RRA,
		        NOP, STA, NOP, SAX, STY, STA, STX, SAX,
		        DEY, NOP, TXA, XAA, STY, STA, STX, SAX,
		        BCC, STA, KIL, AHX, STY, STA, STX, SAX,
		        TYA, STA, TXS, TAS, SHY, STA, SHX, AHX,
		        LDY, LDA, LDX, LAX, LDY, LDA, LDX, LAX,
		        TAY, LDA, TAX, LAX, LDY, LDA, LDX, LAX,
		        BCS, LDA, KIL, LAX, LDY, LDA, LDX, LAX,
		        CLV, LDA, TSX, LAS, LDY, LDA, LDX, LAX,
		        CPY, CMP, NOP, DCP, CPY, CMP, DEC, DCP,
		        INY, CMP, DEX, AXS, CPY, CMP, DEC, DCP,
		        BNE, CMP, KIL, DCP, NOP, CMP, DEC, DCP,
		        CLD, CMP, NOP, DCP, NOP, CMP, DEC, DCP,
		        CPX, SBC, NOP, ISC, CPX, SBC, INC, ISC,
		        INX, SBC, NOP, SBC, CPX, SBC, INC, ISC,
		        BEQ, SBC, KIL, ISC, NOP, SBC, INC, ISC,
		        SED, SBC, NOP, ISC, NOP, SBC, INC, ISC,
	        };
            // reset
            Reset();
        }

        public void Reset()
        {
            PC = Read16(0xFFFC);
            SP = 0xFD;
            //SetFlags(0x24);
        }

        // Выполняет одну инструкцию процессора
        public int Step()
        {
            // Нужно ли подождать несколько циклов
            if (Stall > 0)
            {
                Stall--;
                return 1;
            }

            var cycles = Cycles;

            // Проверяем нужно ли сделать прерывание
            switch (Interrupt)
            {
                case InterruptType.NMI:
                    NMI();
                    break;
                case InterruptType.IRQ:
                    IRQ();
                    break;
            }

            Interrupt = InterruptType.None;

            var opcode = Read(PC);
            AddressingMode mode = (AddressingMode)instructionModes[opcode];

            UInt16 address = 0;
            bool pageCrossed = false;

            switch (mode)
            {
                case AddressingMode.Absolute:
                    address = Read16((UInt16)(PC + 1));
                    break;
                case AddressingMode.AbsoluteX:
                    address = (UInt16)(Read16((UInt16)(PC + 1)) + X);
                    pageCrossed = PagesDiffer((UInt16)(address - X), address);
                    break;
                case AddressingMode.AbsoluteY:
                    address = (UInt16)(Read16((UInt16)(PC + 1)) + Y);
                    break;
                case AddressingMode.Acculumator:
                    address = 0;
                    break;
                case AddressingMode.Immediate:
                    address = (UInt16)(PC + 1);
                    break;
                case AddressingMode.Implied:
                    address = 0;
                    break;
                case AddressingMode.IndexedIndirect:
                    address = (UInt16)(Read16Bug((UInt16)(Read((UInt16)(PC + 1)))) + X);
                    break;
                case AddressingMode.Indirect:
                    address = (UInt16)(Read16Bug(Read16((UInt16)(PC + 1))));
                    break;
                case AddressingMode.IndirectIndexed:
                    address = (UInt16)(Read16Bug((Read((UInt16)(PC + (1))))) + (UInt16)Y);
                    pageCrossed = PagesDiffer((UInt16)(address - Y), address);
                    break;
                case AddressingMode.Relative:
                    var offset = (UInt16)(Read((UInt16)(PC + 1)));
                    if (offset < 0x80)
                    {
                        address = (UInt16)(PC + 2 + offset);
                    }
                    else
                    {
                        address = (UInt16)(PC + 2 + offset - 0x100);
                    }
                    break;
                case AddressingMode.ZeroPage:
                    address = Read((UInt16)(PC + 1));
                    break;
                case AddressingMode.ZeroPageX:
                    address = (UInt16)(Read((UInt16)(PC + 1 + X)) & 0xff);
                    break;
                case AddressingMode.ZeroPageY:
                    address = (UInt16)(Read((UInt16)(PC + 1 + Y)) & 0xff);
                    break;
            }

            PC += (UInt16)(instructionSizes[opcode]);
            Cycles += instructionCycles[opcode];

            bool pagesCrossed = false;
            if (pagesCrossed)
            {
                Cycles += instructionPageCycles[opcode];
            }

            var info = new StepInfo
            {
                Address = address,
                PC = PC,
                Mode = (byte)mode
            };

            PrintInstruction();

            table[opcode](info);

            return (int)(Cycles - cycles);
        }

        private bool PagesDiffer(UInt16 a, UInt16 b)
        {
            return (a & 0xff00) != (b & 0xFF00);
        }

        private void SetZ(byte value)
        {
            if (value == 0)
            {
                Z = 1;
            }
            else
            {
                Z = 0;
            }
        }

        private void SetN(byte value)
        {
            if ((value & 0x80) != 0)
            {
                N = 1;
            }
            else
            {
                N = 0;
            }
        }

        private void SetZN(byte value)
        {
            SetZ(value);
            SetN(value);
        }

        private void AddBranchCycles(StepInfo info)
        {
            Cycles++;
            if (PagesDiffer(info.PC, info.Address))
            {
                Cycles++;
            }
        }

        private void Compare(byte a, byte b)
        {
            SetZN((byte)(a - b));
            if (a >= b)
            {
                C = 1;
            }
            else
            {
                C = 0;
            }
        }

        private byte Flags()
        {
            byte flags = 0;
            flags |= (byte)(C << 0);
            flags |= (byte)(Z << 1);
            flags |= (byte)(I << 2);
            flags |= (byte)(D << 3);
            flags |= (byte)(B << 4);
            flags |= (byte)(U << 5);
            flags |= (byte)(V << 6);
            flags |= (byte)(N << 7);
            return flags;
        }

        private void SetFlags(byte flags)
        {
            C = (byte)((flags >> 0) & 1);
            Z = (byte)((flags >> 1) & 1);
            I = (byte)((flags >> 2) & 1);
            D = (byte)((flags >> 3) & 1);
            B = (byte)((flags >> 4) & 1);
            U = (byte)((flags >> 5) & 1);
            V = (byte)((flags >> 6) & 1);
            N = (byte)((flags >> 7) & 1);
        }

        private void PrintInstruction()
        {
            var opcode = Read(PC);
            var bytes = instructionSizes[opcode];
            var name = instructionNames[opcode];

            var w0 = $"{String.Format("{0:X}", Read((byte)(PC + 0)))}";
            var w1 = $"{String.Format("{0:X}", Read((byte)(PC + 1)))}";
            var w2 = $"{String.Format("{0:X}", Read((byte)(PC + 2)))}";

            if (bytes < 2)
            {
                w1 = "  ";
            }
            if (bytes < 3)
            {
                w2 = "  ";
            }

            Console.WriteLine("{0:X2} {1} {2} {3}  {4} {5:s28} A: {6:X2} X: {7:X2} Y: {8:X2} P: {9:X2} SP: {10:X2} CYC: {11}", PC, w0, w1, w2, name, "", A, X, Y, Flags(), SP, (Cycles * 3) % 341);
        }

        private void NMI()
        {
            Push16(PC);
            Push((byte)(Flags() | 0x10));
            PC = Read16(0xFFFA);
            I = 1;
            Cycles += 7;
        }

        private void IRQ()
        {
            Push16(PC);
            Push((byte)(Flags() | 0x10));
            PC = Read16(0xFFFE);
            I = 1;
            Cycles += 7;
        }

        private void ADC(StepInfo info)
        {
            var a = A;
            var b = Read(info.Address);
            var c = C;

            A = (byte)(a + b + c);
            SetZN(A);

            if (a + b + c > 0xff)
            {
                C = 1;
            }
            else
            {
                C = 0;
            }

            if (((a ^ b) & 0x80) == 0 && ((a ^ A) & 0x80) != 0)
            {
                V = 1;
            }
            else
            {
                V = 0;
            }
        }

        private void AND(StepInfo info)
        {
            A = (byte)(A & Read(info.Address));
            SetZN(A);
        }

        private void ASL(StepInfo info)
        {
            if ((AddressingMode)info.Mode == AddressingMode.Acculumator)
            {
                C = (byte)((A >> 7) & 1);
                A <<= 1;
                SetZN(A);
            }
            else
            {
                var value = Read(info.Address);
                C = (byte)((value >> 7) & 1);
                value <<= 1;
                Write(info.Address, value);
                SetZN(value);
            }
        }

        private void BCC(StepInfo info)
        {
            if (C == 0)
            {
                PC = info.Address;
                AddBranchCycles(info);
            }
        }

        private void BCS(StepInfo info)
        {
            if (C != 0)
            {
                PC = info.Address;
                AddBranchCycles(info);
            }
        }

        private void BEQ(StepInfo info)
        {
            if (Z != 0)
            {
                PC = info.Address;
                AddBranchCycles(info);
            }
        }

        private void BIT(StepInfo info)
        {
            var value = Read(info.Address);
            V = (byte)((value >> 6) & 1);
            SetZ((byte)(value & A));
            SetN(value);
        }

        private void BMI(StepInfo info)
        {
            if (N != 0)
            {
                PC = info.Address;
                AddBranchCycles(info);
            }
        }

        private void BNE(StepInfo info)
        {
            if (Z == 0)
            {
                PC = info.Address;
                AddBranchCycles(info);
            }
        }

        private void BPL(StepInfo info)
        {
            if (N == 0)
            {
                PC = info.Address;
                AddBranchCycles(info);
            }
        }

        private void BRK(StepInfo info)
        {
            Push16(PC);
            PHP(info);
            SEI(info);
            PC = Read16(0xFFFE);
        }

        private void BVC(StepInfo info)
        {
            if (V == 0)
            {
                PC = info.Address;
                AddBranchCycles(info);
            }
        }

        private void BVS(StepInfo info)
        {
            if (V != 0)
            {
                PC = info.Address;
                AddBranchCycles(info);
            }
        }

        private void CLC(StepInfo info)
        {
            C = 0;
        }

        private void CLD(StepInfo info)
        {
            D = 0;
        }

        private void CLI(StepInfo info)
        {
            I = 0;
        }

        private void CLV(StepInfo info)
        {
            V = 0;
        }

        private void CMP(StepInfo info)
        {
            var value = Read(info.Address);
            Compare(A, value);
        }

        private void CPX(StepInfo info)
        {
            var value = Read(info.Address);
            Compare(X, value);
        }

        private void CPY(StepInfo info)
        {
            var value = Read(info.Address);
            Compare(Y, value);
        }

        private void DEC(StepInfo info)
        {
            var value = (byte)(Read(info.Address) - 1);
            Write(info.Address, value);
            SetZN(value);
        }

        private void DEX(StepInfo info)
        {
            X--;
            SetZN(X);
        }

        private void DEY(StepInfo info)
        {
            Y--;
            SetZN(Y);
        }

        private void EOR(StepInfo info)
        {
            A = (byte)(A ^ Read(info.Address));
            SetZN(A);
        }

        private void INC(StepInfo info)
        {
            var value = (byte)(Read(info.Address) + 1);
            Write(info.Address, value);
            SetZN(value);
        }

        private void INX(StepInfo info)
        {
            X++;
            SetZN(X);
        }

        private void INY(StepInfo info)
        {
            Y++;
            SetZN(Y);
        }

        private void JMP(StepInfo info)
        {
            PC = info.Address;
        }

        private void JSR(StepInfo info)
        {
            Push16((byte)(PC - 1));
            PC = info.Address;
        }

        private void LDA(StepInfo info)
        {
            A = Read(info.Address);
            SetZN(A);
        }

        private void LDX(StepInfo info)
        {
            X = Read(info.Address);
            SetZN(X);
        }

        private void LDY(StepInfo info)
        {
            Y = Read(info.Address);
            SetZN(Y);
        }

        private void LSR(StepInfo info)
        {
            if ((AddressingMode)info.Mode == AddressingMode.Acculumator)
            {
                C = (byte)(A & 1);
                A >>= 1;
                SetZN(A);
            }
            else
            {
                var value = Read(info.Address);
                C = (byte)(value & 1);
                value >>= 1;
                Write(info.Address, value);
                SetZN(value);
            }
        }

        private void NOP(StepInfo info)
        {

        }

        private void ORA(StepInfo info)
        {
            A = (byte)(A | Read(info.Address));
            SetZN(A);
        }

        private void PHA(StepInfo info)
        {
            Push(A);
        }

        private void PHP(StepInfo info)
        {
            Push((byte)(Flags() | 0x10));
        }

        private void PLA(StepInfo info)
        {
            A = Pull();
            SetZN(A);
        }

        private void PLP(StepInfo info)
        {
            SetFlags((byte)(Pull() & 0xEF | 0x20));
        }

        private void ROL(StepInfo info)
        {
            if ((AddressingMode)info.Mode == AddressingMode.Acculumator)
            {
                var c = C;
                C = (byte)((A >> 7) & 1);
                A = (byte)((A << 1) | c);
                SetZN(A);
            }
            else
            {
                var c = C;
                var value = Read(info.Address);

                C = (byte)((value >> 7) & 1);
                value = (byte)((value << 1) | c);
                Write(info.Address, value);
                SetZN(value);
            }
        }

        private void ROR(StepInfo info)
        {
            if ((AddressingMode)info.Mode == AddressingMode.Acculumator)
            {
                var c = C;
                C = (byte)(A & 1);
                A = (byte)((A >> 1) | (c << 7));
                SetZN(A);
            }
            else
            {
                var c = C;
                var value = Read(info.Address);

                C = (byte)(value & 1);
                value = (byte)((value >> 1) | (c << 7));
                Write(info.Address, value);
                SetZN(value);
            }
        }

        private void RTI(StepInfo info)
        {
            SetFlags((byte)(Pull() & 0xEF | 0x20));
            PC = Pull16();
        }

        private void RTS(StepInfo info)
        {
            PC = (byte)(Pull16() + 1);
        }

        private void SBC(StepInfo info)
        {
            var a = A;
            var b = Read(info.Address);
            var c = C;

            A = (byte)(a - b - (byte)(1 - c));
            SetZN(A);

            if ((int)a - (int)b - (int)(1 - c) >= 0)
            {
                C = 1;
            }
            else
            {
                C = 0;
            }

            if (((a ^ b) & 0x80) == 0 && ((a ^ A) & 0x80) != 0)
            {
                V = 1;
            }
            else
            {
                V = 0;
            }
        }

        private void SEC(StepInfo info)
        {
            C = 1;
        }

        private void SED(StepInfo info)
        {
            D = 1;
        }

        private void SEI(StepInfo info)
        {
            I = 1;
        }

        private void STA(StepInfo info)
        {
            Write(info.Address, A);
        }

        private void STX(StepInfo info)
        {
            Write(info.Address, X);
        }

        private void STY(StepInfo info)
        {
            Write(info.Address, Y);
        }

        private void TAX(StepInfo info)
        {
            X = A;
            SetZN(X);
        }

        private void TAY(StepInfo info)
        {
            Y = A;
            SetZN(Y);
        }

        private void TSX(StepInfo info)
        {
            X = SP;
            SetZN(X);
        }

        private void TXA(StepInfo info)
        {
            A = X;
            SetZN(A);
        }

        private void TXS(StepInfo info)
        {
            SP = X;
        }

        private void TYA(StepInfo info)
        {
            A = Y;
            SetZN(A);
        }

        private void AHX(StepInfo info)
        {
        }

        private void ALR(StepInfo info)
        {
        }

        private void ANC(StepInfo info)
        {
        }

        private void ARR(StepInfo info)
        {
        }

        private void AXS(StepInfo info)
        {
        }

        private void DCP(StepInfo info)
        {
        }

        private void ISC(StepInfo info)
        {
        }

        private void KIL(StepInfo info)
        {
        }

        private void LAS(StepInfo info)
        {
        }

        private void LAX(StepInfo info)
        {
        }

        private void RLA(StepInfo info)
        {
        }

        private void RRA(StepInfo info)
        {
        }

        private void SAX(StepInfo info)
        {
        }

        private void SHX(StepInfo info)
        {
        }

        private void SHY(StepInfo info)
        {
        }

        private void SLO(StepInfo info)
        {
        }

        private void SRE(StepInfo info)
        {
        }

        private void TAS(StepInfo info)
        {
        }

        private void XAA(StepInfo info)
        {
        }
    }
}
