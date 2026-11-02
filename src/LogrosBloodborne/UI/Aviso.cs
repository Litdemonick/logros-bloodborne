using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LogrosBB
{
    class Aviso : Form
    {
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
        [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint ms);
        [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint ms);

        public class Pedido
        {
            public string Titulo, Linea, Pie;
            public char Grado = 'B';
            public Bitmap Icono;
            public bool Sonar = true;
            public char Son;          // sonido distinto al del grado (hitos)
            public bool Mover;        // modo para arrastrar el aviso y elegir dónde aparece
            public bool Resumen;      // resumen rápido: reemplaza al anterior
            public bool Rapido;       // trofeos que se sincronizan tarde: duran menos en pantalla
            public int[] Conteo;      // obtenidos/total por grado P,G,S,B (como en bbport)
            public char Insignia;     // grado del trofeo para la etiqueta de la esquina
            public Action AlGuardar;
            public bool SoloEnJuego = true;  // trofeos: solo con el juego al frente; lo pedido a mano sale siempre
        }

        // Devuelve true si la ventana del juego está al frente (no Windows, no otra app)
        public static Func<bool> JuegoAlFrente = () => true;
        static System.Windows.Forms.Timer espera;

        public static int EnEspera { get { return cola.Count(x => x.SoloEnJuego); } }

        static bool PuedeMostrarse(Pedido p) { return !p.SoloEnJuego || JuegoAlFrente(); }

        static void Esperar()
        {
            if (espera == null)
            {
                espera = new System.Windows.Forms.Timer { Interval = 400 };
                espera.Tick += delegate { if (actual == null && cola.Count > 0 && PuedeMostrarse(cola.Peek())) { espera.Stop(); Siguiente(); } else if (cola.Count == 0) espera.Stop(); };
            }
            espera.Start();
        }

        static readonly Queue<Pedido> cola = new Queue<Pedido>();
        static Aviso actual;
        public static Ajustes Ajustes;
        public static string UserDir;

        public static void Mostrar(Pedido p)
        {
            if ((p.Mover || p.Resumen) && !p.SoloEnJuego)
            {
                // un resumen o un "mover" en pantalla se reemplaza en el momento; un trofeo real no se pisa
                if (actual != null && !actual.IsDisposed && !(actual.p.Resumen || actual.p.Mover) && !p.Mover) { cola.Enqueue(p); return; }
                var viejo = actual;
                actual = new Aviso(p);
                if (viejo != null && !viejo.IsDisposed) { viejo.reloj.Stop(); viejo.Close(); }
                actual.Show();
                if (p.Mover) actual.Activate();
                return;
            }
            cola.Enqueue(p);
            if (actual == null) Siguiente();
        }

        static void Siguiente()
        {
            if (cola.Count == 0) { actual = null; return; }
            if (!PuedeMostrarse(cola.Peek())) { actual = null; Esperar(); return; } // espera a que vuelvas al juego
            actual = new Aviso(cola.Dequeue());
            actual.Show();
        }

        readonly Pedido p;
        readonly System.Windows.Forms.Timer reloj = new System.Windows.Forms.Timer();
        readonly Stopwatch cron = new Stopwatch();
        Point destino;
        int entrada = 420, salida = 480, total;
        long ultimoCalculo;
        bool siguienteYa;
        bool plat;
        float k = 1f; // escala propia del aviso (pantalla, DPI y tamaño elegido)
        int P(float v) { return (int)Math.Round(v * k); }
        float F(float pt) { return pt * k / Tema.S; }

        Aviso(Pedido pedido)
        {
            p = pedido;
            plat = p.Grado == 'P';
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Tema.Fondo;
            DoubleBuffered = true;
            // se adapta a la pantalla del jugador: 1080p, 1440p, 4K o una ventana chica
            bool c0;
            var z0 = Zona(out c0);
            double auto = Math.Max(1.0, Math.Min(1.6, z0.Height / 1080.0 / Tema.S));
            k = (float)(Tema.S * Math.Max(.7, Math.Min(1.5, Ajustes.Tamano)) * auto);
            float ancho = (plat ? 520 : 490) + 32;
            int extra = (p.Conteo != null ? 34 : 0) + (string.IsNullOrEmpty(p.Pie) ? -24 : 0) + (p.Mover ? TiraMover : 0);
            float alto = (plat ? 136 : 124) + extra + 32;
            // ventana de juego chica: el aviso se achica para entrar en ella (no se sale del área del juego)
            if (z0.Width < ancho * k) k = Math.Max(Tema.S * .45f, z0.Width / ancho);
            if (z0.Height < alto * k) k = Math.Max(Tema.S * .45f, z0.Height / alto);
            Size = new Size(P(plat ? 520 : 490), P((plat ? 136 : 124) + extra));
            // velocidad elegida por el jugador para aparecer / desaparecer
            entrada = (int)(entrada / Math.Max(.4, Ajustes.Velocidad));
            salida = (int)(salida / Math.Max(.4, Ajustes.Velocidad));
            Opacity = 0;
            total = entrada + Math.Max(3, (p.Resumen ? 6 : p.Rapido ? 4 : Ajustes.Segundos) + (plat ? 3 : 0)) * 1000 + salida;
            if (p.Mover) { total = int.MaxValue; Cursor = Cursors.SizeAll; KeyPreview = true; }

            destino = Calcular();
            Location = new Point(destino.X + (Ajustes.Posicion.EndsWith("izquierda") ? -P(40) : P(40)), destino.Y);
            if (Ajustes.Posicion == "arriba-centro" || Ajustes.Posicion == "libre" || p.Mover) Location = new Point(destino.X, destino.Y - P(36));
            if (Ajustes.Posicion.StartsWith("abajo")) Location = new Point(destino.X, destino.Y + P(36));

            reloj.Interval = 8;
            reloj.Tick += delegate { Paso(); };
        }

        // Devuelve el rectángulo de la ventana del juego en pantalla, o null si no hay.
        public static Func<Rectangle?> ZonaJuego;

        // Ubica el aviso en la esquina elegida de la ventana del juego (en ventana, sin bordes o
        // pantalla completa); si el juego no está abierto, en la pantalla activa.
        Rectangle Zona(out bool completa)
        {
            Rectangle? zj = null;
            try { if (ZonaJuego != null) zj = ZonaJuego(); } catch { }
            Rectangle a;
            Screen pantalla;
            if (zj.HasValue)
            {
                pantalla = Screen.FromRectangle(zj.Value);
                a = Rectangle.Intersect(zj.Value, pantalla.Bounds);

            }
            else
            {
                try { pantalla = Screen.FromHandle(GetForegroundWindow()); } catch { pantalla = Screen.PrimaryScreen; }
                a = pantalla.WorkingArea;
            }
            completa = a == pantalla.Bounds;
            return a;
        }

        Point Calcular()
        {
            bool completa;
            var a = Zona(out completa);
            int m = P(completa ? 28 : 16);
            int x, y;
            switch (Ajustes.Posicion)
            {
                case "libre":
                    x = a.Left + (int)Math.Round(Math.Max(0, Math.Min(1, Ajustes.PosX)) * Math.Max(0, a.Width - Width));
                    y = a.Top + (int)Math.Round(Math.Max(0, Math.Min(1, Ajustes.PosY)) * Math.Max(0, a.Height - Height));
                    break;
                case "arriba-izquierda": x = a.Left + m; y = a.Top + m; break;
                case "abajo-derecha": x = a.Right - Width - m; y = a.Bottom - Height - m - (completa ? P(40) : 0); break;
                case "abajo-izquierda": x = a.Left + m; y = a.Bottom - Height - m - (completa ? P(40) : 0); break;
                case "arriba-centro": x = a.Left + (a.Width - Width) / 2; y = a.Top + m; break;
                default: x = a.Right - Width - m; y = a.Top + m; break;
            }
            // siempre dentro del área del juego
            x = Math.Max(a.Left, Math.Min(a.Right - Width, x));
            y = Math.Max(a.Top, Math.Min(a.Bottom - Height, y));
            return new Point(x, y);
        }

        protected override bool ShowWithoutActivation { get { return p == null || !p.Mover; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x8; // toolwindow, topmost
                if (p == null || !p.Mover) cp.ExStyle |= 0x08000000 | 0x20; // sin foco y transparente a clics: no molesta al juego
                return cp;
            }
        }

        // ---- modo mover: arrastrar con el mouse, doble clic / Enter guarda, Esc o clic derecho cancela
        // Además, dos botones fijos abajo del aviso: Guardar y Cancelar, para no depender de atajos.
        const int TiraMover = 54;
        Point agarre; bool arrastrando;
        int sobreBoton; // 0 nada, 1 guardar, 2 cancelar

        RectangleF BotonMover(int cual)
        {
            float w = P(132), h = P(34), gap = P(12);
            float y = Height - P(TiraMover) + (P(TiraMover) - h) / 2 - P(4);
            float x0 = (Width - (w * 2 + gap)) / 2;
            return new RectangleF(cual == 1 ? x0 : x0 + w + gap, y, w, h);
        }

        int BotonEn(Point pt)
        {
            if (!p.Mover) return 0;
            if (BotonMover(1).Contains(pt)) return 1;
            if (BotonMover(2).Contains(pt)) return 2;
            return 0;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!p.Mover) return;
            int b = BotonEn(e.Location);
            if (e.Button == MouseButtons.Left && b == 1) { TerminarMover(true); return; }
            if (e.Button == MouseButtons.Left && b == 2) { TerminarMover(false); return; }
            if (e.Button == MouseButtons.Left) { arrastrando = true; agarre = e.Location; }
            if (e.Button == MouseButtons.Right) TerminarMover(false);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (p.Mover && !arrastrando)
            {
                int b = BotonEn(e.Location);
                if (b != sobreBoton) { sobreBoton = b; Cursor = b != 0 ? Cursors.Hand : Cursors.SizeAll; Invalidate(); }
            }
            if (!arrastrando) return;
            bool completa;
            var a = Zona(out completa);
            int x = Location.X + e.X - agarre.X, y = Location.Y + e.Y - agarre.Y;
            x = Math.Max(a.Left, Math.Min(a.Right - Width, x));
            y = Math.Max(a.Top, Math.Min(a.Bottom - Height, y));
            Location = destino = new Point(x, y);
        }

        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); arrastrando = false; }
        protected override void OnMouseDoubleClick(MouseEventArgs e) { base.OnMouseDoubleClick(e); if (p.Mover && BotonEn(e.Location) == 0) TerminarMover(true); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!p.Mover) return;
            if (e.KeyCode == Keys.Enter) TerminarMover(true);
            else if (e.KeyCode == Keys.Escape) TerminarMover(false);
            else
            {
                int d = e.Shift ? P(40) : P(8);
                if (e.KeyCode == Keys.Left) Location = destino = new Point(Location.X - d, Location.Y);
                if (e.KeyCode == Keys.Right) Location = destino = new Point(Location.X + d, Location.Y);
                if (e.KeyCode == Keys.Up) Location = destino = new Point(Location.X, Location.Y - d);
                if (e.KeyCode == Keys.Down) Location = destino = new Point(Location.X, Location.Y + d);
            }
        }

        void TerminarMover(bool guardar)
        {
            if (!p.Mover) return;
            if (guardar)
            {
                bool completa;
                var a = Zona(out completa);
                Ajustes.Posicion = "libre";
                Ajustes.PosX = a.Width > Width ? (Location.X - a.Left) / (double)(a.Width - Width) : .5;
                Ajustes.PosY = a.Height > Height ? (Location.Y - a.Top) / (double)(a.Height - Height) : 0;
                Ajustes.PosX = Math.Max(0, Math.Min(1, Ajustes.PosX)); Ajustes.PosY = Math.Max(0, Math.Min(1, Ajustes.PosY));
                Ajustes.Guardar();
                if (p.AlGuardar != null) p.AlGuardar();
            }
            p.Mover = false;
            arrastrando = false; sobreBoton = 0; Cursor = Cursors.Default;

            total = (int)cron.ElapsedMilliseconds + salida;
            Invalidate();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try { int r = 2; DwmSetWindowAttribute(Handle, 33, ref r, 4); } catch { }
            SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 | 0x0040);
            if (p.Sonar && Ajustes.Sonido) Sonido.Tocar(p.Son != '\0' ? p.Son : p.Grado, Ajustes.Volumen, UserDir);
            try { timeBeginPeriod(1); } catch { }
            cron.Start();
            reloj.Start();
        }

        static double Suave(double t) { t = Math.Max(0, Math.Min(1, t)); return 1 - Math.Pow(1 - t, 3); }

        bool pausado;

        void Paso()
        {
            if (p.SoloEnJuego && !p.Mover)
            {
                bool frente = JuegoAlFrente();
                if (!frente && !pausado) { pausado = true; cron.Stop(); Opacity = 0; return; }
                if (pausado && !frente) return;
                if (pausado && frente) { pausado = false; cron.Start(); }
            }
            long ms = cron.ElapsedMilliseconds;
            if (ms < entrada)
            {
                double t = Suave(ms / (double)entrada);
                Opacity = .97 * t;
                int ox = Location.X, oy = Location.Y;
                Location = new Point((int)(ox + (destino.X - ox) * t), (int)(oy + (destino.Y - oy) * t));
                // pequeño rebote al llegar
                if (ms > entrada * .7) { int reb = (int)(P(3) * Math.Sin((ms - entrada * .7) / (entrada * .3) * Math.PI)); Location = new Point(Location.X, Location.Y + reb); }
            }
            else if (ms < total - salida)
            {
                // si el jugador mueve o agranda la ventana del juego, el aviso la sigue
                if (!p.Mover && ms - ultimoCalculo > 200) { ultimoCalculo = ms; destino = Calcular(); }
                if (Location != destino)
                {
                    int dx = destino.X - Location.X, dy = destino.Y - Location.Y;
                    Location = Math.Abs(dx) + Math.Abs(dy) < 3 ? destino : new Point(Location.X + (int)(dx * .35), Location.Y + (int)(dy * .35));
                }
                Opacity = .97;
                if (ms % 1000 < 20) SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
            }
            else if (ms < total)
            {
                if (!siguienteYa && ms > total - salida + 140 && actual == this && cola.Count > 0) { siguienteYa = true; actual = null; Siguiente(); }
                // salida: se desliza hacia afuera (arriba si está arriba, abajo si está abajo) y se desvanece
                double t = Suave((ms - (total - salida)) / (double)salida);
                Opacity = .97 * (1 - t);
                bool abajo = Ajustes.Posicion.StartsWith("abajo") || (Ajustes.Posicion == "libre" && Ajustes.PosY > .5);
                int dy = (int)(P(26) * t) * (abajo ? 1 : -1);
                Location = new Point(destino.X, destino.Y + dy);
            }
            else
            {
                reloj.Stop();
                Close();
                return;
            }
            if (ms < 1300 || p.Mover) Invalidate();
            else if (ms < total - salida) Invalidate(new Rectangle(0, Height - P(3), Width, P(3))); // solo la barra
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            reloj.Dispose();
            try { timeEndPeriod(1); } catch { }
            if (lienzo != null) { lienzo.Dispose(); lienzo = null; }
            if (actual != this) return; // ya arrancó el siguiente (o fue reemplazado)
            var t = new System.Windows.Forms.Timer { Interval = 120 };
            t.Tick += delegate { t.Stop(); t.Dispose(); Siguiente(); };
            t.Start();
        }

        // La tarjeta se dibuja UNA vez en memoria; en cada cuadro solo se pinta lo que se anima
        // (destello, brillo y barra). Así la animación queda fluida aunque el juego use la GPU a full.
        Bitmap lienzo; RectangleF riCache;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            if (lienzo == null || lienzo.Size != Size)
            {
                if (lienzo != null) lienzo.Dispose();
                lienzo = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
                using (var gl = Graphics.FromImage(lienzo)) { Tema.Calidad(gl); Dibujar(gl, 1000000, false); }
            }
            g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImageUnscaled(lienzo, 0, 0);
            g.CompositingMode = CompositingMode.SourceOver;
            Tema.Calidad(g);
            long ms = cron.ElapsedMilliseconds;
            Color cg = Tema.ColorGrado(p.Grado);
            if (ms < 900)
            {
                int a = (int)(190 * (1 - Suave(ms / 900.0)));
                using (var pen = new Pen(Color.FromArgb(Math.Max(0, a), 255, 255, 255), P(2))) g.DrawRectangle(pen, P(1), P(1), Width - P(3), Height - P(3));
            }
            var ri = riCache;
            float lado = ri.Width;
            if (ms > 150 && ms < 1100 && lado > 0)
            {
                float t = (float)Suave((ms - 150) / 950.0);
                float bx = ri.X - lado + t * lado * 3;
                var st = g.Save();
                using (var pr = Tema.Redondo(ri, P(7))) g.SetClip(pr);
                using (var lb = new LinearGradientBrush(new RectangleF(bx, ri.Y, lado * .6f, lado), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 25f))
                {
                    lb.InterpolationColors = new ColorBlend { Colors = new[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(130, 255, 255, 255), Color.FromArgb(0, 255, 255, 255) }, Positions = new[] { 0f, .5f, 1f } };
                    g.FillRectangle(lb, bx, ri.Y, lado * .6f, lado);
                }
                g.Restore(st);
            }
            if (p.Mover)
            {
                float ty = Height - P(TiraMover) - P(2);
                using (var pen = new Pen(Color.FromArgb(38, 255, 255, 255))) g.DrawLine(pen, P(18), ty, Width - P(18), ty);
                using (var fb = new Font(Tema.SansSemi, F(9.4f), FontStyle.Bold))
                    for (int cual = 1; cual <= 2; cual++)
                    {
                        var rb = BotonMover(cual);
                        bool sobre = sobreBoton == cual, prim = cual == 1;
                        Color fondo = prim ? (sobre ? Color.FromArgb(150, 34, 38) : Color.FromArgb(132, 28, 32)) : (sobre ? Tema.PanelAlto : Tema.Panel);
                        Color borde = prim ? Color.FromArgb(178, 58, 60) : (sobre ? Color.FromArgb(78, 68, 72) : Tema.Borde);
                        using (var pr = Tema.Redondo(rb, P(6))) using (var br = new SolidBrush(fondo)) using (var pen = new Pen(borde)) { g.FillPath(br, pr); g.DrawPath(pen, pr); }
                        Tema.Escribir(g, prim ? "Guardar" : "Cancelar", fb, prim ? Color.FromArgb(246, 236, 220) : Tema.Texto, rb, StringAlignment.Center, false, StringAlignment.Center);
                    }
            }
            else
            {
                // cuenta regresiva: llena al aparecer, vacía justo cuando empieza a irse
                float bh = P(3), by = Height - bh;
                double visible = Math.Max(1, total - salida - entrada);
                float queda = (float)Math.Max(0, Math.Min(1, 1 - (ms - entrada) / visible));
                using (var br = new SolidBrush(Color.FromArgb(30, 255, 255, 255))) g.FillRectangle(br, 0, by, Width, bh);
                using (var br = new SolidBrush(Color.FromArgb(220, cg))) g.FillRectangle(br, 0, by, Width * queda, bh);
            }
        }

        void Dibujar(Graphics g, long ms, bool barra)
        {
            var r = new RectangleF(0, 0, Width, Height);
            Color cg = Tema.ColorGrado(p.Grado);

            using (var lg = new LinearGradientBrush(r, Color.FromArgb(32, 24, 27), Color.FromArgb(13, 10, 12), 90f))
                g.FillRectangle(lg, r);
            // resplandor del color del grado desde la izquierda
            using (var gp = new GraphicsPath())
            {
                gp.AddEllipse(-Width * .35f, -Height * .8f, Width * .9f, Height * 2.6f);
                using (var pb = new PathGradientBrush(gp))
                {
                    pb.CenterColor = Color.FromArgb(plat ? 70 : 46, cg);
                    pb.SurroundColors = new[] { Color.FromArgb(0, cg) };
                    g.FillPath(pb, gp);
                }
            }
            using (var pen = new Pen(Color.FromArgb(110, cg), P(1)))
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            if (ms < 900)
            {
                int a = (int)(200 * (1 - ms / 900.0));
                using (var pen = new Pen(Color.FromArgb(Math.Max(0, a), 255, 255, 255), P(2))) g.DrawRectangle(pen, P(1), P(1), Width - P(3), Height - P(3));
            }
            using (var br = new SolidBrush(cg)) g.FillRectangle(br, 0, 0, P(4), Height);
            if (p.Mover)
                using (var pen = new Pen(Color.FromArgb(220, 255, 255, 255), P(2)) { DashStyle = DashStyle.Dash })
                    g.DrawRectangle(pen, P(1), P(1), Width - P(3), Height - P(3));

            // Distribución simétrica: mismo margen en los 4 lados, ícono y bloque de texto centrados en vertical
            float pad = P(18), barH = p.Mover ? 0 : P(3);
            float hTit = P(20), hNom = P(plat ? 36 : 32), hPie = string.IsNullOrEmpty(p.Pie) ? 0 : P(22), hCnt = p.Conteo != null ? P(22) : 0;
            float sep = P(4);
            float franja = hCnt > 0 ? P(36) : 0;   // franja de contadores, de borde a borde
            float bloque = hTit + sep + hNom + (hPie > 0 ? sep + hPie : 0);
            float tira = p.Mover ? P(TiraMover) : 0; // botones Guardar / Cancelar del modo mover
            float areaH = Height - pad * 2 - barH - franja - tira;
            float lado = Math.Min(areaH, P(plat ? 96 : 84));
            var ri = new RectangleF(pad + P(4), pad + (areaH - lado) / 2, lado, lado);
            riCache = ri;
            using (var pen = new Pen(Color.FromArgb(200, cg), P(2)))
            using (var pr = Tema.Redondo(RectangleF.Inflate(ri, P(3), P(3)), P(9)))
                g.DrawPath(pen, pr);
            if (p.Icono != null) Tema.Icono(g, p.Icono, ri, P(7));
            else
            {
                using (var pr = Tema.Redondo(ri, P(7))) using (var br = new SolidBrush(Tema.PanelAlto)) g.FillPath(br, pr);
                Tema.Copa(g, RectangleF.Inflate(ri, -lado * .16f, -lado * .12f), cg);
            }
            // brillo que cruza el ícono al aparecer
            if (ms > 150 && ms < 1100)
            {
                float t = (ms - 150) / 950f;
                float bx = ri.X - lado + t * lado * 3;
                var st = g.Save();
                using (var pr = Tema.Redondo(ri, P(7))) g.SetClip(pr);
                using (var lb = new LinearGradientBrush(new RectangleF(bx, ri.Y, lado * .6f, lado), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 25f))
                {
                    lb.InterpolationColors = new ColorBlend { Colors = new[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(140, 255, 255, 255), Color.FromArgb(0, 255, 255, 255) }, Positions = new[] { 0f, .5f, 1f } };
                    g.FillRectangle(lb, bx, ri.Y, lado * .6f, lado);
                }
                g.Restore(st);
            }

            float tx = ri.Right + P(18), tw = Width - tx - pad;
            using (var f1 = new Font(Tema.SansSemi, F(plat ? 9.8f : 9.2f), FontStyle.Bold))
            using (var f2 = new Font(Tema.Serif, F(plat ? 17.5f : 15.5f), FontStyle.Bold))
            using (var f3 = new Font(Tema.Sans, F(9.8f)))
            using (var fc = new Font(Tema.SansSemi, F(9.4f), FontStyle.Bold))
            {
                var stc = g.Save();
                g.SetClip(new RectangleF(tx, 0, tw + 1, Height)); // nada se sale de su zona
                float y = pad + (areaH - bloque) / 2;
                float twTit = tw;
                if (p.Insignia != '\0')
                {
                    // etiqueta del grado: copa + PLATINO / ORO / PLATA / BRONCE, alineada a la derecha
                    Color ci = Tema.ColorGrado(p.Insignia);
                    string ins = Datos.NombreGrado(p.Insignia).ToUpperInvariant();
                    float iw = g.MeasureString(ins, f1).Width + P(32);
                    var rins = new RectangleF(tx + tw - iw, y, iw, hTit);
                    using (var pi = Tema.Redondo(rins, P(5))) using (var bi = new SolidBrush(Color.FromArgb(55, ci))) using (var pe = new Pen(Color.FromArgb(200, ci), 1)) { g.FillPath(bi, pi); g.DrawPath(pe, pi); }
                    Tema.Copa(g, new RectangleF(rins.X + P(6), rins.Y + P(2), hTit - P(4), hTit - P(4)), ci);
                    Tema.Escribir(g, ins, f1, ci, new RectangleF(rins.X + P(22), rins.Y, iw - P(26), hTit), StringAlignment.Center, false, StringAlignment.Center);
                    twTit = tw - iw - P(10);
                }
                Tema.Escribir(g, p.Titulo.ToUpperInvariant(), f1, cg, new RectangleF(tx, y, twTit, hTit), StringAlignment.Near, false, StringAlignment.Center);
                y += hTit + sep;
                Tema.Escribir(g, p.Linea, f2, Tema.Texto, new RectangleF(tx, y, tw, hNom), StringAlignment.Near, false, StringAlignment.Center);
                y += hNom;
                if (hPie > 0)
                {
                    y += sep;
                    Tema.Escribir(g, p.Pie, f3, Color.FromArgb(196, 186, 172), new RectangleF(tx, y, tw, hPie), StringAlignment.Near, false, StringAlignment.Center);
                    y += hPie;
                }
                g.Restore(stc);
                stc = g.Save();
                if (hCnt > 0)
                {
                    // franja inferior simétrica: línea de margen a margen y 4 columnas iguales (como en bbport)
                    float fy = Height - barH - pad / 2 - franja - tira;
                    using (var pen = new Pen(Color.FromArgb(38, 255, 255, 255))) g.DrawLine(pen, pad, fy, Width - pad, fy);
                    y = fy + (franja - hCnt) / 2 + P(2);
                    tx = pad; tw = Width - pad * 2;
                    float cw = tw / 4f;
                    for (int i = 0; i < 4; i++)
                    {
                        char gr = "PGSB"[i];
                        int ob = p.Conteo[i * 2], tot = p.Conteo[i * 2 + 1];
                        Color c2 = Tema.ColorGrado(gr);
                        string s = ob + " / " + tot;
                        float sw = g.MeasureString(s, fc).Width;
                        float cx = tx + cw * i + (cw - (P(18) + sw)) / 2;
                        Tema.Copa(g, new RectangleF(cx, y + P(2), P(17), P(17)), Color.FromArgb(ob > 0 ? 255 : 110, c2));
                        Tema.Escribir(g, s, fc, ob == tot && tot > 0 ? c2 : (ob > 0 ? Tema.Texto : Tema.Tenue), new RectangleF(cx + P(18), y, sw + P(4), hCnt), StringAlignment.Near, false, StringAlignment.Center);
                    }
                }
                g.Restore(stc);
            }
        }
    }
}
