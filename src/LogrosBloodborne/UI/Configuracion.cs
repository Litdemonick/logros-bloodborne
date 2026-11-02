using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace LogrosBB
{
    // Configuración dentro de la misma ventana (reemplaza a la lista mientras está abierta)
    class Configuracion : Pintado, IMessageFilter
    {
        readonly BarraScroll scroll = new BarraScroll();
        int alto; // alto total del contenido
        readonly App app;
        public event Action Volver;
        readonly Panel marco = new Panel();
        readonly Pestanas tabs = new Pestanas();
        readonly Panel cuerpo = new Panel();
        int y;
        const int AnchoCtl = 260;

        public Configuracion(App a)
        {
            app = a;
            BackColor = Tema.Fondo;
            var cab = new Pintado { Dock = DockStyle.Top, Height = Tema.Px(100) };
            cab.Paint += (s, e) =>
            {
                var g = e.Graphics; g.Clear(Tema.Fondo); Tema.Calidad(g);
                using (var f = Tema.F(Tema.Serif, 15f, FontStyle.Bold))
                    Tema.Escribir(g, "CONFIGURACIÓN", f, Tema.Texto, new RectangleF(0, Tema.Px(12), cab.Width, Tema.Px(34)), StringAlignment.Center, false, StringAlignment.Center);
                using (var pen = new Pen(Tema.Borde)) g.DrawLine(pen, Tema.Px(24), cab.Height - 1, cab.Width - Tema.Px(24), cab.Height - 1);
            };
            var volver = new Boton("‹  Volver") { Size = new Size(Tema.Px(110), Tema.Px(32)) };
            volver.Click += delegate { if (Volver != null) Volver(); };
            volver.Location = new Point(Tema.Px(24), Tema.Px(13));
            cab.Controls.Add(volver);
            tabs.Items.Add(new[] { "avisos", "Notificaciones", "" });
            tabs.Items.Add(new[] { "sonido", "Sonido", "" });
            tabs.Items.Add(new[] { "sync", "Sincronización", "" });
            tabs.Items.Add(new[] { "general", "General", "" });
            tabs.Items.Add(new[] { "acerca", "Acerca de", "" });
            tabs.Sel = "avisos";
            tabs.AnchoFijo = 126;
            tabs.Height = Tema.Px(40);
            tabs.Linea = true;
            tabs.Cambio += k => Armar();
            cab.Controls.Add(tabs);
            cab.Resize += delegate { int w = tabs.AnchoTotal(); tabs.Width = w; tabs.Location = new Point((cab.Width - w) / 2, cab.Height - tabs.Height - 1); };

            // el contenido va en una columna centrada de ancho fijo: simétrico aunque la ventana se agrande
            marco.Dock = DockStyle.Fill;
            marco.BackColor = Tema.Fondo;
            cuerpo.BackColor = Tema.Fondo;
            marco.Controls.Add(cuerpo);
            marco.Controls.Add(scroll);
            scroll.Cambio += v => Ubicar();
            marco.Resize += delegate { Centrar(); };
            Application.AddMessageFilter(this); // rueda del mouse sobre cualquier parte de la configuración
            Controls.Add(marco);
            Controls.Add(cab);
        }

        int anchoArmado;
        void Centrar()
        {
            int w = Math.Min(Tema.Px(720), marco.Width - Tema.Px(48));
            cuerpo.Width = w;
            cuerpo.Left = (marco.Width - w) / 2;
            if (w != anchoArmado) { anchoArmado = w; Armar(); }
            Ubicar();
        }

        // solo vertical: el contenido conserva su ancho y su diseño; se desplaza si no entra en la ventana
        void Ubicar()
        {
            scroll.Contenido = alto + Tema.Px(16);
            scroll.Vista = marco.Height;
            scroll.Valor = Math.Min(scroll.Valor, scroll.Maximo);
            scroll.Visible = scroll.Maximo > 0;
            scroll.SetBounds(marco.Width - Tema.Px(18), Tema.Px(6), Tema.Px(12), Math.Max(0, marco.Height - Tema.Px(12)));
            int top = scroll.Maximo > 0 ? Tema.Px(8) - scroll.Valor : Math.Max(Tema.Px(8), (marco.Height - alto) / 2);
            cuerpo.SetBounds(cuerpo.Left, top, cuerpo.Width, alto);
            scroll.Invalidate();
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != 0x020A || !Visible || !IsHandleCreated || scroll.Maximo == 0) return false;
            var pos = marco.PointToClient(Cursor.Position);
            if (!marco.ClientRectangle.Contains(pos)) return false;
            var bajo = Control.FromChildHandle(m.HWnd);
            if (bajo is Deslizador) return false; // la rueda sobre un deslizador cambia su valor
            int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
            scroll.Poner(scroll.Valor - delta / 120 * Tema.Px(66));
            return true;
        }

        protected override void Dispose(bool disposing) { Application.RemoveMessageFilter(this); base.Dispose(disposing); }

        public void Mostrar() { Centrar(); Armar(); }

        // Una fila: título + descripción a la izquierda, control a la derecha, todo alineado
        // Una opción = una tarjeta (como las filas de trofeos): título y descripción a la izquierda,
        // el control a la derecha, con el mismo margen interno y la misma separación en todas.
        void Fila(string titulo, string desc, Control ctl, int alto = 76)
        {
            int m = Tema.Px(24);
            float twM = cuerpo.ClientSize.Width - m * 2 - (ctl != null ? ctl.Width + Tema.Px(28) : 0);
            float descH = 0;
            if (!string.IsNullOrEmpty(desc))
                using (var gm = cuerpo.CreateGraphics())
                using (var f2m = Tema.F(Tema.Sans, 8.8f))
                    descH = gm.MeasureString(desc, f2m, (int)Math.Max(20, twM)).Height + Tema.Px(2);
            int altoFila = Math.Max(Tema.Px(alto), (int)(Tema.Px(21) + descH + Tema.Px(32)));
            var fila = new Pintado { Location = new Point(0, y), Size = new Size(cuerpo.ClientSize.Width, altoFila) };
            fila.Paint += (s, e) =>
            {
                var g = e.Graphics; g.Clear(Tema.Fondo); Tema.Calidad(g);
                var r = new RectangleF(.5f, .5f, fila.Width - 1.5f, fila.Height - 1.5f);
                using (var p = Tema.Redondo(r, Tema.Px(8))) using (var br = new SolidBrush(Tema.Panel)) using (var pen = new Pen(Tema.Borde)) { g.FillPath(br, p); g.DrawPath(pen, p); }
                float tw = fila.Width - m * 2 - (ctl != null ? ctl.Width + Tema.Px(28) : 0);
                using (var f1 = Tema.F(Tema.SansSemi, 10.3f, FontStyle.Bold))
                using (var f2 = Tema.F(Tema.Sans, 8.8f))
                {
                    bool conDesc = !string.IsNullOrEmpty(desc);
                    float bloque = Tema.Px(20) + (conDesc ? Tema.Px(1) + descH : 0);
                    float ty = (fila.Height - bloque) / 2f;
                    Tema.Escribir(g, titulo, f1, Tema.Texto, new RectangleF(m, ty, tw, Tema.Px(20)));
                    if (conDesc) Tema.Escribir(g, desc, f2, Tema.Tenue, new RectangleF(m, ty + Tema.Px(21), tw, descH + Tema.Px(2)), StringAlignment.Near, true);
                }
            };
            if (ctl != null)
            {
                SobreTarjeta(ctl);
                ctl.Location = new Point(fila.Width - m - ctl.Width, (fila.Height - ctl.Height) / 2);
                fila.Controls.Add(ctl);
            }
            cuerpo.Controls.Add(fila);
            y += fila.Height + Tema.Px(10);
        }

        // los controles pintan sus esquinas con el color de la tarjeta: bordes limpios
        static void SobreTarjeta(Control c)
        {
            var pc = c as Pintado;
            if (pc != null) pc.Detras = Tema.Panel;
            else c.BackColor = Tema.Panel;
            foreach (Control h in c.Controls) SobreTarjeta(h);
        }

        Control Grupo(params Control[] ctls)
        {
            var p = new Panel { BackColor = Tema.Fondo };
            int x = 0, h = 0;
            foreach (var c in ctls) { c.Location = new Point(x, 0); p.Controls.Add(c); x += c.Width + Tema.Px(8); h = Math.Max(h, c.Height); }
            p.Size = new Size(x - Tema.Px(8), h);
            return p;
        }

        // Acción que se confirma tocando dos veces el mismo botón (sin ventanas aparte)
        Boton Confirmar(string texto, Action al, int min)
        {
            Boton b = null;
            b = Btn(texto, () =>
            {
                if (b.Text != "¿Seguro?")
                {
                    b.Text = "¿Seguro?";
                    var t = new Timer { Interval = 4000 };
                    t.Tick += delegate { t.Stop(); t.Dispose(); if (!b.IsDisposed) b.Text = texto; };
                    t.Start();
                    return;
                }
                b.Text = texto;
                al();
            }, min);
            return b;
        }

        void Agregar(Control c)
        {
            c.Location = new Point(0, y);
            cuerpo.Controls.Add(c);
            y += c.Height + Tema.Px(10);
        }

        public void IrA(string tab) { if (tab != null && tabs.Items.Any(t => t[0] == tab)) { tabs.Sel = tab; tabs.Invalidate(); } }

        static void Abrir(string destino) { try { Process.Start(destino); } catch { } }

        Interruptor Switch(bool v, Action<bool> al) { var s = new Interruptor { Encendido = v }; s.Cambio += al; return s; }

        Boton Btn(string t, Action al, int min = 0) { var b = new Boton(t); b.Ajustar(min); b.Click += delegate { al(); }; return b; }

        Segmentado Seg(string[] ops, int sel, Action<int> al, int ancho = AnchoCtl)
        {
            var s = new Segmentado { Opciones = ops, Seleccion = sel, Width = Tema.Px(ancho) };
            s.Cambio += al; return s;
        }

        Boton Desplegable(string[][] ops, Func<string> actual, Action<string> al, int ancho = AnchoCtl)
        {
            Func<string> texto = () => { var o = ops.FirstOrDefault(x => x[0] == actual()); return (o != null ? o[1] : "—") + "   ▾"; };
            var b = new Boton(texto()) { Width = Tema.Px(ancho), Height = Tema.Px(34) };
            b.Click += delegate
            {
                var m = App.MenuOscuro();
                foreach (var o in ops)
                {
                    string k = o[0];
                    var it = new ToolStripMenuItem(o[1]) { Checked = actual() == k };
                    it.Click += delegate { al(k); b.Text = texto(); };
                    m.Items.Add(it);
                }
                m.MinimumSize = new Size(b.Width, 0);
                m.Show(b, new Point(0, b.Height + 4));
            };
            return b;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

        void Armar()
        {
            // se rearma tapado (sin redibujar a medias): cambiar de pestaña o tocar botones no parpadea
            bool tapar = cuerpo.IsHandleCreated;
            if (tapar) SendMessage(cuerpo.Handle, 0x000B, IntPtr.Zero, IntPtr.Zero);
            cuerpo.SuspendLayout();
            foreach (Control c in cuerpo.Controls.Cast<Control>().ToList()) c.Dispose();
            cuerpo.Controls.Clear();
            y = Tema.Px(14);
            var aj = app.Ajustes;
            switch (tabs.Sel)
            {
                case "avisos":
                    Fila("Mostrar notificaciones", "Apagadas, los trofeos esperan y salen al volver a prenderlas.",
                        Switch(aj.Avisos, v => app.CambiarAvisos(v)));
                    Fila("Posición", "Dónde aparece el aviso sobre el juego.",
                        Desplegable(new[] { new[] { "arriba-centro", "Arriba al centro" }, new[] { "arriba-izquierda", "Arriba a la izquierda" }, new[] { "arriba-derecha", "Arriba a la derecha" },
                                            new[] { "abajo-izquierda", "Abajo a la izquierda" }, new[] { "abajo-derecha", "Abajo a la derecha" } }
                            .Concat(aj.Posicion == "libre" ? new[] { new[] { "libre", "Donde lo dejaste (a mano)" } } : new string[0][]).ToArray(),
                            () => aj.Posicion, k => { if (k == "libre") return; aj.Posicion = k; aj.Guardar(); app.Probar('X'); }));
                    Fila("Mover a mano", "Arrastrá el aviso a donde quieras y tocá Guardar. «Al centro» lo vuelve arriba al centro.",
                        Grupo(Btn("Mover el aviso…", () => app.MoverAviso(), 150), Btn("Al centro", () => app.RestablecerPosicion(), 102)));
                    var tam = new Deslizador { Min = 70, Max = 150, Paso = 5, Valor = (int)Math.Round(aj.Tamano * 100), Width = Tema.Px(AnchoCtl), Formato = v => v + "%" };
                    tam.Soltado += v => { aj.Tamano = v / 100.0; aj.Guardar(); app.Probar('X'); };
                    Fila("Tamaño", "Se adapta solo a tu pantalla; esto lo agranda o achica.", tam);
                    var dur = new Deslizador { Min = 3, Max = 15, Valor = aj.Segundos, Width = Tema.Px(AnchoCtl), Formato = v => v + " s" };
                    dur.Soltado += v => { aj.Segundos = v; aj.Guardar(); };
                    Fila("Duración en pantalla", "Cuántos segundos queda cada trofeo.", dur);
                    var vel = new Deslizador { Min = 50, Max = 200, Paso = 10, Valor = (int)Math.Round(aj.Velocidad * 100), Width = Tema.Px(AnchoCtl),
                        Formato = v => v < 80 ? "Lenta" : v <= 120 ? "Normal" : v <= 160 ? "Rápida" : "Muy rápida" };
                    vel.Soltado += v => { aj.Velocidad = v / 100.0; aj.Guardar(); app.Probar('X'); };
                    Fila("Velocidad de la animación", "Qué tan rápido aparece y se va cada aviso.", vel);
                    Fila("Probar", null, Btn("Probar notificación", () => app.Probar('X'), AnchoCtl));
                    break;

                case "sonido":
                    Fila("Sonido", "Cada grado y cada hito tiene su propio sonido.", Switch(aj.Sonido, v => { aj.Sonido = v; aj.Guardar(); }));
                    var vol = new Deslizador { Min = 0, Max = 100, Paso = 5, Valor = aj.Volumen, Width = Tema.Px(AnchoCtl), Formato = v => v + "%" };
                    vol.Soltado += v => { aj.Volumen = v; aj.Guardar(); Sonido.Tocar('G', v, app.UserDir); };
                    Fila("Volumen", "60% es un buen punto: se escucha sin tapar el juego.", vol);
                    Fila("Probar trofeos", null, Grupo(Btn("Bronce", () => Sonido.Tocar('B', aj.Volumen, app.UserDir), 60), Btn("Plata", () => Sonido.Tocar('S', aj.Volumen, app.UserDir), 60),
                                                       Btn("Oro", () => Sonido.Tocar('G', aj.Volumen, app.UserDir), 60), Btn("Platino", () => Sonido.Tocar('P', aj.Volumen, app.UserDir), 60)));
                    Fila("Probar hitos", null, Grupo(Btn("25-90%", () => Sonido.Tocar('H', aj.Volumen, app.UserDir), 60), Btn("Bronces", () => Sonido.Tocar('b', aj.Volumen, app.UserDir), 60), Btn("Platas", () => Sonido.Tocar('s', aj.Volumen, app.UserDir), 56),
                                                     Btn("Oros", () => Sonido.Tocar('g', aj.Volumen, app.UserDir), 50), Btn("DLC", () => Sonido.Tocar('d', aj.Volumen, app.UserDir), 46)));
                    Fila("Sonidos propios", "trophy / platinum / hito (.wav o .mp3) en bbport\\user\\custom_trophy.", Btn("Abrir carpeta", () => app.CarpetaSonidos(), 140));
                    break;

                case "sync":
                    Fila(aj.Sincronizado ? "Sincronizado con bbport" : "Sin sincronizar",
                        aj.Sincronizado ? app.Juego.Obtenidos + " de " + app.Juego.Trofeos.Count + " trofeos · se sincroniza solo al abrir el programa." : "Tocá el botón para traer tus trofeos de bbport.",
                        Btn(aj.Sincronizado ? "Sincronizar ahora" : "Sincronizar", () => { app.SincronizarAhora(); Armar(); }, 160));
                    if (aj.Sincronizado)
                        Fila("Rehacer desde cero", "Vuelve a leer todos tus trofeos de bbport. Elegí si querés ver los avisos de nuevo o solo recontar.",
                            Grupo(Confirmar("Ver avisos", () => app.PrimeraSincronizacion(true), 112),
                                  Confirmar("Sin avisos", () => app.PrimeraSincronizacion(false), 112)));
                    Fila("Avisos en espera", "Si hay muchos trofeos esperando para salir, cancelalos: quedan como vistos.",
                        Btn("Cancelar avisos", () => app.CancelarAvisos(), 160));
                    Fila("Trofeos que te perdiste", "Ganados con el programa cerrado o los avisos apagados.",
                        Seg(new[] { "Uno por uno", "Resumen", "No mostrar" }, aj.SaltarPerdidos ? 2 : aj.UnoPorUno ? 0 : 1,
                            i => { aj.SaltarPerdidos = i == 2; if (i < 2) aj.UnoPorUno = i == 0; aj.Guardar(); }));
                    Fila("Respaldo", "Guardá tus trofeos en un archivo o sumá los de otra instalación.",
                        Grupo(Btn("Exportar…", () => app.Exportar(), 120), Btn("Importar…", () => app.Importar(), 120)));
                    Fila("Carpeta de bbport", app.Bbport, Btn("Cambiar…", () => app.CambiarCarpeta(), 120));
                    if (app.SinNombres)
                        Fila("Clave de trofeos: falta", app.BuscandoClave
                                ? "Tus trofeos se sincronizan igual. La estoy buscando en tus discos y pendrives…"
                                : "Tus trofeos se sincronizan igual, sin nombres. Elegí keys.json o config.toml de la carpeta user de un bbport donde ande.",
                            Btn("Elegir archivo…", () => app.AgregarClave(), 140));
                    else if (Datos.Clave(app.UserDir) != null) // sin clave los nombres salen de la lista incluida: no hay nada que exportar
                        Fila("Clave de trofeos: lista", "Para usar la app en otra PC, exportala (por ejemplo a un pendrive).",
                            Btn("Exportar clave…", () => app.ExportarClave(), 140));
                    break;

                default:
                    Fila("Idioma de los trofeos", "«Igual que bbport» sigue el idioma que elegiste en el launcher.",
                        Desplegable(new[] { new[] { "auto", "Igual que bbport" }, new[] { "20", "Español (Latinoamérica)" }, new[] { "3", "Español (España)" }, new[] { "1", "English" }, new[] { "17", "Português (Brasil)" },
                                            new[] { "2", "Français" }, new[] { "4", "Deutsch" }, new[] { "5", "Italiano" } },
                            () => aj.Idioma, k => app.CambiarIdioma(k)));
                    Fila("Formato de hora", "«Windows» usa el mismo formato que el reloj de tu PC.", Seg(new[] { "Windows", "12 h", "24 h" }, aj.Hora == "12" ? 1 : aj.Hora == "24" ? 2 : 0,
                        i => { aj.Hora = i == 1 ? "12" : i == 2 ? "24" : "auto"; Fecha.Formato = aj.Hora; aj.Guardar(); app.RefrescarVentana(); }));
                    Fila("Mostrar trofeos ocultos", "Muestra nombre y descripción de los secretos (spoilers).", Switch(aj.MostrarOcultos, v => { aj.MostrarOcultos = v; aj.Guardar(); app.RefrescarVentana(); }));
                    Fila("Atajos de teclado", "Ctrl+Alt+L lista · O resumen · M mover · S sonido · T probar", Switch(aj.Atajos, v => app.CambiarAtajos(v)));
                    Fila("Iniciar con Windows", "Queda en segundo plano y nunca te perdés un trofeo.", Switch(App.IniciaConWindows, v => App.IniciaConWindows = v));
                    break;

                case "acerca":
                    Fila("Logros Bloodborne " + App.Version, "Trofeos sobre el juego para bbport. Proyecto de fans, no oficial: no está afiliado a Sony, FromSoftware ni bbport.", null);
                    Agregar(new TarjetaActualizacion(app, cuerpo.ClientSize.Width));
                    Fila("Buscar automáticamente", "Revisa GitHub al abrir y una vez por día. Es lo único que usa internet.",
                        Switch(aj.Actualizaciones, v => { aj.Actualizaciones = v; aj.Guardar(); }));
                    Fila("Código y novedades", "Código abierto (licencia MIT) en GitHub.",
                        Grupo(Btn("Ver en GitHub", () => Abrir(App.PaginaRepo), 130), Btn("Novedades", () => Abrir(App.PaginaReleases), 110)));
                    Fila("¿Algo no anda?", "Contalo en GitHub y adjuntá errores.log (está en «Ver mis datos»).",
                        Grupo(Btn("Reportar un problema", () => Abrir(App.PaginaRepo + "/issues"), 170), Btn("Ver mis datos", () => Abrir(App.CarpetaDatosPublica), 120)));
                    Fila("Tus datos", "Solo se lee el registro de trofeos de bbport; no se tocan tus partidas ni los archivos del juego.", null);
                    break;
            }
            // al final de cada pestaña: volver esa zona a los valores de fábrica (se confirma en el mismo botón)
            string sec = tabs.Sel;
            Fila("Valores de fábrica", "Vuelve solo esta sección a como venía (tocá dos veces). No toca tus trofeos ni la carpeta de bbport.",
                Confirmar("Restablecer", () => app.RestablecerSeccion(sec), 140));
            alto = y + Tema.Px(4);
            cuerpo.ResumeLayout();
            Ubicar();
            if (tapar) { SendMessage(cuerpo.Handle, 0x000B, (IntPtr)1, IntPtr.Zero); cuerpo.Invalidate(true); }
        }
    }
}
