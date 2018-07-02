using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OpenTK.Graphics.OpenGL;
using System.Runtime.InteropServices;

namespace Nanami.Core.CHIP8
{
    /*
     * Nanami.Core.CHIP8
     * 
     * Этот класс представляет процессор CHIP-8
     * 
     * Использованные ресурсы:
     *  - https://en.wikipedia.org/wiki/CHIP-8
     *  - http://devernay.free.fr/hacks/chip8/C8TECH10.HTM
    */
    public class CHIP8
    {

        private struct TableEntry
        {
            public UInt16 Opcode;
            public UInt16 Mask;
            public Action<UInt16> Func;
        }

        private TableEntry[] OpcodeTable;

        private const int PixelSize = 16;

        // Память 4КБ
        public byte[] Memory;

        // Фреймбуффер
        public byte[] Framebuffer = new byte[64 * 32];

        // 16 8-ми битных регистров
        public byte[] V = new byte[16];

        // Регистр для операций с памятью
        public UInt16 I = 0;

        public Stack<UInt16> CallStack = new Stack<UInt16>();

        // Поток ВМ
        public Thread VMThread;

        // Счетчик инструкций
        // Обычно программы начинаются на 0x512
        public UInt16 PC = 512;

        // Таймеры
        public byte DelayTimer = 0;
        public byte SoundTimer = 0;

        public bool[] Keypad = new bool[16];

#if SDL
        // Указатель на окно SDL
        public IntPtr SDLWindow = IntPtr.Zero;

        // Указатель на рендерер SDL
        public IntPtr SDLRenderer = IntPtr.Zero;

        // Указатель на сёрфейс
        public SDL.SDL_Surface SDLSurface;
#endif

        //public byte[] Screen = new byte[64 * 32];
        //public int ScreenWidth, ScreenHeight = 0;

        public bool[,] Screen = new bool[64, 32];

        public bool[,] ClearBuffer = new bool[64, 32];

        public bool NeedRedraw;

        public bool IsInputExecuted;

        public UInt16 LastInput;

        // temp?
        public bool Running;

        public CHIP8()
        {
            Memory = new byte[4096];
            // Load fontset into memory
            for (int i = 0; i < FontSet.Length; ++i)
            {
                Memory[i] = FontSet[i];
            }
            InitOpcodes();
            VMThread = new Thread(new ThreadStart(RunVM));
        }

        byte[] FontSet = new byte[80]
        {
            0xF0, 0x90, 0x90, 0x90, 0xF0, // 0
            0x20, 0x60, 0x20, 0x20, 0x70, // 1
            0xF0, 0x10, 0xF0, 0x80, 0xF0, // 2
            0xF0, 0x10, 0xF0, 0x10, 0xF0, // 3
            0x90, 0x90, 0xF0, 0x10, 0x10, // 4
            0xF0, 0x80, 0xF0, 0x10, 0xF0, // 5
            0xF0, 0x80, 0xF0, 0x90, 0xF0, // 6
            0xF0, 0x10, 0x20, 0x40, 0x40, // 7
            0xF0, 0x90, 0xF0, 0x90, 0xF0, // 8
            0xF0, 0x90, 0xF0, 0x10, 0xF0, // 9
            0xF0, 0x90, 0xF0, 0x90, 0x90, // A
            0xE0, 0x90, 0xE0, 0x90, 0xE0, // B
            0xF0, 0x80, 0x80, 0x80, 0xF0, // C
            0xE0, 0x90, 0x90, 0x90, 0xE0, // D
            0xF0, 0x80, 0xF0, 0x80, 0xF0, // E
            0xF0, 0x80, 0xF0, 0x80, 0x80  // F
        };

