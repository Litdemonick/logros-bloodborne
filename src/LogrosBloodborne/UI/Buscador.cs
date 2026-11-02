using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace LogrosBB
{
    // No se encontró bbport: buscarlo en los discos o elegirlo a mano
    class Buscador : Form
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
        public string Elegida;
        string estado = "No encontré bbport en los lugares de siempre.";
        readonly Boton bBuscar, bElegir, bCancelar;
        volatile bool buscando, cancelar;

        public Buscador()
        {
            Text = "Buscar bbport · Logros Bloodborne";
            try { Icon = App.IconoPrograma; } catch { }
            BackColor = Tema.Fondo; ForeColor = Tema.Texto;
            FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Tema.Px(560), Tema.Px(300));
            DoubleBuffered = true;
            bBuscar = new Boton("Buscar en mis discos") { Primario = true, Size = new Size(Tema.Px(220), Tema.Px(38)) };
            bElegir = new Boton("Elegir carpeta…") { Size = new Size(Tema.Px(220), Tema.Px(38)) };
            bCancelar = new Boton("Cancelar") { Size = new Size(Tema.Px(120), Tema.Px(34)) };
            int cx = ClientSize.Width / 2;
            bBuscar.Location = new Point(cx - bBuscar.Width - Tema.Px(6), Tema.Px(170));
            bElegir.Location = new Point(cx + Tema.Px(6), Tema.Px(170));
            bCancelar.Location = new Point(cx - bCancelar.Width / 2, Tema.Px(240));
            bBuscar.Click += delegate { Buscar(); };
            bElegir.Click += delegate
            {
                using (var fb = new FolderBrowserDialog { Description = "Elegí la carpeta de bbport (la que tiene Bloodborne.exe)", ShowNewFolderButton = false })
                    if (fb.ShowDialog(this) == DialogResult.OK) { Elegida = fb.SelectedPath; DialogResult = DialogResult.OK; }
            };
            bCancelar.Click += delegate { cancelar = true; if (!buscando) DialogResult = DialogResult.Cancel; };
            Controls.AddRange(new Control[] { bBuscar, bElegir, bCancelar });
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int v = 1; DwmSetWindowAttribute(Handle, 20, ref v, 4); } catch { }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(Tema.Fondo); Tema.Calidad(g);
            using (var f1 = new Font(Tema.Serif, 16f, FontStyle.Bold))
            using (var f2 = new Font(Tema.Sans, 9.8f))
            {
                Tema.Escribir(g, "¿DÓNDE ESTÁ TU BBPORT?", f1, Tema.Texto, new RectangleF(0, Tema.Px(30), ClientSize.Width, Tema.Px(34)), StringAlignment.Center);
                Tema.Escribir(g, "Logros Bloodborne necesita la carpeta de bbport (la que tiene Bloodborne.exe) para leer tus trofeos.", f2, Tema.Tenue,
                    new RectangleF(Tema.Px(40), Tema.Px(72), ClientSize.Width - Tema.Px(80), Tema.Px(40)), StringAlignment.Center, true);
                Tema.Escribir(g, estado, f2, buscando ? Tema.Oro : Tema.Texto, new RectangleF(Tema.Px(30), Tema.Px(124), ClientSize.Width - Tema.Px(60), Tema.Px(36)), StringAlignment.Center, true);
            }
        }

        void Buscar()
        {
            if (buscando) return;
            buscando = true; cancelar = false;
            bBuscar.Enabled = bElegir.Enabled = false;
            var hilo = new Thread(() =>
            {
                string hallada = null;
                try
                {
                    foreach (var dr in DriveInfo.GetDrives().Where(x => x.DriveType == DriveType.Fixed && x.IsReady))
                    {
                        hallada = Recorrer(dr.RootDirectory.FullName, 0);
                        if (hallada != null || cancelar) break;
                    }
                }
                catch { }
                BeginInvoke((Action)(() =>
                {
                    buscando = false;
                    if (hallada != null) { Elegida = hallada; DialogResult = DialogResult.OK; return; }
                    if (cancelar) { DialogResult = DialogResult.Cancel; return; }
                    estado = "No lo encontré. Elegí la carpeta a mano.";
                    bBuscar.Enabled = bElegir.Enabled = true;
                    Invalidate();
                }));
            }) { IsBackground = true };
            hilo.Start();
        }

        static readonly string[] saltear = { "windows", "$recycle.bin", "programdata", "system volume information", "appdata", "node_modules", ".git", "msocache" };

        string Recorrer(string dir, int prof)
        {
            if (cancelar || prof > 5) return null;
            if (Datos.EsBbport(dir)) return dir;
            if (prof % 2 == 0)
                try { BeginInvoke((Action)(() => { estado = "Buscando… " + dir; Invalidate(); })); } catch { }
            string[] subs;
            try { subs = Directory.GetDirectories(dir); } catch { return null; }
            // primero las carpetas con nombres probables
            foreach (var s in subs.OrderBy(x => Regex.IsMatch(Path.GetFileName(x), "bbport|bloodborne|games|juegos", RegexOptions.IgnoreCase) ? 0 : 1))
            {
                string n = Path.GetFileName(s).ToLowerInvariant();
                if (saltear.Contains(n)) continue;
                try { if ((File.GetAttributes(s) & FileAttributes.ReparsePoint) != 0) continue; } catch { continue; }
                var r = Recorrer(s, prof + 1);
                if (r != null) return r;
            }
            return null;
        }
    }
}
