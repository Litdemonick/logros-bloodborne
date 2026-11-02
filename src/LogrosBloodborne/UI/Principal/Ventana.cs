using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LogrosBB
{
    class Ventana : Form
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);

        readonly App app;
        public readonly Cabecera Cab = new Cabecera();
        public readonly Lista Lista = new Lista();
        readonly Panel barra = new Panel();
        readonly Pintado pie = new Pintado();
        readonly Pestanas tabs = new Pestanas();
        readonly Boton bOrden, bConfig, bJugar, bActualizar;
        readonly TextBox buscar = new TextBox();
        readonly Panel cajaBuscar;
        readonly Pintado panelSync = new Pintado();
        Configuracion config;
        public bool EnConfig { get { return config != null && config.Visible; } }

        public void RefrescarConfig() { if (EnConfig) config.Mostrar(); }

        public void RefrescarPie()
        {
            bActualizar.Text = app.Novedad != null ? "Actualizar" : "Agregar clave…";
            bActualizar.Visible = app.Novedad != null || app.SinNombres;
            pie.Invalidate();
        }

        public void MostrarConfig(string tab = null)
        {
            if (EnConfig) { config.IrA(tab); config.Mostrar(); return; }
            Fundido.Hacer(this, () =>
            {
                SuspendLayout();
                if (config == null)
                {
                    config = new Configuracion(app) { Dock = DockStyle.Fill };
                    config.Volver += CerrarConfig;
                    Controls.Add(config);
                }
                // en Configuración no va la portada grande: todo el alto queda para las opciones
                Cab.Visible = false;
                config.Visible = true;
                panelSync.Visible = Lista.Visible = barra.Visible = false;
                config.BringToFront();
                config.IrA(tab);
                ResumeLayout(true);
                config.Mostrar();
            });
        }

        public void CerrarConfig()
        {
            if (!EnConfig) return;
            Fundido.Hacer(this, () =>
            {
                SuspendLayout();
                config.Visible = false;
                Cab.Visible = true;
                ResumeLayout(true);
                Refrescar();
            });
        }
        readonly Boton bSync;
        readonly System.Windows.Forms.Timer animar = new System.Windows.Forms.Timer { Interval = 16 };
        readonly Stopwatch cron = new Stopwatch();

        public Ventana(App a)
        {
            app = a;
            Text = Edicion.Dev ? "Logros · Bloodborne (Dev)" : "Logros · Bloodborne";
            try { Icon = App.IconoPrograma; } catch { }
            BackColor = Tema.Fondo;
            ForeColor = Tema.Texto;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            Font = Tema.F(Tema.Sans, 9.5f);
            // diseño estático: tamaño fijo (se achica solo si la pantalla no da)
            var area = Screen.FromPoint(Cursor.Position).WorkingArea;
            // se puede agrandar libremente; nunca más chica que el diseño, así nada se encima
            FormBorderStyle = FormBorderStyle.Sizable;
            ClientSize = new Size(Math.Min(Tema.Px(960), area.Width - Tema.Px(40)), Math.Min(Tema.Px(760), area.Height - Tema.Px(60)));
            // alto mínimo: que siempre entren la cabecera, la barra y varias filas (no se "come" nada)
            MinimumSize = new Size(Math.Min(Size.Width, area.Width), Math.Min(Tema.Px(700), area.Height));
            MaximizeBox = true;
            int m = Tema.Px(24);

            Cab.App = app;
            Cab.Dock = DockStyle.Top;
            Cab.Height = Tema.Px(200);

            barra.Dock = DockStyle.Top;
            barra.Height = Tema.Px(56);
            barra.BackColor = Tema.Fondo;
            barra.Paint += (s, e) => { using (var pen = new Pen(Tema.Borde)) e.Graphics.DrawLine(pen, m, barra.Height - 1, barra.Width - m, barra.Height - 1); };

            tabs.Sel = "todos";
            tabs.AnchoFijo = 112;
            tabs.Marco = true;
            tabs.Linea = true;
            tabs.Cambio += k => { Lista.Filtro = k; Lista.Arriba(); Refrescar(); };
            barra.Controls.Add(tabs);

            buscar.BorderStyle = BorderStyle.None;
            buscar.BackColor = Tema.Panel;
            buscar.ForeColor = Tema.Texto;
            buscar.Font = Tema.F(Tema.Sans, 10f);
            SetCue(buscar, "Buscar trofeo…   Ctrl+F");
            buscar.MaxLength = 60;
            buscar.ShortcutsEnabled = true;
            // el texto solo se compara con los nombres en memoria (no hay base de datos ni nada que se ejecute);
            // igual se limpia: sin caracteres de control ni espacios de más
            buscar.TextChanged += delegate
            {
                var limpio = new string(buscar.Text.Where(ch => !char.IsControl(ch)).ToArray());
                limpio = System.Text.RegularExpressions.Regex.Replace(limpio, "\\s+", " ");
                if (limpio != buscar.Text) { int pos = buscar.SelectionStart; buscar.Text = limpio; buscar.SelectionStart = Math.Min(pos, limpio.Length); return; }
                Lista.Busqueda = limpio.Trim(); Lista.Arriba(); Lista.Rearmar(); cajaBuscar.Invalidate();
            };
            buscar.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { buscar.Text = ""; e.SuppressKeyPress = true; } };
            cajaBuscar = new Panel { BackColor = Tema.Panel, Size = new Size(Tema.Px(206), Tema.Px(34)), Padding = new Padding(Tema.Px(12), Tema.Px(8), Tema.Px(10), 0) };
            cajaBuscar.Paint += (s, e) =>
            {
                Tema.Calidad(e.Graphics);
                e.Graphics.Clear(Tema.Fondo);
                bool foco = buscar.Focused || buscar.Text != "";
                using (var p = Tema.Redondo(new RectangleF(.5f, .5f, cajaBuscar.Width - 1.5f, cajaBuscar.Height - 1.5f), Tema.Px(6)))
                using (var br = new SolidBrush(Tema.Panel)) using (var pen = new Pen(foco ? Tema.Oro : Tema.Borde)) { e.Graphics.FillPath(br, p); e.Graphics.DrawPath(pen, p); }
            };
            buscar.GotFocus += delegate { cajaBuscar.Invalidate(); };
            buscar.LostFocus += delegate { cajaBuscar.Invalidate(); };
            buscar.Dock = DockStyle.Fill;
            cajaBuscar.Controls.Add(buscar);
            barra.Controls.Add(cajaBuscar);

            bOrden = new Boton("Orden");
            bOrden.Width = Tema.Px(96);
            bOrden.Click += delegate { bOrden.Desplegar(MenuOrden()); };
            barra.Controls.Add(bOrden);
            bConfig = new Boton("Configuración") { Dibujo = Tema.Engranaje };
            bConfig.Width = Tema.Px(42);
            new ToolTip().SetToolTip(bConfig, "Configuración");
            bConfig.Click += delegate { app.AbrirConfig(); };
            barra.Controls.Add(bConfig);
            barra.Resize += delegate { Ubicar(); };

            bJugar = new Boton("Jugar") { Primario = true };
            bJugar.Ajustar(110);
            bJugar.Click += delegate { app.AbrirJuego(); };
            Cab.Controls.Add(bJugar);

            pie.Dock = DockStyle.Bottom;
            pie.Height = Tema.Px(32);
            pie.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.Clear(Color.FromArgb(17, 14, 16));
                Tema.Calidad(g);
                using (var pen = new Pen(Tema.Borde)) g.DrawLine(pen, 0, 0, pie.Width, 0);
                using (var f = Tema.F(Tema.Sans, 8.5f))
                {
                    if (app.Novedad == null && app.SinNombres)
                    {
                        Tema.Escribir(g, app.BuscandoClave ? "Tus trofeos están sincronizados. Buscando la clave para ver los nombres…"
                                                            : "Tus trofeos están sincronizados, pero sin nombres: falta la clave de trofeos.", f, Tema.Oro,
                            new RectangleF(m, 0, pie.Width - m * 2 - bActualizar.Width - Tema.Px(12), pie.Height), StringAlignment.Near, false, StringAlignment.Center);
                        return;
                    }
                    if (app.Novedad != null)
                    {
                        Tema.Escribir(g, "Hay una versión nueva de Logros Bloodborne: " + app.Novedad.Version + " (tenés " + App.Version + ")", f, Tema.Oro,
                            new RectangleF(m, 0, pie.Width - m * 2 - bActualizar.Width - Tema.Px(12), pie.Height), StringAlignment.Near, false, StringAlignment.Center);
                        return;
                    }
                    var fallidos = app.AtajosFallidos;
                    string t = !app.Ajustes.Atajos ? "Atajos de teclado desactivados  ·  Configuración → General"
                        : fallidos.Count > 0 ? "Atajos ocupados por otro programa: " + string.Join(", ", fallidos)
                        : "Ctrl+Alt+L  lista     ·     Ctrl+Alt+O  resumen en el juego     ·     Ctrl+Alt+M  mover aviso     ·     Ctrl+Alt+S  sonido";
                    Tema.Escribir(g, t, f, Tema.Tenue, new RectangleF(m, 0, pie.Width - m * 2, pie.Height), StringAlignment.Center, false, StringAlignment.Center);
                }
            };

            bActualizar = new Boton("Actualizar") { Primario = true, Detras = Color.FromArgb(17, 14, 16), Size = new Size(Tema.Px(120), Tema.Px(26)), Visible = false };
            bActualizar.Font = Tema.F(Tema.SansSemi, 8.8f, FontStyle.Bold);
            bActualizar.Click += delegate { if (app.Novedad != null) app.PedirActualizar(); else if (app.SinNombres) app.AgregarClave(); };
            pie.Controls.Add(bActualizar);
            pie.Resize += delegate { bActualizar.Location = new Point(pie.Width - m - bActualizar.Width, (pie.Height - bActualizar.Height) / 2); };

            Lista.Dock = DockStyle.Fill;
            Lista.Ajustes = app.Ajustes;

            // Primera vez: nada salta solo; el jugador conecta con bbport cuando quiere.
            panelSync.Dock = DockStyle.Fill;
            bSync = new Boton("Sincronizar con bbport") { Primario = true, Detras = Tema.Panel };
            bSync.Font = Tema.F(Tema.SansSemi, 10.5f, FontStyle.Bold);
            bSync.Ajustar(240); bSync.Height = Tema.Px(42);
            bSync.Click += delegate { app.PrimeraSincronizacion(); };
            panelSync.Controls.Add(bSync);
            // antes de sincronizar se elige cómo entran los trofeos que ya tenés (es el mismo ajuste que
            // Configuración → Notificaciones → Trofeos que te perdiste)
            segPrimera = new Segmentado { Opciones = new[] { "Uno por uno", "Resumen", "Sin avisos" }, Detras = Tema.Panel, Width = Tema.Px(330) };
            segPrimera.Cambio += i => { var aj = app.Ajustes; aj.SaltarPerdidos = i == 2; if (i < 2) aj.UnoPorUno = i == 0; aj.Guardar(); panelSync.Invalidate(); };
            panelSync.Controls.Add(segPrimera);
            panelSync.Paint += (s, e) =>
            {
                var g = e.Graphics; g.Clear(Tema.Fondo); Tema.Calidad(g);
                var r = TarjetaSync();
                using (var p = Tema.Redondo(r, Tema.Px(10))) using (var br = new SolidBrush(Tema.Panel)) using (var pen = new Pen(Tema.Borde)) { g.FillPath(br, p); g.DrawPath(pen, p); }
                using (var f1 = Tema.F(Tema.Serif, 17f, FontStyle.Bold))
                using (var f2 = Tema.F(Tema.Sans, 10f))
                using (var f3 = Tema.F(Tema.Sans, 8.8f))
                {
                    Tema.Escribir(g, "Conectá tus trofeos con bbport", f1, Tema.Texto, new RectangleF(r.X, r.Y + Tema.Px(30), r.Width, Tema.Px(34)), StringAlignment.Center);
                    Tema.Escribir(g, TextoSync, f2, Tema.Tenue,
                        new RectangleF(r.X + Tema.Px(48), r.Y + Tema.Px(72), r.Width - Tema.Px(96), descSync), StringAlignment.Center, true);
                    var aj = app.Ajustes;
                    string modo = aj.SaltarPerdidos ? "Los trofeos que ya tenés entran sin avisos (solo se cuentan)"
                                : aj.UnoPorUno ? "Los trofeos que ya tenés salen en orden, uno por uno" : "Los trofeos que ya tenés salen en un resumen";
                    Tema.Escribir(g, modo, f3, Tema.Oro, new RectangleF(r.X, segPrimera.Top - Tema.Px(24), r.Width, Tema.Px(18)), StringAlignment.Center);
                    Tema.Escribir(g, "bbport:  " + app.Bbport + "\nJuego:  " + app.JuegoDir, f3, Color.FromArgb(120, 110, 100),
                        new RectangleF(r.X + Tema.Px(40), bSync.Bottom + Tema.Px(22), r.Width - Tema.Px(80), Tema.Px(38)), StringAlignment.Center, true);
                    Tema.Escribir(g, "Solo se lee el registro de trofeos de bbport. No se tocan tus partidas.", f3, Color.FromArgb(120, 110, 100),
                        new RectangleF(r.X, r.Bottom - Tema.Px(40), r.Width, Tema.Px(20)), StringAlignment.Center);
                }
            };
            panelSync.Resize += delegate
            {
                // la descripción se mide: el botón y las rutas van debajo según su alto real (nada se corta)
                float w = Math.Min(Tema.Px(600), panelSync.Width - Tema.Px(48)) - Tema.Px(96);
                using (var g = panelSync.CreateGraphics()) using (var f2 = Tema.F(Tema.Sans, 10f))
                    descSync = (int)Math.Ceiling(g.MeasureString(TextoSync, f2, (int)Math.Max(20, w)).Height) + Tema.Px(4);
                var r = TarjetaSync();
                int y0 = (int)r.Y + Tema.Px(72) + descSync + Tema.Px(16) + Tema.Px(24);
                segPrimera.Location = new Point((int)(r.X + (r.Width - segPrimera.Width) / 2), y0);
                bSync.Location = new Point((int)(r.X + (r.Width - bSync.Width) / 2), segPrimera.Bottom + Tema.Px(18));
                panelSync.Invalidate();
            };

            Controls.Add(panelSync);
            Controls.Add(Lista);
            Controls.Add(pie);
            Controls.Add(barra);
            Controls.Add(Cab);

            animar.Tick += delegate
            {
                Cab.Anim = 1 - Math.Pow(1 - Math.Min(1, cron.ElapsedMilliseconds / 900.0), 3);
                Cab.Invalidate();
                if (cron.ElapsedMilliseconds > 950) animar.Stop();
            };
            KeyPreview = true;
        }

        const string TextoSync = "Se leen los trofeos que ya conseguiste en bbport. Después se sincroniza solo, mientras el programa esté abierto, y cada trofeo nuevo sale sobre el juego.";
        int descSync = Tema.Px(44);
        readonly Segmentado segPrimera;

        RectangleF TarjetaSync()
        {
            // título, descripción medida, cómo entran los que ya tenés, botón, rutas y la nota de abajo
            float w = Math.Min(Tema.Px(600), panelSync.Width - Tema.Px(48)),
                  h = Tema.Px(72) + descSync + Tema.Px(40) + Tema.Px(32) + Tema.Px(18) + Tema.Px(42) + Tema.Px(22) + Tema.Px(38) + Tema.Px(64);
            return new RectangleF((panelSync.Width - w) / 2, Math.Max(Tema.Px(24), (panelSync.Height - h) / 2 - Tema.Px(20)), w, h);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Control && e.KeyCode == Keys.F) { buscar.Focus(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape && EnConfig) CerrarConfig();
            else if (e.KeyCode == Keys.Escape && !buscar.Focused) Hide();
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, string l);
        static void SetCue(TextBox t, string s) { t.HandleCreated += delegate { SendMessage(t.Handle, 0x1501, (IntPtr)1, s); }; }

        void Ubicar()
        {
            int m = Tema.Px(24), alto = Tema.Px(34);
            int y = (barra.Height - alto) / 2;
            int d = barra.ClientSize.Width - m;
            bConfig.Location = new Point(d - bConfig.Width, y); d -= bConfig.Width + Tema.Px(8);
            bOrden.Location = new Point(d - bOrden.Width, y); d -= bOrden.Width + Tema.Px(8);
            cajaBuscar.Location = new Point(d - cajaBuscar.Width, y); d -= cajaBuscar.Width + Tema.Px(16);
            tabs.Location = new Point(m, 0);
            tabs.Size = new Size(Math.Max(Tema.Px(100), d - m), barra.Height);
            bJugar.Location = new Point(Cab.Width - m - bJugar.Width, Cab.Height - bJugar.Height - Tema.Px(14));
        }

        ContextMenuStrip MenuOrden()
        {
            var mn = App.MenuOscuro();
            foreach (var par in new[] { new[] { "juego", "Orden del juego" }, new[] { "tipo", "Por grado (platino → bronce)" }, new[] { "recientes", "Últimos conseguidos primero" }, new[] { "nombre", "Por nombre (A-Z)" } })
            {
                var it = new ToolStripMenuItem(par[1]) { Checked = Lista.Orden == par[0], Tag = par[0] };
                it.Click += (s, e) => { Lista.Orden = (string)((ToolStripItem)s).Tag; Refrescar(); };
                mn.Items.Add(it);
            }
            mn.Items.Add(new ToolStripSeparator());
            var oc = new ToolStripMenuItem("Mostrar trofeos ocultos (spoilers)") { Checked = app.Ajustes.MostrarOcultos };
            oc.Click += delegate { app.Ajustes.MostrarOcultos = !app.Ajustes.MostrarOcultos; app.Ajustes.Guardar(); Refrescar(); };
            mn.Items.Add(oc);
            return mn;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int v = 1; DwmSetWindowAttribute(Handle, 20, ref v, 4); } catch { }       // barra de título oscura
            try { int c = 0x00302A30; DwmSetWindowAttribute(Handle, 34, ref c, 4); } catch { }   // borde sutil (Win11)
            try { int c = 0x000E0C0E; DwmSetWindowAttribute(Handle, 35, ref c, 4); } catch { }   // título del color del fondo (Win11)
        }

        protected override void OnShown(EventArgs e) { base.OnShown(e); Ubicar(); cron.Restart(); animar.Start(); }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) { cron.Restart(); animar.Start(); }
        }

        public void Refrescar()
        {
            var j = app.Juego;
            Lista.Juego = j;
            bool sinc = app.Ajustes.Sincronizado;
            RefrescarPie();
            bActualizar.Location = new Point(pie.Width - Tema.Px(24) - bActualizar.Width, (pie.Height - bActualizar.Height) / 2);
            if (EnConfig) { Cab.Invalidate(); pie.Invalidate(); RefrescarConfig(); return; }
            panelSync.Visible = !sinc; Lista.Visible = sinc; barra.Visible = sinc;
            if (!sinc) { panelSync.BringToFront(); segPrimera.Seleccion = app.Ajustes.SaltarPerdidos ? 2 : app.Ajustes.UnoPorUno ? 0 : 1; panelSync.Invalidate(); }
            int ob = j.Obtenidos, tot = j.Trofeos.Count;
            tabs.Items.Clear();
            tabs.Items.Add(new[] { "todos", "Todos", tot.ToString() });
            tabs.Items.Add(new[] { "obtenidos", "Obtenidos", ob.ToString() });
            tabs.Items.Add(new[] { "faltantes", "Me faltan", (tot - ob).ToString() });
            tabs.Items.Add(new[] { "recientes", "Recientes", "" });
            tabs.Sel = Lista.Filtro;
            tabs.Invalidate();
            bOrden.Enabled = Lista.Filtro != "recientes";
            bJugar.Visible = !app.JuegoAbierto && sinc;
            Ubicar();
            Lista.Rearmar();
            Cab.Invalidate();
            pie.Invalidate();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Cerrar la ventana no cierra el programa: sigue en segundo plano escuchando trofeos.
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); app.AvisoBandeja(); return; }
            base.OnFormClosing(e);
        }
    }
}