        // Инициализация таблицы опкодов
        public void InitOpcodes()
        {
            OpcodeTable = new TableEntry[]
            {
                new TableEntry { Opcode = 0x00E0, Mask = 0xFFFF, Func = Opcode00E0 },
                new TableEntry { Opcode = 0x00EE, Mask = 0xFFFF, Func = Opcode00EE },
                new TableEntry { Opcode = 0x0000, Mask = 0xF000, Func = Opcode0NNN },
                new TableEntry { Opcode = 0x1000, Mask = 0xF000, Func = Opcode1NNN },
                new TableEntry { Opcode = 0x2000, Mask = 0xF000, Func = Opcode2NNN },
                new TableEntry { Opcode = 0x3000, Mask = 0xF000, Func = Opcode3XNN },
                new TableEntry { Opcode = 0x4000, Mask = 0xF000, Func = Opcode4XNN },
                new TableEntry { Opcode = 0x5000, Mask = 0xF00F, Func = Opcode5XY0 },
                new TableEntry { Opcode = 0x6000, Mask = 0xF000, Func = Opcode6XNN },
                new TableEntry { Opcode = 0x7000, Mask = 0xF000, Func = Opcode7XNN },
                new TableEntry { Opcode = 0x8000, Mask = 0xF00F, Func = Opcode8XY0 },
                new TableEntry { Opcode = 0x8001, Mask = 0xF00F, Func = Opcode8XY1 },
                new TableEntry { Opcode = 0x8002, Mask = 0xF00F, Func = Opcode8XY2 },
                new TableEntry { Opcode = 0x8003, Mask = 0xF00F, Func = Opcode8XY3 },
                new TableEntry { Opcode = 0x8004, Mask = 0xF00F, Func = Opcode8XY4 },
                new TableEntry { Opcode = 0x8005, Mask = 0xF00F, Func = Opcode8XY5 },
                new TableEntry { Opcode = 0x8006, Mask = 0xF00F, Func = Opcode8XY6 },
                new TableEntry { Opcode = 0x8007, Mask = 0xF00F, Func = Opcode8XY7 },
                new TableEntry { Opcode = 0x800E, Mask = 0xF00F, Func = Opcode8XYE },
                new TableEntry { Opcode = 0x9000, Mask = 0xF00F, Func = Opcode9XY0 },
                new TableEntry { Opcode = 0xA000, Mask = 0xF000, Func = OpcodeANNN },
                new TableEntry { Opcode = 0xB000, Mask = 0xF000, Func = OpcodeBNNN },
                new TableEntry { Opcode = 0xC000, Mask = 0xF000, Func = OpcodeCXNN },
                new TableEntry { Opcode = 0xD000, Mask = 0xF000, Func = OpcodeDXYN },
                new TableEntry { Opcode = 0xE09E, Mask = 0xF0FF, Func = OpcodeEX9E },
                new TableEntry { Opcode = 0xE0A1, Mask = 0xF0FF, Func = OpcodeEXA1 },
                new TableEntry { Opcode = 0xF007, Mask = 0xF0FF, Func = OpcodeFX07 },
                new TableEntry { Opcode = 0xF00A, Mask = 0xF0FF, Func = OpcodeFX0A },
                new TableEntry { Opcode = 0xF015, Mask = 0xF0FF, Func = OpcodeFX15 },
                new TableEntry { Opcode = 0xF018, Mask = 0xF0FF, Func = OpcodeFX18 },
                new TableEntry { Opcode = 0xF01E, Mask = 0xF0FF, Func = OpcodeFX1E },
                new TableEntry { Opcode = 0xF029, Mask = 0xF0FF, Func = OpcodeFX29 },
                new TableEntry { Opcode = 0xF033, Mask = 0xF0FF, Func = OpcodeFX33 },
                new TableEntry { Opcode = 0xF055, Mask = 0xF0FF, Func = OpcodeFX55 },
                new TableEntry { Opcode = 0xF065, Mask = 0xF0FF, Func = OpcodeFX65 }
            };
        }

        // Запуск ВМ
        public void RunVM()
        {
            for (; ; )
            {
                Step();
            }
        }

