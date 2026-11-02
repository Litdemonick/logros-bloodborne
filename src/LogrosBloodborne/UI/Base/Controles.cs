using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace LogrosBB
{
    // Base: control pintado a mano, con el color de lo que tiene detrás (para esquinas limpias).
    class Pintado : Control
    {
        public Color Detras = Tema.Fondo;
        protected bool Sobre, Apretado;

        public Pintado()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.StandardDoubleClick, false); // dos clics rápidos = dos clics, ninguno se pierde
            Font = Tema.F(Tema.SansSemi, 9.5f, FontStyle.Bold);
        }
        protected override void OnMouseEnter(EventArgs e) { Sobre = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Sobre = false; Apretado = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { Apretado = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { Apretado = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    }

    class Boton : Pintado
    {
        public bool Primario;
        public Action<Graphics, RectangleF, Color> Dibujo;   // si está, se dibuja este ícono en vez del texto
        DateTime ultimoClic;

        // tocar varias veces seguidas no repite la acción ni hace parpadear nada
        protected override void OnClick(EventArgs e)
        {
            if ((DateTime.Now - ultimoClic).TotalMilliseconds < 350) return;
            ultimoClic = DateTime.Now;
            base.OnClick(e);
        }

        public Boton(string texto) { Text = texto; Cursor = Cursors.Hand; Size = new Size(Tema.Px(110), Tema.Px(34)); }

        // Abre un menú debajo del botón. Si el menú ya estaba abierto, tocar el mismo botón lo cierra
        // (el clic que lo cerró no lo vuelve a abrir).
        DateTime menuCerrado;
        public void Desplegar(ContextMenuStrip m)
        {
            if ((DateTime.Now - menuCerrado).TotalMilliseconds < 500) { menuCerrado = DateTime.MinValue; m.Dispose(); return; }
            m.Closed += (s, e) =>
            {
                if (!IsDisposed && e.CloseReason == ToolStripDropDownCloseReason.AppClicked && ClientRectangle.Contains(PointToClient(Cursor.Position)))
                    menuCerrado = DateTime.Now;
                BeginInvoke((Action)m.Dispose);
            };
            if (m.MinimumSize.Width == 0) m.MinimumSize = new Size(Width, 0);
            m.Show(this, new Point(0, Height + 4));
        }

        public void Ajustar(int minimo = 0)
        {
            using (var g = CreateGraphics())
                Size = new Size(Math.Max(Tema.Px(minimo), (int)g.MeasureString(Text, Font).Width + Tema.Px(32)), Tema.Px(34));
        }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Detras);
            Tema.Calidad(g);
            var r = new RectangleF(.5f, .5f, Width - 1.5f, Height - 1.5f);
            Color fondo, borde, texto;
            if (Primario)
            {
                fondo = Apretado ? Color.FromArgb(120, 26, 30) : Sobre ? Color.FromArgb(150, 34, 38) : Color.FromArgb(132, 28, 32);
                borde = Color.FromArgb(178, 58, 60); texto = Color.FromArgb(246, 236, 220);
            }
            else
            {
                fondo = Apretado ? Tema.Panel : Sobre ? Tema.PanelAlto : Tema.Panel;
                borde = Sobre ? Color.FromArgb(78, 68, 72) : Tema.Borde; texto = Tema.Texto;
            }
            if (!Enabled) { texto = Tema.Tenue; borde = Tema.Borde; fondo = Tema.Panel; }
            using (var p = Tema.Redondo(r, Tema.Px(6)))
            using (var br = new SolidBrush(fondo))
            using (var pen = new Pen(borde, 1)) { g.FillPath(br, p); g.DrawPath(pen, p); }
            if (Primario && Enabled)
                using (var pen = new Pen(Color.FromArgb(40, 255, 255, 255), 1)) g.DrawLine(pen, r.X + Tema.Px(6), r.Y + 1, r.Right - Tema.Px(6), r.Y + 1);
            if (Dibujo != null)
            {
                float lado = Math.Min(r.Width, r.Height) * .5f;
                Dibujo(g, new RectangleF(r.X + (r.Width - lado) / 2, r.Y + (r.Height - lado) / 2, lado, lado), Sobre && Enabled ? Tema.Oro : texto);
            }
            else Tema.Escribir(g, Text, Font, texto, r, StringAlignment.Center, false, StringAlignment.Center);
        }
    }

    // Pestañas con contador y subrayado dorado, como en el launcher de bbport
    class Pestanas : Pintado
    {
        public readonly List<string[]> Items = new List<string[]>(); // clave, texto, contador
        public string Sel;
        public int AnchoFijo;   // si > 0, todas las pestañas miden lo mismo: el diseño no se mueve
        public bool Marco;      // dentro de un marco redondeado, con la elegida resaltada
        public bool Linea;      // dibuja la línea separadora de abajo (la que el control taparía)
        public event Action<string> Cambio;
        int sobreI = -1;
        readonly List<RectangleF> zonas = new List<RectangleF>();

        public Pestanas() { Cursor = Cursors.Hand; Font = Tema.F(Tema.SansSemi, 10f, FontStyle.Bold); }

        void Medir(Graphics g)
        {
            zonas.Clear();
            float x = Marco ? Tema.Px(4) : 0;
            float zy = Marco ? Tema.Px(11) : 0, zh = Marco ? Height - Tema.Px(22) : Height;
            using (var fc = Tema.F(Tema.Sans, 9.5f))
                foreach (var it in Items)
                {
                    float w = AnchoFijo > 0 ? Tema.Px(AnchoFijo) : g.MeasureString(it[1], Font).Width + (it[2] != "" ? g.MeasureString(it[2], fc).Width + Tema.Px(2) : 0) + Tema.Px(10);
                    zonas.Add(new RectangleF(x, zy, w, zh));
                    x += w + (AnchoFijo > 0 || Marco ? (Marco ? Tema.Px(2) : 0) : Tema.Px(18));
                }
        }

        public int AnchoTotal()
        {
            using (var g = CreateGraphics()) { Medir(g); return zonas.Count == 0 ? 0 : (int)zonas[zonas.Count - 1].Right + (Marco ? Tema.Px(5) : 2); }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int s = zonas.FindIndex(z => z.Contains(e.Location));
            if (s != sobreI) { sobreI = s; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); sobreI = -1; Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (zonas.Count == 0) using (var g = CreateGraphics()) Medir(g);
            int s = zonas.FindIndex(z => z.Contains(e.Location));
            if (s >= 0 && Items[s][0] != Sel) { Sel = Items[s][0]; Invalidate(); if (Cambio != null) Cambio(Sel); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Detras);
            Tema.Calidad(g);
            Medir(g);
            if (Linea) using (var pen = new Pen(Tema.Borde)) g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            if (Marco && zonas.Count > 0)
            {
                var caja = new RectangleF(.5f, Tema.Px(7) + .5f, zonas[zonas.Count - 1].Right + Tema.Px(4), Height - Tema.Px(14) - 1);
                using (var p = Tema.Redondo(caja, Tema.Px(9)))
                using (var br = new SolidBrush(Tema.Panel)) using (var pen = new Pen(Tema.Borde)) { g.FillPath(br, p); g.DrawPath(pen, p); }
            }
            using (var fc = Tema.F(Tema.Sans, 9.5f))
                for (int i = 0; i < Items.Count; i++)
                {
                    var z = zonas[i];
                    bool act = Items[i][0] == Sel;
                    if (Marco && (act || i == sobreI))
                        using (var p = Tema.Redondo(z, Tema.Px(7)))
                        using (var br = new SolidBrush(act ? Tema.PanelAlto : Color.FromArgb(28, 22, 25)))
                        using (var pen = new Pen(act ? Color.FromArgb(150, Tema.Oro) : Tema.Borde)) { g.FillPath(br, p); g.DrawPath(pen, p); }
                    Color c = act ? Tema.Texto : (i == sobreI ? Color.FromArgb(205, 196, 182) : Tema.Tenue);
                    float tw = g.MeasureString(Items[i][1], Font).Width;
                    float cw = Items[i][2] != "" ? g.MeasureString(Items[i][2], fc).Width : 0;
                    float x0 = AnchoFijo > 0 ? z.X + (z.Width - tw - cw - (cw > 0 ? Tema.Px(5) : 0)) / 2 : z.X;
                    float ty = Marco ? z.Y : 0, th = Marco ? z.Height : Height - Tema.Px(6);
                    Tema.Escribir(g, Items[i][1], Font, c, new RectangleF(x0, ty, tw + 4, th), StringAlignment.Near, false, StringAlignment.Center);
                    if (Items[i][2] != "")
                        Tema.Escribir(g, Items[i][2], fc, act ? Tema.Oro : Color.FromArgb(120, 110, 100), new RectangleF(x0 + tw + Tema.Px(5), ty, cw + 4, th), StringAlignment.Near, false, StringAlignment.Center);
                    float ux = AnchoFijo > 0 ? z.X + Tema.Px(10) : z.X, uw = AnchoFijo > 0 ? z.Width - Tema.Px(20) : z.Width - Tema.Px(6);
                    if (act && !Marco) using (var br = new SolidBrush(Tema.Oro)) g.FillRectangle(br, ux, Height - Tema.Px(3), uw, Tema.Px(2));
                }
        }
    }

    class Interruptor : Pintado
    {
        bool on;
        public event Action<bool> Cambio;
        public bool Encendido { get { return on; } set { on = value; Invalidate(); } }

        public Interruptor() { Size = new Size(Tema.Px(46), Tema.Px(26)); Cursor = Cursors.Hand; }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            on = !on; Invalidate();
            if (Cambio != null) Cambio(on);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Detras);
            Tema.Calidad(g);
            var r = new RectangleF(1, 1, Width - 3, Height - 3);
            using (var p = Tema.Redondo(r, r.Height / 2))
            using (var br = new SolidBrush(on ? Color.FromArgb(132, 28, 32) : Tema.PanelAlto))
            using (var pen = new Pen(on ? Color.FromArgb(178, 58, 60) : Tema.Borde)) { g.FillPath(br, p); g.DrawPath(pen, p); }
            float d = r.Height - Tema.Px(8);
            float x = on ? r.Right - d - Tema.Px(4) : r.X + Tema.Px(4);
            using (var br = new SolidBrush(on ? Color.FromArgb(246, 236, 220) : Tema.Tenue)) g.FillEllipse(br, x, r.Y + Tema.Px(4), d, d);
        }
    }

    // Barra deslizable: el valor cambia solo agarrando y arrastrando (la rueda desplaza la página; un clic suelto no cambia nada)
    class Deslizador : Pintado
    {
        public int Min, Max = 100, Paso = 1;
        int val;
        public Func<int, string> Formato = v => v.ToString();
        public event Action<int> Cambio;      // mientras se arrastra
        public event Action<int> Soltado;     // al soltar (para probar el sonido)
        bool arrastrando;

        public int Valor { get { return val; } set { val = Math.Max(Min, Math.Min(Max, value)); Invalidate(); } }

        public Deslizador() { Size = new Size(Tema.Px(250), Tema.Px(28)); Cursor = Cursors.Hand; }

        RectangleF Pista { get { return new RectangleF(Tema.Px(8), Height / 2f - Tema.Px(2), Width - Tema.Px(96), Tema.Px(4)); } }

        void Poner(int x)
        {
            var p = Pista;
            double t = Math.Max(0, Math.Min(1, (x - p.X) / p.Width));
            int v = Min + (int)Math.Round((Max - Min) * t / Paso) * Paso;
            if (v != val) { val = Math.Max(Min, Math.Min(Max, v)); Invalidate(); if (Cambio != null) Cambio(val); }
        }

        int desdeX, desdeVal; bool movido;
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button != MouseButtons.Left) return; arrastrando = true; movido = false; desdeX = e.X; desdeVal = val; }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!arrastrando) return;
            if (!movido && Math.Abs(e.X - desdeX) < Tema.Px(4)) return; // un clic suelto no cambia el valor
            movido = true; Poner(e.X);
        }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); if (arrastrando && movido && val != desdeVal && Soltado != null) Soltado(val); arrastrando = false; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Detras);
            Tema.Calidad(g);
            var p = Pista;
            float t = Max == Min ? 0 : (val - Min) / (float)(Max - Min);
            using (var pr = Tema.Redondo(p, p.Height / 2)) using (var br = new SolidBrush(Tema.PanelAlto)) g.FillPath(br, pr);
            if (t > 0) using (var pr = Tema.Redondo(new RectangleF(p.X, p.Y, Math.Max(p.Height, p.Width * t), p.Height), p.Height / 2)) using (var br = new SolidBrush(Tema.Oro)) g.FillPath(br, pr);
            float d = Tema.Px(Sobre || arrastrando ? 16 : 14);
            float cx = p.X + p.Width * t;
            using (var br = new SolidBrush(Color.FromArgb(80, 0, 0, 0))) g.FillEllipse(br, cx - d / 2, Height / 2f - d / 2 + 1.5f, d, d);
            using (var br = new SolidBrush(Color.FromArgb(246, 236, 220))) g.FillEllipse(br, cx - d / 2, Height / 2f - d / 2, d, d);
            using (var pen = new Pen(Tema.Oro, Tema.Px(2))) g.DrawEllipse(pen, cx - d / 2, Height / 2f - d / 2, d, d);
            using (var f = Tema.F(Tema.SansSemi, 9.5f, FontStyle.Bold))
                Tema.Escribir(g, Formato(val), f, Tema.Texto, new RectangleF(p.Right + Tema.Px(10), 0, Width - p.Right - Tema.Px(10), Height), StringAlignment.Far, false, StringAlignment.Center);
        }
    }

    // Opciones excluyentes en una sola pieza (ej.: 12 h / 24 h)
    class Segmentado : Pintado
    {
        public string[] Opciones = new string[0];
        int sel, sobreI = -1;
        public event Action<int> Cambio;
        public int Seleccion { get { return sel; } set { sel = value; Invalidate(); } }

        public Segmentado() { Size = new Size(Tema.Px(250), Tema.Px(32)); Cursor = Cursors.Hand; Font = Tema.F(Tema.SansSemi, 9f, FontStyle.Bold); }

        int Indice(int x) { return Opciones.Length == 0 ? -1 : Math.Max(0, Math.Min(Opciones.Length - 1, x * Opciones.Length / Math.Max(1, Width))); }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); int s = Indice(e.X); if (s != sobreI) { sobreI = s; Invalidate(); } }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); sobreI = -1; }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int s = Indice(e.X);
            if (s >= 0 && s != sel) { sel = s; Invalidate(); if (Cambio != null) Cambio(sel); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Detras);
            Tema.Calidad(g);
            var r = new RectangleF(.5f, .5f, Width - 1.5f, Height - 1.5f);
            using (var p = Tema.Redondo(r, Tema.Px(6))) using (var br = new SolidBrush(Tema.Fondo)) using (var pen = new Pen(Tema.Borde)) { g.FillPath(br, p); g.DrawPath(pen, p); }
            float w = r.Width / Math.Max(1, Opciones.Length);
            for (int i = 0; i < Opciones.Length; i++)
            {
                var ri = new RectangleF(r.X + w * i + Tema.Px(3), r.Y + Tema.Px(3), w - Tema.Px(6), r.Height - Tema.Px(6));
                if (i == sel) using (var p = Tema.Redondo(ri, Tema.Px(4))) using (var br = new SolidBrush(Color.FromArgb(132, 28, 32))) g.FillPath(br, p);
                else if (i == sobreI) using (var p = Tema.Redondo(ri, Tema.Px(4))) using (var br = new SolidBrush(Tema.PanelAlto)) g.FillPath(br, p);
                Tema.Escribir(g, Opciones[i], Font, i == sel ? Color.FromArgb(246, 236, 220) : Tema.Tenue, ri, StringAlignment.Center, false, StringAlignment.Center);
            }
        }
    }
    // Barra de desplazamiento vertical propia: finita, dorada, se agranda al pasar el mouse
    class BarraScroll : Pintado
    {
        public int Contenido, Vista, Valor;
        public event Action<int> Cambio;
        bool arrastra; int agarre;

        public int Maximo { get { return Math.Max(0, Contenido - Vista); } }

        RectangleF Pulgar()
        {
            if (Contenido <= Vista || Height <= 0) return RectangleF.Empty;
            float h = Math.Max(Tema.Px(40), Height * (Vista / (float)Contenido));
            float y = (Height - h) * (Valor / (float)Math.Max(1, Maximo));
            float w = Tema.Px(Sobre || arrastra ? 6 : 4);
            return new RectangleF((Width - w) / 2f, y, w, h);
        }

        public void Poner(int v)
        {
            v = Math.Max(0, Math.Min(Maximo, v));
            if (v == Valor) return;
            Valor = v; Invalidate();
            if (Cambio != null) Cambio(v);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            var p = Pulgar();
            if (p.IsEmpty) return;
            if (e.Y >= p.Y && e.Y <= p.Bottom) { arrastra = true; agarre = e.Y - (int)p.Y; }
            else Poner(Valor + (e.Y < p.Y ? -Vista : Vista) * 9 / 10);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!arrastra) return;
            var p = Pulgar();
            float t = (e.Y - agarre) / Math.Max(1f, Height - p.Height);
            Poner((int)(t * Maximo));
        }

        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); arrastra = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Detras);
            Tema.Calidad(g);
            var p = Pulgar();
            if (p.IsEmpty) return;
            using (var br = new SolidBrush(Color.FromArgb(28, 255, 255, 255)))
            using (var pr = Tema.Redondo(new RectangleF(p.X, 0, p.Width, Height), p.Width / 2)) g.FillPath(br, pr);
            using (var br = new SolidBrush(Color.FromArgb(Sobre || arrastra ? 230 : 150, Tema.Oro)))
            using (var pr = Tema.Redondo(p, p.Width / 2)) g.FillPath(br, pr);
        }
    }
}
