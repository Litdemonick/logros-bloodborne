using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Logros Bloodborne")]
[assembly: AssemblyProduct("Logros Bloodborne")]
[assembly: AssemblyDescription("Trofeos en pantalla para bbport")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace LogrosBB
{
    static class Programa
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        // Ícono del programa: copa dorada sobre un sello rojo sangre. Se usa al compilar.
        static void GuardarIcono(string ruta)
        {
            var tamaños = new[] { 256, 64, 48, 32, 24, 16 };
            var pngs = new List<byte[]>();
            foreach (int s in tamaños)
                using (var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        Tema.Calidad(g);
                        var r = new RectangleF(s * .03f, s * .03f, s * .94f, s * .94f);
                        using (var gp = new GraphicsPath())
                        {
                            gp.AddEllipse(r);
                            using (var pb = new PathGradientBrush(gp) { CenterPoint = new PointF(s * .4f, s * .35f), CenterColor = Color.FromArgb(196, 44, 48), SurroundColors = new[] { Color.FromArgb(92, 12, 18) } })
                                g.FillPath(pb, gp);
                        }
                        using (var pen = new Pen(Color.FromArgb(232, 186, 74), Math.Max(1f, s * .035f))) g.DrawEllipse(pen, RectangleF.Inflate(r, -s * .03f, -s * .03f));
                        var rc = RectangleF.Inflate(r, -s * .17f, -s * .17f);
                        rc.Offset(0, s * .03f);
                        Tema.Copa(g, new RectangleF(rc.X + s * .012f, rc.Y + s * .02f, rc.Width, rc.Height), Color.FromArgb(120, 0, 0, 0));
                        Tema.Copa(g, rc, Color.FromArgb(240, 196, 88));
                    }
                    using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); pngs.Add(ms.ToArray()); }
                }
            using (var fs = File.Create(ruta))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write((short)0); bw.Write((short)1); bw.Write((short)tamaños.Length);
                int off = 6 + 16 * tamaños.Length;
                for (int i = 0; i < tamaños.Length; i++)
                {
                    int s = tamaños[i];
                    bw.Write((byte)(s >= 256 ? 0 : s)); bw.Write((byte)(s >= 256 ? 0 : s));
                    bw.Write((byte)0); bw.Write((byte)0); bw.Write((short)1); bw.Write((short)32);
                    bw.Write(pngs[i].Length); bw.Write(off); off += pngs[i].Length;
                }
                foreach (var p in pngs) bw.Write(p);
            }
        }

        // Un error nunca debe tirar el programa en medio del juego: se anota y se sigue.
        static int errores;
        static void Error(Exception ex, bool fatal)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LogrosBloodborne");
                Directory.CreateDirectory(dir);
                string log = Path.Combine(dir, "errores.log");
                if (File.Exists(log) && new FileInfo(log).Length > 512 * 1024) File.Copy(log, Path.Combine(dir, "errores.anterior.log"), true);
                if (File.Exists(log) && new FileInfo(log).Length > 512 * 1024) File.Delete(log);
                File.AppendAllText(log, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + (fatal ? " [FATAL] " : " ") + ex + Environment.NewLine + Environment.NewLine);
            }
            catch { }
            if (fatal || ++errores > 50) Environment.Exit(1);
        }

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--icono") { GuardarIcono(args[1]); return; }
            bool bandeja = args.Any(a => a.Equals("--bandeja", StringComparison.OrdinalIgnoreCase));
            bool creado;
            var mutex = new Mutex(true, "LogrosBloodborne_Instancia", out creado);
            var mostrar = new EventWaitHandle(false, EventResetMode.AutoReset, "LogrosBloodborne_Mostrar");
            if (!creado && args.Contains("--reinicio"))
            {
                // reinicio pedido por la copia anterior: esperar a que termine de cerrarse
                try { creado = mutex.WaitOne(10000); } catch (AbandonedMutexException) { creado = true; }
            }
            if (!creado)
            {
                if (!bandeja) mostrar.Set(); // ya está abierto: solo mostrar su ventana
                return;
            }
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => Error(e.Exception, false);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Error(e.ExceptionObject as Exception, true);
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) Tema.S = g.DpiX / 96f;
            try { App.IconoPrograma = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location); } catch { App.IconoPrograma = SystemIcons.Application; }
            var ctx = new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(ctx);
            App.ContextoUI = ctx;
            Application.Run(new App(!bandeja, mostrar));
            GC.KeepAlive(mutex);
        }
    }
}
