using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace LogrosBB
{
    // Lo que falta para arrancar, en cualquier PC: la carpeta de bbport o la del juego.
    // Busca solo; si no lo encuentra, explica qué carpeta elegir.
    class Buscador : Form
    {
        public enum Que { Bbport, Juego }

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
        public string Elegida;
        readonly Que que;
        readonly string bbport;
        string titulo, explica, estado;
        // cómo se ve por dentro la carpeta que hay que elegir: "nivel|c(arpeta) o a(rchivo)|nombre|nota"
        string[] ejemplo;
        bool error;
        readonly Boton bBuscar, bElegir, bCancelar;
        readonly System.Windows.Forms.Timer espera = new System.Windows.Forms.Timer { Interval = 2000 };
        volatile bool buscando, cancelar;

        public Buscador(Que que, string bbport = null, string textoSalir = "Salir")
        {
            this.que = que; this.bbport = bbport;
            Text = "Logros Bloodborne";
            try { Icon = App.IconoPrograma; } catch { }
            BackColor = Tema.Fondo; ForeColor = Tema.Texto;
            FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Tema.Px(620), Tema.Px(450));
            DoubleBuffered = true;

            string textoBuscar = "Buscar en mis discos", textoElegir = "Elegir carpeta…";
            switch (que)
            {
                case Que.Bbport:
                    titulo = "¿DÓNDE ESTÁ TU BBPORT?";
                    explica = "No lo encontré en los lugares de siempre, y sin bbport la app no puede andar. Tocá «Elegir carpeta…» y elegí la carpeta bbport " +
                              "(la que tiene Bloodborne.exe), o donde descomprimiste Bloodborne: la busco ahí.";
                    ejemplo = new[] { "0|c|bbport|← elegí esta", "1|a|Bloodborne.exe|", "1|a|Play Bloodborne.exe|", "1|c|user   ·   _internal   ·   launcher|" };
                    estado = "Esperando… si abrís bbport, lo detecto solo.";
                    break;
                default:
                    titulo = "¿DÓNDE ESTÁ EL JUEGO?";
                    explica = "Ya tengo bbport, pero no encontré los archivos del juego. Tocá «Elegir carpeta…» y elegí la carpeta del juego " +
                              "(suele llamarse CUSA03173 y está al lado de bbport). Sirve también la carpeta de arriba: la busco ahí.";
                    ejemplo = new[] { "0|c|CUSA03173|← elegí esta", "1|a|eboot.bin|", "1|c|sce_sys|", "2|c|trophy   →   trophy00.trp|" };
                    estado = bbport != null ? "Tu bbport está en: " + bbport : "";
                    break;
            }

            // el botón principal es el que más ayuda: elegir la carpeta
            bElegir = new Boton(textoElegir) { Primario = true, Size = new Size(Tema.Px(230), Tema.Px(38)) };
            bBuscar = new Boton(textoBuscar) { Size = new Size(Tema.Px(230), Tema.Px(38)) };
            bCancelar = new Boton(textoSalir) { Size = new Size(Tema.Px(130), Tema.Px(34)) };
            int cx = ClientSize.Width / 2;
            bElegir.Location = new Point(cx - bElegir.Width - Tema.Px(6), Tema.Px(330));
            bBuscar.Location = new Point(cx + Tema.Px(6), Tema.Px(330));
            bCancelar.Location = new Point(cx - bCancelar.Width / 2, Tema.Px(392));
            bBuscar.Click += delegate { Buscar(); };
            bElegir.Click += delegate { ElegirAMano(); };
            bCancelar.Click += delegate { cancelar = true; if (!buscando) DialogResult = DialogResult.Cancel; };
            Controls.AddRange(new Control[] { bBuscar, bElegir, bCancelar });
            espera.Tick += delegate { Revisar(); };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int v = 1; DwmSetWindowAttribute(Handle, 20, ref v, 4); } catch { }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            espera.Start(); // cada 2 s: si abren bbport (o el launcher ya sabe dónde está el juego), sigue solo
            Revisar();
        }

        protected override void OnFormClosed(FormClosedEventArgs e) { cancelar = true; espera.Stop(); espera.Dispose(); base.OnFormClosed(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(Tema.Fondo); Tema.Calidad(g);
            using (var f1 = new Font(Tema.Serif, 16f, FontStyle.Bold))
            using (var f2 = new Font(Tema.Sans, 9.8f))
            {
                Tema.Escribir(g, titulo, f1, Tema.Texto, new RectangleF(0, Tema.Px(28), ClientSize.Width, Tema.Px(34)), StringAlignment.Center);
                Tema.Escribir(g, explica, f2, Tema.Tenue, new RectangleF(Tema.Px(36), Tema.Px(70), ClientSize.Width - Tema.Px(72), Tema.Px(64)), StringAlignment.Center, true);
                // ejemplo de la carpeta correcta, en un panel fijo (no se mueve nada al cambiar el estado)
                var panel = new RectangleF(Tema.Px(110), Tema.Px(142), ClientSize.Width - Tema.Px(220), Tema.Px(112));
                using (var p = Tema.Redondo(panel, Tema.Px(8)))
                using (var br = new SolidBrush(Tema.Panel))
                using (var pen = new Pen(Tema.Borde)) { g.FillPath(br, p); g.DrawPath(pen, p); }
                using (var f3 = new Font(Tema.Sans, 9.4f))
                {
                    float y0 = panel.Y + (panel.Height - ejemplo.Length * Tema.Px(22)) / 2f;
                    for (int i = 0; i < ejemplo.Length; i++)
                    {
                        var x = ejemplo[i].Split('|');
                        float fx = panel.X + Tema.Px(24) + int.Parse(x[0]) * Tema.Px(22), fy = y0 + i * Tema.Px(22);
                        bool carpeta = x[1] == "c", elegir = x[3] != "";
                        Color ct = elegir ? Tema.Oro : Tema.Texto;
                        IconoArchivo(g, new RectangleF(fx, fy + Tema.Px(4), Tema.Px(16), Tema.Px(13)), carpeta, carpeta ? Tema.Oro : Tema.Tenue);
                        float tx = fx + Tema.Px(24);
                        float tw = g.MeasureString(x[2], f3).Width;
                        Tema.Escribir(g, x[2], f3, ct, new RectangleF(tx, fy, panel.Right - tx - Tema.Px(10), Tema.Px(22)), StringAlignment.Near, false, StringAlignment.Center);
                        if (elegir)
                            Tema.Escribir(g, x[3], f3, Tema.Oro, new RectangleF(tx + tw + Tema.Px(14), fy, panel.Right - tx - tw - Tema.Px(24), Tema.Px(22)), StringAlignment.Near, false, StringAlignment.Center);
                    }
                }
                Color c = error ? Color.FromArgb(222, 96, 92) : buscando ? Tema.Oro : Tema.Tenue;
                Tema.Escribir(g, estado, f2, c, new RectangleF(Tema.Px(30), Tema.Px(266), ClientSize.Width - Tema.Px(60), Tema.Px(54)), StringAlignment.Center, true);
            }
        }

        // carpeta o archivo dibujados a mano (los emojis no se ven bien en todas las PCs)
        static void IconoArchivo(Graphics g, RectangleF r, bool carpeta, Color c)
        {
            using (var pen = new Pen(c, Math.Max(1f, Tema.Px(1) * 1.3f)))
            using (var br = new SolidBrush(Color.FromArgb(carpeta ? 60 : 0, c)))
            {
                if (carpeta)
                {
                    var pts = new[] { new PointF(r.X, r.Y + r.Height * .15f), new PointF(r.X + r.Width * .38f, r.Y + r.Height * .15f), new PointF(r.X + r.Width * .5f, r.Y + r.Height * .32f),
                                      new PointF(r.Right, r.Y + r.Height * .32f), new PointF(r.Right, r.Bottom), new PointF(r.X, r.Bottom) };
                    g.FillPolygon(br, pts); g.DrawPolygon(pen, pts);
                }
                else
                {
                    float w = r.Height * .78f, x = r.X + (r.Width - w) / 2;
                    var pts = new[] { new PointF(x, r.Y - r.Height * .1f), new PointF(x + w * .65f, r.Y - r.Height * .1f), new PointF(x + w, r.Y + r.Height * .2f), new PointF(x + w, r.Bottom), new PointF(x, r.Bottom) };
                    g.DrawPolygon(pen, pts);
                }
            }
        }

        void Estado(string s, bool err = false) { estado = s; error = err; Invalidate(); }

        // ---------------------------------------------------------------- elegir a mano

        void ElegirAMano()
        {
            string desc = que == Que.Bbport ? "Elegí la carpeta de bbport (la que tiene Bloodborne.exe)"
                        : "Elegí la carpeta del juego (la que tiene eboot.bin y sce_sys, por ejemplo CUSA03173)";
            if (eligiendo || buscando) return;
            string d;
            eligiendo = true; // mientras está abierto el selector, la revisión automática espera
            try
            {
                using (var fb = new FolderBrowserDialog { Description = desc, ShowNewFolderButton = false })
                {
                    if (bbport != null) try { fb.SelectedPath = bbport; } catch { }
                    if (fb.ShowDialog(this) != DialogResult.OK) { eligiendo = false; return; }
                    d = fb.SelectedPath;
                }
            }
            catch { eligiendo = false; return; }
            // comprobar en segundo plano: buscar adentro de una carpeta grande no congela la ventana
            Estado("Comprobando «" + d + "»…");
            bElegir.Enabled = bBuscar.Enabled = false;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                string ok = null;
                try { ok = Validar(d); } catch { }
                try { BeginInvoke((Action)(() => { eligiendo = false; bElegir.Enabled = bBuscar.Enabled = true; Elegido(d, ok); })); } catch { }
            });
        }

        void Elegido(string d, string ok)
        {
            if (DialogResult != DialogResult.None) return;
            if (ok != null) { espera.Stop(); Terminar(ok); return; }
            if (que == Que.Bbport) Estado("En «" + d + "» no hay bbport. Tiene que ser la carpeta que tiene Bloodborne.exe, o una que la contenga.", true);
            else Estado("En «" + d + "» no está el juego. Tiene que tener sce_sys\\trophy\\trophy00.trp (la carpeta CUSA03173).", true);
        }

        string Validar(string d)
        {
            if (que == Que.Bbport) return Datos.NormalizarBbport(d);
            return Datos.NormalizarJuego(d);
        }

        void Terminar(string d) { Elegida = d; DialogResult = DialogResult.OK; }

        // ---------------------------------------------------------------- detección automática (cada 2 s)

        bool revisando, eligiendo, tipicosVistos;
        void Revisar()
        {
            if (revisando || buscando || eligiendo || DialogResult != DialogResult.None) return;
            revisando = true;
            var q = que;
            // fuera del hilo de la ventana: mirar los procesos no traba nada
            ThreadPool.QueueUserWorkItem(_ =>
            {
                string hallada = null;
                try
                {
                    if (q == Que.Bbport)
                    {
                        hallada = Datos.BbportAbierto();
                        if (hallada == null && !tipicosVistos) { tipicosVistos = true; hallada = Datos.BuscarTipicos(); }
                    }
                    else hallada = Datos.JuegoDelLauncher();
                }
                catch { }
                try { BeginInvoke((Action)(() => { revisando = false; if (hallada != null && !buscando && !eligiendo && DialogResult == DialogResult.None) { espera.Stop(); Terminar(hallada); } })); }
                catch { }
            });
        }

        // ---------------------------------------------------------------- búsqueda en los discos

        void Buscar()
        {
            if (buscando) return;
            buscando = true; cancelar = false;
            bBuscar.Enabled = bElegir.Enabled = false;
            Estado("Buscando…");
            var hilo = new Thread(() =>
            {
                string hallada = null;
                try
                {
                    foreach (var dr in DriveInfo.GetDrives())
                    {
                        bool ok;
                        try { ok = dr.IsReady && (dr.DriveType == DriveType.Fixed || dr.DriveType == DriveType.Removable); } catch { ok = false; }
                        if (!ok) continue;
                        hallada = Recorrer(dr.RootDirectory.FullName, 0);
                        if (hallada != null || cancelar) break;
                    }
                }
                catch { }
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        buscando = false;
                        if (hallada != null && DialogResult == DialogResult.None) { espera.Stop(); Terminar(hallada); return; }
                        if (cancelar) { DialogResult = DialogResult.Cancel; return; }
                        Estado(que == Que.Bbport ? "No lo encontré en tus discos. Tocá «Elegir carpeta…» y elegí la que tiene Bloodborne.exe."
                                                 : "No lo encontré en tus discos. Tocá «Elegir carpeta…» y elegí la carpeta CUSA03173.", true);
                        bBuscar.Enabled = bElegir.Enabled = true;
                    }));
                }
                catch { } // la ventana ya se cerró
            }) { IsBackground = true };
            hilo.Start();
        }

        static readonly string[] saltear = { "windows", "$recycle.bin", "programdata", "system volume information", "appdata", "node_modules", ".git", "msocache", "$windows.~bt", "$windows.~ws", "recovery" };

        bool Es(string dir) { return que == Que.Bbport ? Datos.EsBbport(dir) : File.Exists(Path.Combine(dir, "sce_sys", "trophy", "trophy00.trp")); }

        string Recorrer(string dir, int prof)
        {
            if (cancelar || prof > 6) return null;
            try { if (Es(dir)) return dir; } catch { }
            if (prof <= 2)
                try { BeginInvoke((Action)(() => Estado("Buscando… " + dir))); } catch { }
            string[] subs;
            try { subs = Directory.GetDirectories(dir); } catch { return null; }
            foreach (var s in subs.OrderBy(x => Regex.IsMatch(Path.GetFileName(x), "bbport|bloodborne|cusa|games|juegos", RegexOptions.IgnoreCase) ? 0 : 1))
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
