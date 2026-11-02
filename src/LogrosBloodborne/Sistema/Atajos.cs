using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LogrosBB
{
    // Atajos globales: funcionan con el juego en primer plano y no le quitan el foco (salvo abrir la lista).
    class Atajos : NativeWindow
    {
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
        const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_NOREPEAT = 0x4000;

        readonly App app;
        public readonly List<string> Fallidos = new List<string>();
        static readonly Keys[] teclas = { Keys.L, Keys.O, Keys.M, Keys.S, Keys.T };

        public Atajos(App a) { app = a; CreateHandle(new CreateParams()); }

        public void Registrar()
        {
            Liberar();
            Fallidos.Clear();
            for (int i = 0; i < teclas.Length; i++)
                if (!RegisterHotKey(Handle, i + 1, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, (uint)teclas[i])) Fallidos.Add("Ctrl+Alt+" + teclas[i]);
        }

        public void Liberar() { for (int i = 0; i < teclas.Length; i++) UnregisterHotKey(Handle, i + 1); }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312)
            {
                switch (m.WParam.ToInt32())
                {
                    case 1: app.AlternarVentana(); break;
                    case 2: app.Resumen(); break;
                    case 3: app.MoverAviso(); break;
                    case 4: app.AlternarSonido(); break;
                    case 5: app.Probar('X'); break;
                }
                return;
            }
            base.WndProc(ref m);
        }
    }
}
