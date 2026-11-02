using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;

namespace LogrosBB
{
    class Cabecera : Pintado
    {
        public App App;
        public double Anim = 1;
        Bitmap fondoCache; Size fondoTam;

        Bitmap Fondo()
        {
            var j = App.Juego;
            if (fondoCache != null && fondoTam == Size) return fondoCache;
            if (fondoCache != null) fondoCache.Dispose();
            fondoTam = Size;
            fondoCache = new Bitmap(Math.Max(1, Width), Math.Max(1, Height), PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(fondoCache))
            {
                Tema.Calidad(g);
                g.Clear(Tema.Fondo);
                var r = new Rectangle(0, 0, Width, Height);
                if (j.Fondo != null)
                {
                    // Franja de ARRIBA del arte del juego (la luz y la textura, sin el logo ni el personaje),
                    // con un desenfoque suave y oscurecida donde va el texto
                    var f = j.Fondo;
                    float srcH = f.Width * Height / (float)Math.Max(1, Width), srcW = f.Width;
                    float maxH = f.Height * .29f; // sin llegar al logo ni al personaje
                    if (srcH > maxH) { srcH = maxH; srcW = srcH * Width / (float)Math.Max(1, Height); }
                    var src = new RectangleF((f.Width - srcW) / 2, 0, srcW, srcH);
                    using (var chico = new Bitmap(Math.Max(2, Width / 3), Math.Max(2, Height / 3), PixelFormat.Format32bppRgb))
                    {
                        using (var gc = Graphics.FromImage(chico))
                        using (var ia0 = new ImageAttributes())
                        {
                            gc.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            ia0.SetWrapMode(WrapMode.TileFlipXY);
                            gc.DrawImage(f, new Rectangle(0, 0, chico.Width, chico.Height), src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel, ia0);
                        }
                        using (var ia = new ImageAttributes())
                        {
                            ia.SetWrapMode(WrapMode.TileFlipXY);
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.DrawImage(chico, r, 0, 0, chico.Width, chico.Height, GraphicsUnit.Pixel, ia);
                        }
                    }
                    using (var br = new SolidBrush(Color.FromArgb(120, 10, 9, 12))) g.FillRectangle(br, r);
                    // más oscuro a la izquierda (portada, título y progreso) y más claro a la derecha
                    using (var lg = new LinearGradientBrush(r, Color.FromArgb(200, 10, 9, 12), Color.FromArgb(40, 10, 9, 12), 0f)) g.FillRectangle(lg, r);
                }
                var abajo = new RectangleF(0, Height * .5f, Width, Height * .5f + 1);
                using (var lg = new LinearGradientBrush(abajo, Color.FromArgb(0, Tema.Fondo), Tema.Fondo, 90f)) g.FillRectangle(lg, abajo);
            }
            return fondoCache;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Tema.Calidad(g);
            if (App == null || App.Juego == null) { g.Clear(Tema.Fondo); return; }
            var juego = App.Juego;
            g.DrawImageUnscaled(Fondo(), 0, 0);
            bool sinc = App.Ajustes.Sincronizado;

            float m = Tema.Px(24);
            float lado = Tema.Px(124);
            float top = (Height - lado) / 2f - Tema.Px(4);
            if (juego.IconoJuego != null)
            {
                var ri = new RectangleF(m, top, lado, lado);
                using (var sh = Tema.Redondo(new RectangleF(ri.X, ri.Y + 3, ri.Width, ri.Height), Tema.Px(8)))
                using (var br = new SolidBrush(Color.FromArgb(120, 0, 0, 0))) g.FillPath(br, sh);
                Tema.Icono(g, juego.IconoJuego, ri, Tema.Px(8));
                using (var p = Tema.Redondo(ri, Tema.Px(8))) using (var pen = new Pen(Color.FromArgb(60, 255, 255, 255), 1)) g.DrawPath(pen, p);
            }
            float x = m + lado + Tema.Px(24);
            float derecha = Width - m;
            float colDer = Tema.Px(300);
            float tw = derecha - colDer - x - Tema.Px(16);

            var plat = juego.Platino;
            bool tienePlat = plat != null && plat.Obtenido != null;
            int total = juego.Trofeos.Count, ob = juego.Obtenidos, faltan = juego.FaltanPlatino;

            using (var fT = Tema.F(Tema.Serif, 22f, FontStyle.Bold))
            using (var fS = Tema.F(Tema.Sans, 9.5f))
            using (var fB = Tema.F(Tema.SansSemi, 10f, FontStyle.Bold))
            using (var fP = Tema.F(Tema.Serif, 26f, FontStyle.Bold))
            {
                float y = top - Tema.Px(4);
                Tema.Escribir(g, juego.Titulo.ToUpperInvariant(), fT, Tema.Texto, new RectangleF(x, y, tw, Tema.Px(36)));
                y += Tema.Px(36);
                if (!sinc)
                {
                    Tema.Escribir(g, total + " trofeos · todavía sin sincronizar con bbport", fS, Tema.Tenue, new RectangleF(x, y, tw, Tema.Px(18)));
                    return;
                }
                Tema.Escribir(g, ob + " de " + total + " trofeos obtenidos  ·  " + juego.PuntosObtenidos + " / " + juego.PuntosTotales + " puntos", fS, Tema.Tenue, new RectangleF(x, y, tw, Tema.Px(18)));
                y += Tema.Px(24);
                int pct = (int)Math.Round(juego.Porcentaje * Anim);
                string sp = pct + "%";
                float pw = g.MeasureString(sp, fP).Width;
                Tema.Escribir(g, sp, fP, Tema.Texto, new RectangleF(x - Tema.Px(4), y, pw + 4, Tema.Px(42)), StringAlignment.Near, false, StringAlignment.Center);
                string msj; Color cm;
                if (tienePlat) { msj = "Platino conseguido " + Fecha.Amigable(plat.Obtenido.Value); cm = Tema.ColorGrado('P'); }
                else if (faltan == 0) { msj = "¡Todo listo! El Platino está por caer"; cm = Tema.ColorGrado('P'); }
                else if (faltan == 1) { msj = "¡Te falta UNO solo para el Platino!"; cm = Tema.ColorGrado('G'); }
                else { msj = "Te faltan " + faltan + " para el Platino"; cm = Tema.Texto; }
                Tema.Escribir(g, msj, fB, cm, new RectangleF(x + pw + Tema.Px(6), y, tw - pw - Tema.Px(6), Tema.Px(42)), StringAlignment.Near, false, StringAlignment.Center);
                y += Tema.Px(46);
                var barra = new RectangleF(x, y, Math.Min(tw, Tema.Px(330)), Tema.Px(4));
                using (var br = new SolidBrush(Color.FromArgb(60, 255, 255, 255))) using (var p = Tema.Redondo(barra, Tema.Px(2))) g.FillPath(br, p);
                float fw = (float)(barra.Width * juego.Porcentaje / 100.0 * Anim);
                if (fw > 0) using (var br = new SolidBrush(tienePlat ? Tema.ColorGrado('P') : Tema.Oro)) using (var p = Tema.Redondo(new RectangleF(barra.X, barra.Y, Math.Max(Tema.Px(4), fw), barra.Height), Tema.Px(2))) g.FillPath(br, p);
            }

            // columna derecha: contadores por grado (como bbport) y tiempo de juego
            if (!sinc) return;
            float cw = colDer / 4f;
            float cy = top;
            using (var fN = Tema.F(Tema.SansSemi, 9.5f, FontStyle.Bold))
            using (var fL = Tema.F(Tema.Sans, 8.3f))
            {
                int i = 0;
                foreach (char gr in "PGSB")
                {
                    var deG = juego.Trofeos.Where(t => t.Grado == gr).ToList();
                    int og = deG.Count(t => t.Obtenido != null);
                    Color cg = Tema.ColorGrado(gr);
                    float cx = derecha - colDer + cw * i;
                    float d = Tema.Px(36);
                    var rc = new RectangleF(cx + (cw - d) / 2, cy, d, d);
                    bool completo = deG.Count > 0 && og == deG.Count;
                    using (var br = new SolidBrush(completo ? Color.FromArgb(70, cg) : Color.FromArgb(150, 10, 8, 10))) g.FillEllipse(br, rc);
                    using (var pen = new Pen(Color.FromArgb(og > 0 ? 200 : 80, cg), Tema.Px(1.5f))) g.DrawEllipse(pen, rc);
                    Tema.Copa(g, RectangleF.Inflate(rc, -d * .2f, -d * .18f), og > 0 ? cg : Color.FromArgb(120, cg));
                    Tema.Escribir(g, og + " / " + deG.Count, fN, og > 0 ? Tema.Texto : Tema.Tenue, new RectangleF(cx, cy + d + Tema.Px(4), cw, Tema.Px(18)), StringAlignment.Center);
                    Tema.Escribir(g, Datos.NombreGrado(gr), fL, Color.FromArgb(120, 110, 100), new RectangleF(cx, cy + d + Tema.Px(20), cw, Tema.Px(16)), StringAlignment.Center);
                    i++;
                }
                float ty = cy + Tema.Px(84);
                var rt = new RectangleF(derecha - colDer, ty, colDer, Tema.Px(18));
                if (App.EnJuego && App.SesionDesde.HasValue)
                {
                    // EN VIVO + reloj de la sesión + si los trofeos de bbport se están sincronizando
                    bool okSync;
                    string ses = "EN VIVO  " + Tiempo.Reloj(App.SesionTiempo);
                    string est = "  ·  " + App.EstadoSync(out okSync);
                    float sw = g.MeasureString(ses, fN).Width, ew = g.MeasureString(est, fL).Width;
                    if (Tema.Px(14) + sw + ew > rt.Width + Tema.Px(20)) { est = ""; ew = 0; } // si no entra, solo el reloj
                    float sx = rt.X + (rt.Width - sw - ew - Tema.Px(14)) / 2;
                    bool latido = DateTime.Now.Millisecond < 600;
                    using (var br = new SolidBrush(Color.FromArgb(latido ? 255 : 110, Tema.Vivo))) g.FillEllipse(br, sx, ty + Tema.Px(5), Tema.Px(8), Tema.Px(8));
                    Tema.Escribir(g, ses, fN, Tema.Vivo, new RectangleF(sx + Tema.Px(14), ty, sw + 4, Tema.Px(18)));
                    if (est != "")
                        Tema.Escribir(g, est, fL, okSync ? Color.FromArgb(150, 200, 160) : Color.FromArgb(222, 120, 112),
                            new RectangleF(sx + Tema.Px(14) + sw, ty + Tema.Px(1), ew + 4, Tema.Px(18)));
                }
                else if (App.JuegoAbierto)
                    Tema.Escribir(g, "Launcher abierto · esperando el juego", fL, Tema.Tenue, rt, StringAlignment.Center);
                Tema.Escribir(g, App.TextoTiempo(), fL, Tema.Tenue, new RectangleF(rt.X - Tema.Px(20), ty + Tema.Px(20), rt.Width + Tema.Px(20), Tema.Px(16)), StringAlignment.Center);
            }
        }
    }
}