        public void Step()
        {
            if (DelayTimer > 0)
            {
                DelayTimer--;
            }

            if (SoundTimer > 0)
            {
                SoundTimer--;
                if (SoundTimer == 0)
                {
                    Console.Beep();
                }
            }

            for (int i = 0; i < 5; i++)
            {
                // Execute instruction
                UInt16 instruction = 0;

                if (PC + 1 >= 4096)
                {
                    Console.WriteLine($"PC out of bounds ({PC})");
                    return;
                }

                instruction = (UInt16)(Memory.ElementAt(PC) << 8); // Big Endian
                instruction |= Memory[PC + 1];

                foreach (var entry in OpcodeTable)
                {
                    if ((instruction & entry.Mask) == entry.Opcode)
                    {
                        entry.Func(instruction);
                        break;
                    }
                }

                PC += 2;
            }
        }

        // Сброс
        public void Reset()
        {
            PC = 512;

            // Предположительно, все регистры при старте процессора равны нулю
            for (int i = 0; i < 16; i++)
            {
                V[i] = 0;
            }

            I = 0;

            // Чистка памяти
            for (int i = 0; i < Memory.Length; i++)
            {
                Memory[i] = 0;
            }
        }
        /*
        public void DrawPixel(int x, int y, byte color)
        {
            for (int k = y * PixelSize; k < (y + 1) * PixelSize; k++)
            {
                for (int f = x * PixelSize; f < (x + 1) * PixelSize; k++)
                {
                    GL.Begin(BeginMode.Quads);

                    GL.End();
                }
            }
        }
        */

        public void RedrawScreen()
        {
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    if (Screen[y, x])
                    {
                        //DrawPixel(x, y, (byte)(Framebuffer[x + y * 64] * 0xFF));
                        GL.Begin(BeginMode.Quads);
                        GL.Vertex2(y * 10, x * 10);
                        GL.Vertex2(y * 10, x * 10 + 10);
                        GL.Vertex2((y + 1) * 10, x * 10 + 10);
                        GL.Vertex2((y + 1) * 10, x * 10);
                        GL.End();
                        GL.Flush();
                    }
                }
            }
        }

        public bool ReadMemoryImage(string path)
        {

            using (BinaryReader r = new BinaryReader(File.Open(path, FileMode.Open)))
            {
                for (int i = 0; i < r.BaseStream.Length; i++)
                {
                    Memory[512 + i] = r.ReadByte();
                }
            }

            return true;

        }

        public void SetKey(UInt16 index, bool state)
        {
            Keypad[index] = state;

            if (state)
            {
                IsInputExecuted = true;
                LastInput = index;
            }
            else
            {
                IsInputExecuted = false;
            }
        }

