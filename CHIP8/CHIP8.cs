using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SDL2;
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

        private List<TableEntry> OpcodeTable;

        private const int PixelSize = 16;

        // Память 4КБ
        public byte[] Memory = new byte[4096];

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
        public UInt16 PC = 0x512;

        // Таймеры
        public byte DelayTimer = 0;
        public byte SoundTimer = 0;

        // Указатель на окно SDL
        public IntPtr SDLWindow = IntPtr.Zero;

        // Указатель на рендерер SDL
        public IntPtr SDLRenderer = IntPtr.Zero;

        // Указатель на сёрфейс
        public SDL.SDL_Surface SDLSurface;

        public byte[] Screen = new byte[64 * 32];
        public int ScreenWidth, ScreenHeight = 0;

        // temp?
        public bool Running;

        public CHIP8()
        {
            VMThread = new Thread(new ThreadStart(RunVM));
        }

        // Инициализация таблицы опкодов
        public void InitOpcodes()
        {
            OpcodeTable = new List<TableEntry>
            {
                new TableEntry { Opcode = 0x0000, Mask = 0xF000, Func = Opcode0NNN },
                new TableEntry { Opcode = 0x00E0, Mask = 0xFFFF, Func = Opcode00E0 },
                new TableEntry { Opcode = 0x00EE, Mask = 0xFFFF, Func = Opcode00EE },
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
            UInt32 ticks = SDL.SDL_GetTicks();

            for (;;)
            {
                UInt32 now = SDL.SDL_GetTicks();

                if (now - ticks > 16)
                {
                    UInt32 d = now - ticks;
                    UInt32 t = d / 16;

                    DelayTimer = (byte)Math.Max(0, DelayTimer - (int)t);
                    SoundTimer = (byte)Math.Max(0, DelayTimer - (int)t);

                    ticks = now - d % 16;
                }
            }
        }

        // Сброс
        public void Reset()
        {
            PC = 0x512;

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
         * Инициализация графики
         * 
         * Использованные ресурсы:
         *  - https://github.com/Kuzirashi/sdl2-cs-basics
        */
        public bool InitGraphics()
        {
            // Инициализация модулей SDL
            if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO | SDL.SDL_INIT_AUDIO | SDL.SDL_INIT_TIMER) != 0)
            {
                Console.WriteLine($"Failed to initialize SDL: {SDL.SDL_GetError()}");
                return false;
            }

            // Создание окна
            SDLWindow = SDL.SDL_CreateWindow("Nanami.CHIP-8", SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED, 64 * PixelSize, 32 * PixelSize, 0);
            if (SDLWindow == IntPtr.Zero)
            {
                Console.WriteLine($"Failed to create SDL window: {SDL.SDL_GetError()}");
                DestroyGraphics();
                return false;
            }

            // Создание рендерера
            SDLRenderer = SDL.SDL_CreateRenderer(SDLWindow, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);
            if (SDLRenderer == IntPtr.Zero)
            {
                Console.WriteLine($"Failed to create SDL renderer: {SDL.SDL_GetError()}");
                DestroyGraphics();
                return false;
            }

            var s = SDL.SDL_GetWindowSurface(SDLWindow);
            if (s == IntPtr.Zero)
            {
                Console.WriteLine($"Failed to get window surface: {SDL.SDL_GetError()}");
                DestroyGraphics();
                return false;
            }
            SDLSurface = Marshal.PtrToStructure<SDL.SDL_Surface>(s);

            return true;
        }

        public void Wait()
        {
            Running = true;
            // TODO: Создать отдельную функцию для ивентов SDL
            while (Running) {
                SDL.SDL_Event e;
                while (SDL.SDL_PollEvent(out e) == 1)
                {
                    if (e.type == SDL.SDL_EventType.SDL_QUIT)
                    {
                        Running = false;
                        break;
                    }
                }
                SDL.SDL_UpdateWindowSurface(SDLWindow);
            }
        }

        // Чистка SDL
        public void DestroyGraphics()
        {
            SDL.SDL_DestroyRenderer(SDLRenderer);
            SDL.SDL_DestroyWindow(SDLWindow);
            SDL.SDL_Quit();
        }

        public bool ReadMemoryImage(string path)
        {
            FileStream image;

            try
            {
                image = File.Open(path, FileMode.Open, FileAccess.Read);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to load file: {path} ({e.Message})");
#if DEBUG
                Console.WriteLine(e.StackTrace);
#endif
                return false;
            }

            MemoryStream m = new MemoryStream();
            image.CopyTo(m);

            Memory = m.ToArray();

            m.Close();
            image.Close();

            return true;

        }

        public void DrawPixel(int x, int y, byte color)
        {
            for (int k = y * PixelSize; k < (y + 1) * PixelSize; k++)
            {
                for (int f = x * PixelSize; f < (x + 1) * PixelSize; k++)
                {
                    Screen[(k * ScreenWidth + f) * 4 + 0] = color;
                    Screen[(k * ScreenWidth + f) * 4 + 1] = color;
                    Screen[(k * ScreenWidth + f) * 4 + 2] = color;
                    Screen[(k * ScreenWidth + f) * 4 + 3] = color;
                }
            }
        }

        public void RedrawScreen()
        {
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; y < 64; y++)
                {
                    DrawPixel(x, y, (byte)(Framebuffer[x + y * 64] * 0xFF));
                }
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
            for (int i = 0; i < Framebuffer.Length; i++)
            {
                Framebuffer[i] = 0;
            }

            RedrawScreen();
        }

        // Возвращает подпрограмму
        private void Opcode00EE(UInt16 opcode)
        {
            if (CallStack.Count == 0)
            {
                // TODO:
                return;
            }
            PC = CallStack.Peek();
            CallStack.Pop();
        }

        // Прыжок на адрес NNN 
        private void Opcode1NNN(UInt16 opcode)
        {
            PC = GetNNN(opcode);
        }

        // Вызов подпрограммы по адресу NNN
        private void Opcode2NNN(UInt16 opcode)
        {
            CallStack.Push(PC);
            PC = GetNNN(opcode);
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
            var x = V[GetX(opcode)] % 64; // ??
            var y = V[GetY(opcode)] % 32; // ??
            var n = GetN(opcode);
            bool flipped = false;

            for (int j = 0; j < n; j++, y++)
            {
                var px = Memory[I + j];
                for (int k = 0; k < 8; k++)
                {
                    var bit = (byte)((px >> k) & 1);
                    if (bit == 1 && Framebuffer[x + k + y * 64] == 1)
                    {
                        flipped |= true;
                    }
                    Framebuffer[x + k + y * 64] ^= bit;
                }
            }

            V[0xF] = Convert.ToByte(flipped);
        }

        private void OpcodeEX9E(UInt16 opcode) {}
        private void OpcodeEXA1(UInt16 opcode) {}

        private void OpcodeFX07(UInt16 opcode)
        {
            var x = GetX(opcode);
            V[x] = DelayTimer;
        }

        private void OpcodeFX0A(UInt16 opcode) {}

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
            I = (byte)(I + V[GetX(opcode)]);
        }

        private void OpcodeFX29(UInt16 opcode)
        {

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
