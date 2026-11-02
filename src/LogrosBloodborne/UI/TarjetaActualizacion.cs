using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LogrosBB
{
    // Actualizaciones dentro de la configuración, en una tarjeta de tamaño fijo: buscar, ver la versión
    // nueva, confirmar e instalar, todo en el mismo lugar. Ícono, textos y botones tienen posición
    // fija, así nada se mueve ni empuja el diseño al cambiar de estado. Nunca instala sin confirmar.
    class TarjetaActualizacion : Pintado
    {
        readonly App app;
        readonly Boton b1, b2;
        readonly Timer reloj = new Timer { Interval = 40 };
        App.EstadoAct visto = (App.EstadoAct)(-1);
        float giro;

        public TarjetaActualizacion(App a, int ancho)
        {
            app = a;
            Size = new Size(ancho, Tema.Px(104));
            b1 = new Boton("") { Primario = true, Detras = Tema.Panel, Size = new Size(Tema.Px(150), Tema.Px(34)) };
            b2 = new Boton("") { Detras = Tema.Panel, Size = new Size(Tema.Px(120), Tema.Px(34)) };
            int m = Tema.Px(24);
            b1.Location = new Point(Width - m - b1.Width, (Height - b1.Height) / 2);
            b2.Location = new Point(b1.Left - Tema.Px(8) - b2.Width, b1.Top);
            b1.Click += delegate { Accion(true); };
            b2.Click += delegate { Accion(false); };
            Controls.Add(b1); Controls.Add(b2);
            reloj.Tick += delegate
            {
                if (app.Act != visto) Configurar();
                if (Ocupado) { giro = (giro + 14) % 360; Invalidate(new Rectangle(0, 0, Tema.Px(80), Height)); }
            };
            Configurar();
            reloj.Start();
        }

        bool Ocupado { get { return app.Act == App.EstadoAct.Buscando || app.Act == App.EstadoAct.Descargando; } }

        protected override void Dispose(bool disposing) { if (disposing) { reloj.Stop(); reloj.Dispose(); } base.Dispose(disposing); }

        void Configurar()
        {
            visto = app.Act;
            b2.Visible = false; b1.Enabled = true; b1.Primario = false;
            switch (app.Act)
            {
                case App.EstadoAct.Buscando: b1.Text = "Buscando…"; b1.Enabled = false; break;
                case App.EstadoAct.AlDia: b1.Text = "Buscar de nuevo"; break;
                case App.EstadoAct.Hay: b1.Text = "Instalar"; b1.Primario = true; b2.Text = "Novedades"; b2.Visible = true; break;
                case App.EstadoAct.Confirmar: b1.Text = "Sí, instalar"; b1.Primario = true; b2.Text = "Ahora no"; b2.Visible = true; break;
                case App.EstadoAct.Descargando: b1.Text = "Instalando…"; b1.Enabled = false; break;
                case App.EstadoAct.Error: b1.Text = app.Novedad != null ? "Reintentar" : "Buscar de nuevo"; break;
                default: b1.Text = "Buscar ahora"; break;
            }
            b1.Invalidate(); b2.Invalidate();
            Invalidate();
        }

        void Accion(bool principal)
        {
            switch (app.Act)
            {
                case App.EstadoAct.Hay:
                    if (principal) app.PedirActualizar();
                    else try { Process.Start(App.PaginaReleases); } catch { }
                    break;
                case App.EstadoAct.Confirmar:
                    if (principal) app.Actualizar(); else app.CancelarActualizar();
                    break;
                case App.EstadoAct.Error:
                    if (app.Novedad != null) app.PedirActualizar(); else app.BuscarActualizacion(true);
                    break;
                case App.EstadoAct.Buscando:
                case App.EstadoAct.Descargando:
                    break;
                default:
                    app.BuscarActualizacion(true);
                    break;
            }
            Configurar();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.Clear(Tema.Fondo); Tema.Calidad(g);
            var r = new RectangleF(.5f, .5f, Width - 1.5f, Height - 1.5f);
            bool destacar = app.Act == App.EstadoAct.Hay || app.Act == App.EstadoAct.Confirmar;
            using (var p = Tema.Redondo(r, Tema.Px(8)))
            using (var br = new SolidBrush(Tema.Panel))
            using (var pen = new Pen(destacar ? Color.FromArgb(150, Tema.Oro) : Tema.Borde)) { g.FillPath(br, p); g.DrawPath(pen, p); }

            // ícono de estado, siempre en el mismo lugar
            int m = Tema.Px(24), lado = Tema.Px(30);
            var ri = new RectangleF(m, (Height - lado) / 2f, lado, lado);
            Icono(g, ri);

            string v = App.Version, n = app.Novedad != null ? app.Novedad.Version.ToString() : "";
            string titulo, desc;
            switch (app.Act)
            {
                case App.EstadoAct.Buscando: titulo = "Buscando actualizaciones…"; desc = "Revisando GitHub. Tenés la versión " + v + "."; break;
                case App.EstadoAct.AlDia: titulo = "Estás al día"; desc = "Tenés la última versión (" + v + ")."; break;
                case App.EstadoAct.Hay: titulo = "Versión nueva disponible: " + n; desc = "Tenés la " + v + ". Se descarga, se verifica (SHA-256) y se instala encima: tus trofeos y ajustes se mantienen."; break;
                case App.EstadoAct.Confirmar: titulo = "¿Instalar la versión " + n + "?"; desc = "Se abre el instalador y el programa se cierra. Seguí sus pasos: tus trofeos y ajustes se mantienen."; break;
                case App.EstadoAct.Descargando: titulo = "Instalando la versión " + n + "…"; desc = "Descargando y verificando… después se abre el instalador y el programa se cierra."; break;
                case App.EstadoAct.Error: titulo = app.Novedad != null ? "No se pudo actualizar" : "No pude buscar actualizaciones"; desc = app.ErrorAct ?? "Probá de nuevo en un rato."; break;
                default: titulo = "Actualizaciones"; desc = "Tenés la versión " + v + ". Nunca se instala nada sin que lo confirmes."; break;
            }
            float tx = ri.Right + Tema.Px(16);
            // el texto siempre tiene el mismo ancho (el de los dos botones), aunque haya uno solo
            float tw = b2.Left - Tema.Px(16) - tx;
            using (var f1 = Tema.F(Tema.SansSemi, 10.3f, FontStyle.Bold))
            using (var f2 = Tema.F(Tema.Sans, 8.8f))
            {
                float ty = Height / 2f - Tema.Px(26);
                Tema.Escribir(g, titulo, f1, destacar ? Tema.Oro : Tema.Texto, new RectangleF(tx, ty, tw, Tema.Px(20)));
                Tema.Escribir(g, desc, f2, app.Act == App.EstadoAct.Error ? Color.FromArgb(222, 120, 112) : Tema.Tenue, new RectangleF(tx, ty + Tema.Px(22), tw, Tema.Px(34)), StringAlignment.Near, true);
            }
        }

        void Icono(Graphics g, RectangleF ri)
        {
            float w = Tema.Px(3);
            if (Ocupado)
            {
                using (var pen = new Pen(Color.FromArgb(50, Tema.Oro), w)) g.DrawEllipse(pen, RectangleF.Inflate(ri, -w, -w));
                using (var pen = new Pen(Tema.Oro, w) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(pen, RectangleF.Inflate(ri, -w, -w), giro, 100);
                return;
            }
            Color c = app.Act == App.EstadoAct.AlDia ? Tema.Vivo : app.Act == App.EstadoAct.Error ? Tema.SangreClara :
                      (app.Act == App.EstadoAct.Hay || app.Act == App.EstadoAct.Confirmar) ? Tema.Oro : Tema.Tenue;
            var rc = RectangleF.Inflate(ri, -w / 2, -w / 2);
            using (var pen = new Pen(c, w / 1.5f)) g.DrawEllipse(pen, rc);
            using (var pen = new Pen(c, w / 1.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                float cx = ri.X + ri.Width / 2, cy = ri.Y + ri.Height / 2, s = ri.Width * .22f;
                if (app.Act == App.EstadoAct.AlDia)
                    g.DrawLines(pen, new[] { new PointF(cx - s, cy), new PointF(cx - s * .25f, cy + s * .75f), new PointF(cx + s * 1.1f, cy - s * .8f) });
                else if (app.Act == App.EstadoAct.Error)
                { g.DrawLine(pen, cx, cy - s, cx, cy + s * .2f); g.DrawLine(pen, cx, cy + s * .85f, cx, cy + s * .9f); }
                else
                { g.DrawLine(pen, cx, cy - s, cx, cy + s); g.DrawLine(pen, cx - s * .8f, cy + s * .2f, cx, cy + s); g.DrawLine(pen, cx + s * .8f, cy + s * .2f, cx, cy + s); }
            }
        }
    }
}
