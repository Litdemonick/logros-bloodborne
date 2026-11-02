using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace LogrosBB
{
    class Lista : Pintado
    {
        class Fila { public string Seccion; public string SeccionInfo; public double SeccionProg; public Bitmap SeccionIcono; public Trofeo T; public int Y, H; }

        readonly List<Fila> filas = new List<Fila>();
        public Juego Juego;
        public Ajustes Ajustes;
        public string Filtro = "todos", Orden = "juego", Busqueda = "";
        public int Destacado = -1;
        public DateTime DestacadoDesde;
        int sobre = -1;
        float scroll, objetivo; int alto;
        bool arrastraBarra; float agarreY; bool sobreBarra;
        readonly System.Windows.Forms.Timer anim = new System.Windows.Forms.Timer { Interval = 15 };

        public int Margen { get { return Tema.Px(24); } }

        public Lista()
        {
            anim.Tick += delegate
            {
                bool moviendo = Math.Abs(objetivo - scroll) > .5f;
                if (moviendo) scroll += (objetivo - scroll) * .25f; else scroll = objetivo;
                bool brillando = Destacado >= 0 && (DateTime.Now - DestacadoDesde).TotalSeconds < 6;
                if (!brillando) Destacado = -1;
                Invalidate();
                if (!moviendo && !brillando) anim.Stop();
            };
        }

        float MaxScroll { get { return Math.Max(0, alto - Height); } }
        void IrA(float y) { objetivo = Math.Max(0, Math.Min(MaxScroll, y)); anim.Start(); }
        public void Arriba() { scroll = objetivo = 0; Invalidate(); }

        public void Destacar(int id)
        {
            Destacado = id; DestacadoDesde = DateTime.Now;
            foreach (var f in filas) if (f.T != null && f.T.Id == id) IrA(f.Y - Height / 3);
            anim.Start();
        }

        bool SeVe(Trofeo t) { return !t.Oculto || t.Obtenido != null || Ajustes.MostrarOcultos; }

        public void Rearmar()
        {
            filas.Clear();
            if (Juego == null) { alto = 0; Invalidate(); return; }
            IEnumerable<Trofeo> lista = Juego.Trofeos;
            if (Filtro == "obtenidos") lista = lista.Where(t => t.Obtenido != null);
            else if (Filtro == "faltantes") lista = lista.Where(t => t.Obtenido == null);
            else if (Filtro == "recientes") lista = lista.Where(t => t.Obtenido != null);
            string q = (Busqueda ?? "").Trim();
            if (q != "")
            {
                var ci = CultureInfo.GetCultureInfo("es-ES").CompareInfo;
                var op = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
                lista = lista.Where(t => SeVe(t) && (ci.IndexOf(t.Nombre, q, op) >= 0 || ci.IndexOf(t.Detalle, q, op) >= 0 || ci.IndexOf(Datos.NombreGrado(t.Grado), q, op) >= 0));
            }
            Func<Trofeo, int> rango = t => "PGSB".IndexOf(t.Grado);
            List<Trofeo> orden;
            switch (Filtro == "recientes" ? "recientes" : Orden)
            {
                case "tipo": orden = lista.OrderBy(rango).ThenBy(t => t.Id).ToList(); break;
                case "recientes": orden = lista.OrderByDescending(t => t.Obtenido ?? DateTime.MinValue).ThenBy(t => t.Id).ToList(); break;
                case "nombre": orden = lista.OrderBy(t => SeVe(t) ? t.Nombre : "￿", StringComparer.Create(CultureInfo.GetCultureInfo("es-ES"), true)).ToList(); break;
                default: orden = lista.OrderBy(t => t.Id).ToList(); break;
            }
            int y = Tema.Px(6), hFila = Tema.Px(80), hSec = Tema.Px(52);
            if (Filtro == "recientes")
            {
                foreach (var gr in orden.GroupBy(t => Fecha.Grupo(t.Obtenido.Value)))
                {
                    var lst = gr.ToList();
                    filas.Add(new Fila { Seccion = gr.Key, SeccionInfo = lst.Count == 1 ? "1 trofeo" : lst.Count + " trofeos", SeccionProg = -1, Y = y, H = hSec }); y += hSec;
                    foreach (var t in lst) { filas.Add(new Fila { T = t, Y = y, H = hFila }); y += hFila; }
                    y += Tema.Px(8);
                }
            }
            else
            {
                bool porGrupo = Orden == "juego" || Orden == "tipo";
                var grupos = porGrupo ? Juego.Trofeos.Select(t => t.Grupo).Distinct().OrderBy(g => g).ToList() : new List<int> { int.MinValue };
                foreach (int gid in grupos)
                {
                    var deGrupo = porGrupo ? orden.Where(t => t.Grupo == gid).ToList() : orden;
                    if (deGrupo.Count == 0) continue;
                    if (porGrupo)
                    {
                        var todos = Juego.Trofeos.Where(t => t.Grupo == gid).ToList();
                        int ob = todos.Count(t => t.Obtenido != null);
                        filas.Add(new Fila
                        {
                            Seccion = gid < 0 ? Juego.Titulo : (Juego.Grupos.ContainsKey(gid) ? Juego.Grupos[gid] : "Contenido extra"),
                            SeccionInfo = ob + " / " + todos.Count,
                            SeccionProg = todos.Count == 0 ? 0 : ob / (double)todos.Count,
                            SeccionIcono = gid < 0 ? null : Juego.IconoGrupo,
                            Y = y, H = hSec
                        });
                        y += hSec;
                    }
                    foreach (var t in deGrupo) { filas.Add(new Fila { T = t, Y = y, H = hFila }); y += hFila; }
                    y += Tema.Px(8);
                }
            }
            if (filas.Count == 0) { filas.Add(new Fila { Seccion = "", Y = y, H = Tema.Px(140) }); y += Tema.Px(140); }
            alto = y + Tema.Px(12);
            scroll = objetivo = Math.Min(objetivo, MaxScroll);
            Invalidate();
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); scroll = objetivo = Math.Min(objetivo, MaxScroll); }

        // ---- barra de desplazamiento propia: finita, dorada, se agranda al pasar el mouse
        RectangleF Barra()
        {
            if (alto <= Height) return RectangleF.Empty;
            float h = Math.Max(Tema.Px(40), Height * (Height / (float)alto));
            float y = (Height - h) * (scroll / Math.Max(1, MaxScroll));
            float w = Tema.Px(sobreBarra || arrastraBarra ? 6 : 4);
            return new RectangleF(Width - Margen / 2f - w / 2f, y + Tema.Px(4), w, h - Tema.Px(8));
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            IrA(objetivo - e.Delta / 120f * Tema.Px(80 * 1.5f));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.X > Width - Margen && alto > Height)
            {
                var b = Barra();
                if (b.Contains(b.X + 1, e.Y)) { arrastraBarra = true; agarreY = e.Y - b.Y; }
                else IrA(objetivo + (e.Y < b.Y ? -Height : Height) * .9f);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); arrastraBarra = false; Invalidate(); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (arrastraBarra)
            {
                var b = Barra();
                float t = (e.Y - agarreY - Tema.Px(4)) / Math.Max(1, Height - b.Height - Tema.Px(8));
                scroll = objetivo = Math.Max(0, Math.Min(MaxScroll, t * MaxScroll));
                Invalidate();
                return;
            }
            bool sb = e.X > Width - Margen && alto > Height;
            if (sb != sobreBarra) { sobreBarra = sb; Invalidate(); }
            int y = (int)(e.Y + scroll), s = -1;
            for (int i = 0; i < filas.Count; i++) if (filas[i].T != null && y >= filas[i].Y && y < filas[i].Y + filas[i].H && e.X > Margen && e.X < Width - Margen) s = i;
            if (s != sobre) { sobre = s; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); sobre = -1; sobreBarra = false; Invalidate(); }
        protected override bool IsInputKey(Keys k) { return k == Keys.Up || k == Keys.Down || base.IsInputKey(k); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Down) IrA(objetivo + Tema.Px(80));
            if (e.KeyCode == Keys.Up) IrA(objetivo - Tema.Px(80));
            if (e.KeyCode == Keys.PageDown) IrA(objetivo + Height * .9f);
            if (e.KeyCode == Keys.PageUp) IrA(objetivo - Height * .9f);
            if (e.KeyCode == Keys.Home) IrA(0);
            if (e.KeyCode == Keys.End) IrA(MaxScroll);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Tema.Fondo);
            Tema.Calidad(g);
            float mx = Margen, ancho = Width - mx * 2;
            using (var fNom = Tema.F(Tema.SansSemi, 11f, FontStyle.Bold))
            using (var fDet = Tema.F(Tema.Sans, 9.3f))
            using (var fChico = Tema.F(Tema.SansSemi, 9f, FontStyle.Bold))
            using (var fFecha = Tema.F(Tema.Sans, 8.8f))
            using (var fSec = Tema.F(Tema.Serif, 13.5f, FontStyle.Bold))
            {
                if (filas.Count == 1 && filas[0].Seccion == "")
                {
                    string msj = Busqueda != "" ? "No hay trofeos que coincidan con «" + Busqueda.Trim() + "»" :
                                 Filtro == "faltantes" ? "¡No te falta ninguno! Sos un cazador de verdad." : "Todavía no hay trofeos acá. ¡A cazar!";
                    Tema.Escribir(g, msj, fDet, Tema.Tenue, new RectangleF(mx, filas[0].Y - scroll, ancho, filas[0].H), StringAlignment.Center, false, StringAlignment.Center);
                }
                else
                    for (int i = 0; i < filas.Count; i++)
                    {
                        var f = filas[i];
                        float y = f.Y - scroll;
                        if (y + f.H < 0 || y > Height) continue;
                        if (f.T == null) Seccion(g, f, mx, y, ancho, fSec, fDet);
                        else FilaTrofeo(g, f.T, new RectangleF(mx, y + Tema.Px(4), ancho, f.H - Tema.Px(8)), i == sobre, fNom, fDet, fChico, fFecha);
                    }
            }
            // degradé arriba/abajo para que el contenido "entre" suave
            int fade = Tema.Px(14);
            if (scroll > 1) using (var lg = new LinearGradientBrush(new Rectangle(0, 0, Width, fade + 1), Tema.Fondo, Color.FromArgb(0, Tema.Fondo), 90f)) g.FillRectangle(lg, 0, 0, Width, fade);
            if (scroll < MaxScroll - 1) using (var lg = new LinearGradientBrush(new Rectangle(0, Height - fade - 1, Width, fade + 1), Color.FromArgb(0, Tema.Fondo), Tema.Fondo, 90f)) g.FillRectangle(lg, 0, Height - fade, Width, fade);

            var b = Barra();
            if (!b.IsEmpty)
            {
                using (var br = new SolidBrush(Color.FromArgb(28, 255, 255, 255)))
                using (var p = Tema.Redondo(new RectangleF(b.X, Tema.Px(4), b.Width, Height - Tema.Px(8)), b.Width / 2)) g.FillPath(br, p);
                using (var br = new SolidBrush(Color.FromArgb(sobreBarra || arrastraBarra ? 230 : 150, Tema.Oro)))
                using (var p = Tema.Redondo(b, b.Width / 2)) g.FillPath(br, p);
            }
        }

        void Seccion(Graphics g, Fila f, float x, float y, float w, Font fSec, Font fDet)
        {
            float iy = y + Tema.Px(14), h = Tema.Px(26);
            if (f.SeccionIcono != null)
            {
                float iw = h * f.SeccionIcono.Width / f.SeccionIcono.Height;
                Tema.Icono(g, f.SeccionIcono, new RectangleF(x, iy, iw, h), Tema.Px(4));
                x += iw + Tema.Px(10); w -= iw + Tema.Px(10);
            }
            float tw = g.MeasureString(f.Seccion.ToUpperInvariant(), fSec).Width;
            Tema.Escribir(g, f.Seccion.ToUpperInvariant(), fSec, Tema.Oro, new RectangleF(x, iy, Math.Min(tw + 4, w - Tema.Px(220)), h), StringAlignment.Near, false, StringAlignment.Center);
            Tema.Escribir(g, f.SeccionInfo, fDet, Tema.Tenue, new RectangleF(x + Math.Min(tw + 4, w - Tema.Px(220)) + Tema.Px(8), iy, Tema.Px(120), h), StringAlignment.Near, false, StringAlignment.Center);
            if (f.SeccionProg < 0) return;
            float bw = Tema.Px(160), by = iy + h / 2 - Tema.Px(2);
            using (var br = new SolidBrush(Tema.PanelAlto)) using (var p = Tema.Redondo(new RectangleF(x + w - bw, by, bw, Tema.Px(4)), Tema.Px(2))) g.FillPath(br, p);
            if (f.SeccionProg > 0)
                using (var br = new SolidBrush(Tema.Oro)) using (var p = Tema.Redondo(new RectangleF(x + w - bw, by, (float)Math.Max(Tema.Px(4), bw * f.SeccionProg), Tema.Px(4)), Tema.Px(2))) g.FillPath(br, p);
        }

        void FilaTrofeo(Graphics g, Trofeo t, RectangleF r, bool hover, Font fNom, Font fDet, Font fChico, Font fFecha)
        {
            bool tiene = t.Obtenido != null;
            bool ver = SeVe(t);
            Color cg = Tema.ColorGrado(t.Grado);
            Color fondo = hover ? Tema.PanelAlto : Tema.Panel;
            using (var p = Tema.Redondo(r, Tema.Px(8)))
            {
                using (var br = new SolidBrush(fondo)) g.FillPath(br, p);
                if (tiene)
                {
                    var st = g.Save();
                    g.SetClip(p);
                    // el degradé termina en transparente justo donde termina la franja: sin corte visible
                    var franja = new RectangleF(r.X, r.Y, r.Width * .45f, r.Height);
                    using (var lg = new LinearGradientBrush(new RectangleF(franja.X - 1, franja.Y, franja.Width + 2, franja.Height), Color.FromArgb(22, cg), Color.FromArgb(0, cg), 0f)) g.FillRectangle(lg, franja);
                    using (var br = new SolidBrush(cg)) g.FillRectangle(br, r.X, r.Y, Tema.Px(3), r.Height);
                    g.Restore(st);
                }
                Color borde = Tema.Borde;
                if (Destacado == t.Id)
                {
                    double s = (DateTime.Now - DestacadoDesde).TotalSeconds;
                    borde = Color.FromArgb((int)Math.Max(0, 230 * (1 - s / 6) * (.6 + .4 * Math.Sin(s * 5))), cg);
                }
                using (var pen = new Pen(borde, Destacado == t.Id ? Tema.Px(2) : 1)) g.DrawPath(pen, p);
            }

            float pad = Tema.Px(12);
            float lado = r.Height - pad * 2;
            var ri = new RectangleF(r.X + pad + Tema.Px(2), r.Y + pad, lado, lado);
            if (ver && t.Icono != null) Tema.Icono(g, tiene ? t.Icono : t.IconoGris, ri, Tema.Px(6));
            else
            {
                using (var p = Tema.Redondo(ri, Tema.Px(6))) using (var br = new SolidBrush(Tema.Fondo)) using (var pen = new Pen(Tema.Borde)) { g.FillPath(br, p); g.DrawPath(pen, p); }
                using (var fq = Tema.F(Tema.Serif, 18f, FontStyle.Bold)) Tema.Escribir(g, "?", fq, Tema.Tenue, ri, StringAlignment.Center, false, StringAlignment.Center);
            }

            float col = Tema.Px(170);
            float tx = ri.Right + Tema.Px(14), tw = r.Right - tx - col - pad;
            string nombre = ver ? t.Nombre : "Trofeo oculto";
            string detalle = ver ? t.Detalle : "Seguí jugando para descubrirlo.";
            var rTexto = new RectangleF(tx, r.Y, tw, r.Height);
            var st2 = g.Save();
            g.SetClip(rTexto); // el texto nunca se sale de su zona
            Tema.Escribir(g, nombre, fNom, tiene ? Tema.Texto : Color.FromArgb(196, 188, 176), new RectangleF(tx, r.Y + pad - Tema.Px(1), tw, Tema.Px(22)));
            Tema.Escribir(g, detalle, fDet, Tema.Tenue, new RectangleF(tx, r.Y + pad + Tema.Px(22), tw, r.Height - pad * 2 - Tema.Px(20)), StringAlignment.Near, true);
            g.Restore(st2);

            // columna derecha: grado y estado, alineados a la derecha (como en bbport)
            float cx = r.Right - pad - col;
            Tema.Escribir(g, Datos.NombreGrado(t.Grado), fChico, tiene ? cg : Color.FromArgb(150, cg), new RectangleF(cx, r.Y + pad, col, Tema.Px(20)), StringAlignment.Far, false, StringAlignment.Center);
            string estado = tiene ? Fecha.Amigable(t.Obtenido.Value) : "Bloqueado";
            Tema.Escribir(g, estado, fFecha, tiene ? Tema.Vivo : Tema.Tenue, new RectangleF(cx, r.Y + pad + Tema.Px(22), col, Tema.Px(18)), StringAlignment.Far, false, StringAlignment.Center);
            Tema.Escribir(g, t.Puntos + " pts", fFecha, Color.FromArgb(105, 96, 88), new RectangleF(cx, r.Y + pad + Tema.Px(38), col, Tema.Px(16)), StringAlignment.Far, false, StringAlignment.Center);
        }
    }
}