#region Opcodes

        private UInt16 GetNNN(UInt16 opcode)
        {
            return (UInt16)(opcode & 0x0FFF);
        }

        private UInt16 GetNN(UInt16 opcode)
        {
            return (UInt16)(opcode & 0x00FF);
        }

        private UInt16 GetN(UInt16 opcode)
        {
            return (UInt16)(opcode & 0x000F);
        }

        private UInt16 GetX(UInt16 opcode)
        {
            return (UInt16)((opcode & 0x0F00) >> 8);
        }

        private UInt16 GetY(UInt16 opcode)
        {
            return (UInt16)((opcode & 0x00F0) >> 4);
        }

        // Вызов программы (https://en.wikipedia.org/wiki/RCA_1802)
        private void Opcode0NNN(UInt16 opcode)
        {
        }

        // Очистка экрана
        private void Opcode00E0(UInt16 opcode)
        {
            for (int i = 0; i < 64; i++)
            {
                for (int j = 0; j < 32; j++)
                {
                    Screen[i, j] = false;
                }
            }
        }

        // Возвращает подпрограмму
        private void Opcode00EE(UInt16 opcode)
        {
            PC = CallStack.Peek();
            CallStack.Pop();
        }

        // Прыжок на адрес NNN 
        private void Opcode1NNN(UInt16 opcode)
        {
            PC = GetNNN(opcode);
            PC -= 2;

            //Console.WriteLine("NNN: {0}, Other: {0}", stuff, other);
        }

        // Вызов подпрограммы по адресу NNN
        private void Opcode2NNN(UInt16 opcode)
        {
            CallStack.Push(PC);
            PC = GetNNN(opcode);
            PC -= 2;
        }

        // Пропуск следующей инструкции если VX == NN
        private void Opcode3XNN(UInt16 opcode)
        {
            var x = GetX(opcode);
            var n = GetNN(opcode);

            if (V[x] == n)
            {
                PC += 2;
            }
        }

        // Пропуск следующей инструкции если VX != NN
        private void Opcode4XNN(UInt16 opcode)
        {
            var x = GetX(opcode);
            var n = GetNN(opcode);

            if (V[x] != n)
            {
                PC += 2;
            }
        }

        // Пропуск следующей инструкции если VX == VY
        private void Opcode5XY0(UInt16 opcode)
        {
            var x = GetX(opcode);
            var y = GetY(opcode);

            if (V[x] == V[y])
            {
                PC += 2;
            }
        }

        // Присвоение VX к NN
        private void Opcode6XNN(UInt16 opcode)
        {
            V[GetX(opcode)] = (byte)GetNN(opcode);
        }

        // Прибавление NN к VX
        private void Opcode7XNN(UInt16 opcode)
        {
            V[GetX(opcode)] += (byte)GetNN(opcode);
        }

        // Присвоение VX к VY
        private void Opcode8XY0(UInt16 opcode)
        {
            V[GetX(opcode)] = V[GetY(opcode)];
        }

        // Присвоение VX к VX | VY
        private void Opcode8XY1(UInt16 opcode)
        {
            V[GetX(opcode)] |= V[GetY(opcode)];
        }

        // Присвоение VX к VX & VY
        private void Opcode8XY2(UInt16 opcode)
        {
            V[GetX(opcode)] &= V[GetY(opcode)];
        }

        // Присвоение VX к VX ^ VY
        private void Opcode8XY3(UInt16 opcode)
        {
            V[GetX(opcode)] ^= V[GetY(opcode)];
        }

        // Прибавление VY к VX
        // Если есть перенос, то VF = 1
        private void Opcode8XY4(UInt16 opcode)
        {
            UInt16 r = (UInt16)(V[GetX(opcode)] + V[GetY(opcode)]);
            V[GetX(opcode)] = (byte)r;
            V[0xF] = (byte)((r >= 0x100) ? 1 : 0);
        }

        // Вычитание VY из VX
        // Если есть заем, то VF = 1
        private void Opcode8XY5(UInt16 opcode)
        {
            Int16 r = (Int16)(V[GetX(opcode)] - V[GetY(opcode)]);
            V[GetX(opcode)] = (byte)r;
            V[0xF] = (byte)((r >= 0) ? 1 : 0);
        }

        // Сдвиг VY на один бит вправо и присвоение результата VX
        // VF = НЗБ (Least Significant Bit)
        private void Opcode8XY6(UInt16 opcode)
        {
            byte lsb = (byte)(V[GetY(opcode)] & 1);
            byte r = (byte)(V[GetY(opcode)] >> 1);

            V[GetY(opcode)] = r;
            V[GetX(opcode)] = r;
            V[0xF] = lsb;
        }

        // Присвоение VX к VY - VX (VY не изменяется)
        // Если есть заем, то VF = 1
        private void Opcode8XY7(UInt16 opcode)
        {
            Int16 r = (Int16)(V[GetY(opcode)] - V[GetX(opcode)]);
            V[GetX(opcode)] = (byte)r;
            V[0xF] = (byte)((r >= 0) ? 1 : 0);
        }

        // Сдвиг VY на один бит влево и присвоение результата VX
        // VF = СЗМ (Most Significant Bit)
        private void Opcode8XYE(UInt16 opcode)
        {
            byte msb = (byte)(V[GetY(opcode)] >> 7);
            byte r = (byte)(V[GetY(opcode)] << 1);

            V[GetY(opcode)] = r;
            V[GetX(opcode)] = r;
            V[0xF] = msb;
        }

        // Пропуск следующей инструкции если VX != VY
        private void Opcode9XY0(UInt16 opcode)
        {
            if (V[GetX(opcode)] != V[GetY(opcode)])
            {
                PC += 2;
            }
        }

        // Присоение I к NNN
        private void OpcodeANNN(UInt16 opcode)
        {
            I = GetNNN(opcode);
        }

        // Прыжок на адрес NNN + V0
        private void OpcodeBNNN(UInt16 opcode)
        {
            PC = (UInt16)(V[0] + GetNNN(opcode) & 0xFFF);
        }

        private void OpcodeCXNN(UInt16 opcode)
        {
            V[GetX(opcode)] = (byte)(new Random().Next(0, 255));
        }

        // Рисование спрайта
        private void OpcodeDXYN(UInt16 opcode)
        {
            var n = GetN(opcode);

            for (var x = 0; x < 64; x++)
            {
                for (var y = 0; y < 32; y++)
                {
                    if (ClearBuffer[x, y])
                    {
                        if (Screen[x, y])
                        {
                            NeedRedraw = true;
                        }

                        ClearBuffer[x, y] = false;
                        Screen[x, y] = false;
                    }
                }
            }

            V[0xF] = 0;

            for (int j = 0; j < n; j++)
            {
                var px = Memory[I + j];
                for (int k = 0; k < 8; k++)
                {
                    var x = (V[GetX(opcode)] + k) % 64; // ??
                    var y = (V[GetY(opcode)] + j) % 32; // ??

                    var spriteBit = ((px >> (7 - k)) & 1);
                    var oldBit = Screen[x, y] ? 1 : 0;

                    if (oldBit != spriteBit)
                    {
                        NeedRedraw = true;
                    }

                    var newBit = oldBit ^ spriteBit;

                    if (newBit != 0)
                    {
                        Screen[x, y] = true;
                    }
                    else
                    {
                        ClearBuffer[x, y] = true;
                    }

                    if (oldBit != 0 && newBit == 0)
                    {
                        V[0xF] = 1;
                    }
                }
            }
        }

        private void OpcodeEX9E(UInt16 opcode)
        {
            if (Keypad[V[GetX(opcode)]])
            {
                PC += 2;
            }
        }

        private void OpcodeEXA1(UInt16 opcode)
        {
            if (!Keypad[V[GetX(opcode)]])
            {
                PC += 2;
            }
        }

        private void OpcodeFX07(UInt16 opcode)
        {
            var x = GetX(opcode);
            V[x] = DelayTimer;
        }

        private void OpcodeFX0A(UInt16 opcode)
        {
            if (IsInputExecuted)
            {
                V[GetX(opcode)] = (byte)LastInput;
            }
            else
            {
                PC -= 2;
            }
        }

        private void OpcodeFX15(UInt16 opcode)
        {
            DelayTimer = V[GetX(opcode)];
        }

        private void OpcodeFX18(UInt16 opcode)
        {
            SoundTimer = V[GetX(opcode)];
        }

        private void OpcodeFX1E(UInt16 opcode)
        {
            I = (UInt16)(I + V[GetX(opcode)]);
        }

        private void OpcodeFX29(UInt16 opcode)
        {
            I = (UInt16)(V[GetX(opcode)] * 5);
        }

        private void OpcodeFX33(UInt16 opcode)
        {
            UInt16 bcd = V[GetX(opcode)];

            Memory[I + 0] = (byte)(bcd / 100);
            Memory[I + 1] = (byte)((bcd / 10) % 10);
            Memory[I + 2] = (byte)(bcd % 10);
        }

        private void OpcodeFX55(UInt16 opcode)
        {
            int last = GetX(opcode);

            for (int a = 0; a <= last; a++, I++)
            {
                Memory[I] = V[a];
            }
        }

        private void OpcodeFX65(UInt16 opcode)
        {
            int last = GetX(opcode);

            for (int a = 0; a <= last; a++, I++)
            {
                V[a] = Memory[I];
            }
        }

#endregion
    }
}
