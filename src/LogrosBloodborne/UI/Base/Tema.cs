using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace LogrosBB
{
    static class Tema
    {
        public static readonly Color Fondo = Color.FromArgb(14, 11, 13);
        public static readonly Color Panel = Color.FromArgb(24, 19, 22);
        public static readonly Color PanelAlto = Color.FromArgb(34, 26, 29);
        public static readonly Color Borde = Color.FromArgb(58, 45, 48);
        public static readonly Color Texto = Color.FromArgb(236, 226, 211);
        public static readonly Color Tenue = Color.FromArgb(150, 136, 124);
        public static readonly Color Sangre = Color.FromArgb(158, 28, 34);
        public static readonly Color SangreClara = Color.FromArgb(214, 64, 64);
        public static readonly Color Vivo = Color.FromArgb(110, 200, 130);
        public static readonly Color Oro = Color.FromArgb(201, 168, 106);

        public static Color ColorGrado(char g)
        {
            switch (g)
            {
                case 'P': return Color.FromArgb(170, 205, 235);
                case 'G': return Color.FromArgb(232, 186, 74);
                case 'S': return Color.FromArgb(196, 202, 212);
                case 'R': return Color.FromArgb(220, 70, 70);
                default: return Color.FromArgb(198, 128, 78);
            }
        }

        public static float S = 1f;
        public static int Px(float v) { return (int)Math.Round(v * S); }

        // Letras medidas con la MISMA escala que el resto del diseño (en píxeles, no en puntos): así el
        // texto y sus cajas crecen juntos en cualquier escala de Windows, monitor o tele, y nada se sale.
        public static Font F(FontFamily fam, float puntos, FontStyle estilo = FontStyle.Regular)
        {
            if (!fam.IsStyleAvailable(estilo))
                estilo = fam.IsStyleAvailable(FontStyle.Regular) ? FontStyle.Regular : FontStyle.Bold;
            return new Font(fam, Math.Max(1f, puntos * S * 96f / 72f), estilo, GraphicsUnit.Pixel);
        }

        public static string Fuente(params string[] nombres)
        {
            using (var fc = new InstalledFontCollection())
                foreach (var n in nombres)
                    foreach (var f in fc.Families)
                        if (f.Name.Equals(n, StringComparison.OrdinalIgnoreCase)) return n;
            return "Segoe UI";
        }

        // Mismas letras que el launcher de bbport: Cinzel (títulos) e Inter (texto). Se leen de la
        // carpeta de bbport del jugador; si no están, se usan las del sistema.
        static readonly PrivateFontCollection propias = new PrivateFontCollection();
        static FontFamily serif, sans;

        public static void CargarFuentes(string bbport)
        {
            try
            {
                string dir = Path.Combine(bbport, "_internal", "assets", "fonts");
                foreach (var n in new[] { "Cinzel-Bold.ttf", "Inter-Regular.ttf", "Inter-Bold.ttf" })
                    if (File.Exists(Path.Combine(dir, n))) propias.AddFontFile(Path.Combine(dir, n));
                foreach (var fam in propias.Families)
                {
                    if (fam.Name == "Cinzel" && fam.IsStyleAvailable(FontStyle.Bold)) serif = fam;
                    if (fam.Name == "Inter" && fam.IsStyleAvailable(FontStyle.Regular) && fam.IsStyleAvailable(FontStyle.Bold)) sans = fam;
                }
            }
            catch { }
        }

        static FontFamily Sistema(params string[] nombres)
        {
            foreach (var n in nombres) try { return new FontFamily(Fuente(n)); } catch { }
            return FontFamily.GenericSansSerif;
        }
        public static FontFamily Serif { get { return serif ?? (serif = Sistema("Palatino Linotype", "Georgia")); } }
        public static FontFamily Sans { get { return sans ?? (sans = Sistema("Segoe UI")); } }
        public static FontFamily SansSemi { get { return Sans; } }

        public static GraphicsPath Redondo(RectangleF r, float radio)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radio * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Calidad(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.CompositingQuality = CompositingQuality.HighQuality;
        }

        public static void Icono(Graphics g, Bitmap img, RectangleF r, float radio)
        {
            if (img == null) return;
            using (var p = Redondo(r, radio))
            {
                var st = g.Save();
                g.SetClip(p);
                g.DrawImage(img, r);
                g.Restore(st);
            }
        }

        public static void Escribir(Graphics g, string s, Font f, Color c, RectangleF r, StringAlignment h = StringAlignment.Near, bool envolver = false, StringAlignment v = StringAlignment.Near)
        {
            using (var sf = new StringFormat())
            using (var br = new SolidBrush(c))
            {
                sf.Alignment = h; sf.LineAlignment = v;
                sf.Trimming = StringTrimming.EllipsisWord;
                if (!envolver) sf.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(s, f, br, r, sf);
            }
        }

        // Engranaje dibujado (botón de Configuración): se ve igual en cualquier PC, sin depender de fuentes
        public static void Engranaje(Graphics g, RectangleF r, Color c)
        {
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, R = Math.Min(r.Width, r.Height) / 2;
            float ext = R, intr = R * .74f, hueco = R * .32f;
            int dientes = 8;
            var pts = new System.Collections.Generic.List<PointF>();
            for (int i = 0; i < dientes; i++)
            {
                double a0 = Math.PI * 2 * i / dientes;
                double paso = Math.PI * 2 / dientes;
                foreach (var x in new[] { new[] { -.5, intr }, new[] { -.3, ext }, new[] { .05, ext }, new[] { .25, intr } })
                {
                    double a = a0 + paso * x[0];
                    pts.Add(new PointF(cx + (float)(Math.Cos(a) * x[1]), cy + (float)(Math.Sin(a) * x[1])));
                }
            }
            using (var gp = new GraphicsPath())
            {
                gp.AddPolygon(pts.ToArray());
                gp.AddEllipse(cx - hueco, cy - hueco, hueco * 2, hueco * 2);
                gp.FillMode = FillMode.Alternate;
                using (var br = new SolidBrush(c)) g.FillPath(br, gp);
            }
        }

        // Copa de trofeo dibujada, para el ícono del programa y para trofeos ocultos
        public static void Copa(Graphics g, RectangleF r, Color c)
        {
            float w = r.Width, x = r.X, y = r.Y;
            using (var br = new SolidBrush(c))
            using (var pen = new Pen(c, w * 0.07f))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                var cup = new GraphicsPath();
                cup.AddLine(x + w * .27f, y + w * .18f, x + w * .73f, y + w * .18f);
                cup.AddBezier(x + w * .73f, y + w * .18f, x + w * .73f, y + w * .48f, x + w * .62f, y + w * .58f, x + w * .5f, y + w * .58f);
                cup.AddBezier(x + w * .5f, y + w * .58f, x + w * .38f, y + w * .58f, x + w * .27f, y + w * .48f, x + w * .27f, y + w * .18f);
                cup.CloseFigure();
                g.FillPath(br, cup);
                g.DrawArc(pen, x + w * .14f, y + w * .22f, w * .2f, w * .2f, 90, 180);
                g.DrawArc(pen, x + w * .66f, y + w * .22f, w * .2f, w * .2f, 270, 180);
                g.FillRectangle(br, x + w * .46f, y + w * .56f, w * .08f, w * .16f);
                using (var b = Redondo(new RectangleF(x + w * .32f, y + w * .7f, w * .36f, w * .1f), w * .03f)) g.FillPath(br, b);
            }
        }
    }
}
