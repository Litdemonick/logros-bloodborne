using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LogrosBB
{
    class App : ApplicationContext
    {
        public static Icon IconoPrograma;
        public const string PaginaRepo = "https://github.com/Litdemonick/logros-bloodborne";
        public const string PaginaReleases = PaginaRepo + "/releases";
        public static string CarpetaDatosPublica { get { return CarpetaDatos; } }
        public static string Version { get { return Actualizador.Actual.ToString(); } }
        public Actualizador.Novedad Novedad;  // versión nueva disponible (o null)
        public static SynchronizationContext ContextoUI;
        public Ajustes Ajustes;
        public Juego Juego;
        public string Bbport, JuegoDir, UserDir;
        public bool JuegoAbierto;            // algún proceso de bbport (launcher o juego)
        public bool EnJuego;                 // la ventana del juego (SDL) está abierta
        public DateTime? SesionDesde;        // inicio de la sesión de juego actual
        readonly bool mostrarAlInicio;
        bool avisoBandejaMostrado;
        readonly NotifyIcon bandeja = new NotifyIcon();
        Ventana ventana;
        readonly System.Windows.Forms.Timer reloj = new System.Windows.Forms.Timer { Interval = 1000 };
        readonly Dictionary<int, DateTime> conocidos = new Dictionary<int, DateTime>();
        readonly HashSet<int> vistos = new HashSet<int>();
        DateTime ultimoLog = DateTime.MinValue; long largoLog = -1;
        int encogido;                        // lecturas seguidas con menos trofeos (anti-lectura a medias)
        int ticks;
        int pidJuego; DateTime inicioProceso; DateTime? finVentana;
        ContextMenuStrip menu;
        Atajos atajos;

        static string CarpetaDatos
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LogrosBloodborne"); }
        }

        public App(bool mostrarAlInicio, EventWaitHandle mostrar)
        {
            this.mostrarAlInicio = mostrarAlInicio;
            Ajustes = Ajustes.Cargar();
            Aviso.Ajustes = Ajustes;
            Aviso.ZonaJuego = ZonaJuego;
            Aviso.JuegoAlFrente = JuegoAlFrente;
            Fecha.Formato = Ajustes.Hora;

            if (!Preparar()) { Salir(); return; }

            bandeja.Icon = IconoPrograma;
            bandeja.Text = "Logros Bloodborne · en segundo plano";
            bandeja.Visible = true;
            bandeja.ContextMenuStrip = Menu();
            bandeja.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) AlternarVentana(); };

            atajos = new Atajos(this);
            if (Ajustes.Atajos) atajos.Registrar();

            Revisar();
            if (Ajustes.Sincronizado) Sincronizar(true);

            reloj.Tick += delegate { Tick(); };
            reloj.Start();

            // Otra copia (acceso directo, menú inicio) pidió mostrar la ventana
            var t = new Thread(() =>
            {
                while (true)
                {
                    mostrar.WaitOne();
                    try { ContextoUI.Post(_ => MostrarVentana(), null); } catch { }
                }
            }) { IsBackground = true };
            t.Start();

            if (mostrarAlInicio) MostrarVentana();

            // actualizaciones: a los 20 s de abrir y después una vez por día, sin trabar nada
            var buscarAct = new System.Windows.Forms.Timer { Interval = 20000 };
            buscarAct.Tick += delegate { buscarAct.Interval = 24 * 3600 * 1000; BuscarActualizacion(false); };
            buscarAct.Start();
            if (Environment.GetCommandLineArgs().Any(x => x == "--config")) AbrirConfig();
        }

        // ------------------------------------------------------------ preparación

        bool Preparar()
        {
            Bbport = Datos.BuscarBbport(Ajustes);
            while (Bbport == null)
            {
                string d;
                using (var b = new Buscador()) { if (b.ShowDialog() != DialogResult.OK) return false; d = b.Elegida; }
                Ajustes.Bbport = d;
                Bbport = Datos.BuscarBbport(Ajustes);
                if (Bbport == null) MessageBox.Show("Esa carpeta no parece ser de bbport (tiene que tener Bloodborne.exe y la carpeta user).", "Logros Bloodborne", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            Ajustes.Bbport = Bbport;
            Tema.CargarFuentes(Bbport);
            UserDir = Datos.CarpetaUser(Bbport);
            Aviso.UserDir = UserDir;
            JuegoDir = Datos.BuscarJuego(Ajustes, Bbport);
            while (JuegoDir == null)
            {
                if (MessageBox.Show("No encontré los archivos del juego.\n\nElegí la carpeta del juego (la que tiene eboot.bin y sce_sys, por ejemplo CUSA03173).", "Logros Bloodborne", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return false;
                string d = Elegir("Carpeta del juego (CUSA03173)");
                if (d == null) return false;
                Ajustes.JuegoDir = d;
                JuegoDir = Datos.BuscarJuego(Ajustes, Bbport);
            }
            Ajustes.JuegoDir = JuegoDir;
            Ajustes.Guardar();
            return CargarJuego();
        }

        bool CargarJuego()
        {
            string error;
            Juego j = null;
            try { j = Datos.Cargar(JuegoDir, UserDir, Ajustes.Idioma, out error); }
            catch (Exception ex) { error = "No pude leer los trofeos: " + ex.Message; }
            if (j == null) { MessageBox.Show(error, "Logros Bloodborne", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
            Juego = j;
            Aplicar();
            return true;
        }

        static string Elegir(string desc)
        {
            using (var f = new FolderBrowserDialog { Description = desc, ShowNewFolderButton = false })
                return f.ShowDialog() == DialogResult.OK ? f.SelectedPath : null;
        }

        void Aplicar()
        {
            if (Juego == null) return;
            foreach (var t in Juego.Trofeos) t.Obtenido = conocidos.ContainsKey(t.Id) ? (DateTime?)conocidos[t.Id] : null;
        }

        // ------------------------------------------------------------ sincronización con bbport

        // Trofeos ya mostrados, guardados por carpeta de bbport. Así, si conseguís trofeos con este
        // programa cerrado, aparecen apenas lo abrís; y nunca se repite uno ya mostrado.
        string ArchivoVistos
        {
            get
            {
                string clave = Convert.ToBase64String(Encoding.UTF8.GetBytes(UserDir.ToLowerInvariant())).Replace('/', '_').Replace('+', '-').TrimEnd('=');
                if (clave.Length > 60) clave = clave.Substring(clave.Length - 60);
                return Path.Combine(CarpetaDatos, "vistos-" + clave + ".txt");
            }
        }

        bool CargarVistos()
        {
            try
            {
                var lineas = Disco.Leer(ArchivoVistos);
                if (lineas == null) return File.Exists(ArchivoVistos); // vacío = ya sincronizado sin trofeos
                foreach (var l in lineas) { int id; if (int.TryParse(l.Trim(), out id)) vistos.Add(id); }
                return true;
            }
            catch { return false; }
        }

        void GuardarVistos()
        {
            try
            {
                Disco.Escribir(ArchivoVistos, vistos.OrderBy(i => i).Select(i => i.ToString()));
                RespaldoAuto();
            }
            catch { }
        }

        // Lee trophies.log (solo lectura, compartido: nunca bloquea ni toca a bbport) y anuncia lo nuevo.
        public void Sincronizar(bool inicio, bool forzar = false)
        {
            string log = Path.Combine(UserDir, "trophies.log");
            var fi = new FileInfo(log);
            if (!inicio && !forzar && fi.Exists && fi.LastWriteTimeUtc == ultimoLog && fi.Length == largoLog) return;
            var actual = Datos.LeerLog(log);
            if (actual == null) return; // no se pudo leer: reintenta en el próximo segundo

            // Si de golpe hay menos trofeos (bbport reescribiendo el archivo, o se cambió de partida),
            // se espera a que se repita 3 veces seguidas antes de aceptarlo.
            if (!inicio && actual.Count < conocidos.Count && ++encogido < 3) return;
            encogido = 0;
            if (fi.Exists) { ultimoLog = fi.LastWriteTimeUtc; largoLog = fi.Length; }

            bool primeraVez = inicio && !CargarVistos();
            conocidos.Clear();
            foreach (var kv in actual) conocidos[kv.Key] = kv.Value;
            Aplicar();

            if (primeraVez)
            {
                // Primera vez: saludo, y después los trofeos que ya tenías van saliendo en orden (una sola vez).
                if (conocidos.Count == 0) GuardarVistos();
                if (Ajustes.Avisos)
                    Aviso.Mostrar(new Aviso.Pedido
                    {
                        Titulo = "Conectado con bbport",
                        Linea = conocidos.Count == 0 ? "¡A cazar se ha dicho!" : "Sincronizando tus " + conocidos.Count + " trofeos…",
                        Pie = "Te aviso de cada trofeo mientras jugás · Ctrl+Alt+L abre la lista",
                        Grado = 'R', Icono = Juego.IconoJuego, Son = 'H', Rapido = true
                    });
            }

            vistos.IntersectWith(conocidos.Keys); // si bbport los borró (otra partida), se olvidan
            if (!Ajustes.Avisos)
            {
                // notificaciones apagadas: los nuevos quedan pendientes y salen cuando se vuelvan a activar
                if (ventana != null && ventana.Visible) ventana.Refrescar();
                ActualizarPendientes();
                return;
            }
            var nuevos = Juego.Trofeos.Where(t => t.Obtenido != null && !vistos.Contains(t.Id))
                                      .OrderBy(t => t.Grado == 'P' ? 1 : 0).ThenBy(t => t.Obtenido).ToList();
            if (nuevos.Count > 0)
            {
                bool perdidos = nuevos.All(t => (DateTime.Now - t.Obtenido.Value).TotalMinutes > 2);
                int baseCuenta = Juego.Obtenidos - nuevos.Count;
                if (perdidos && !Ajustes.UnoPorUno && nuevos.Count > 3)
                {
                    // muchos de golpe: un resumen + los 3 mejores
                    Aviso.Mostrar(new Aviso.Pedido
                    {
                        Titulo = "Mientras no estaba abierto",
                        Linea = "Conseguiste " + nuevos.Count + " trofeos",
                        Pie = "+" + nuevos.Sum(t => t.Puntos) + " puntos · " + Juego.Obtenidos + " de " + Juego.Trofeos.Count,
                        Grado = 'R', Icono = Juego.IconoJuego, Son = 'H'
                    });
                    foreach (var t in nuevos.OrderBy(t => "PGSB".IndexOf(t.Grado)).Take(3)) Anunciar(t, true, -1, true, true);
                }
                else
                {
                    for (int i = 0; i < nuevos.Count; i++)
                    {
                        bool tarde = (DateTime.Now - nuevos[i].Obtenido.Value).TotalMinutes > 2;
                        Anunciar(nuevos[i], true, baseCuenta + i, tarde, tarde);
                    }
                }
                Colecciones(nuevos);
                vistos.UnionWith(nuevos.Select(t => t.Id));
                GuardarVistos();
                ActualizarPendientes();
                if (ventana != null && ventana.Visible) { ventana.Refrescar(); ventana.Lista.Destacar(nuevos[nuevos.Count - 1].Id); }
            }
            else if (ventana != null && ventana.Visible) ventana.Refrescar();
        }

        // Colecciones completas: todos los bronces / platas / oros, y la expansión.
        void Colecciones(List<Trofeo> nuevos)
        {
            foreach (char g in "BSG")
            {
                var deG = Juego.Trofeos.Where(t => t.Grado == g).ToList();
                if (deG.Count == 0 || deG.Any(t => t.Obtenido == null) || !nuevos.Any(t => t.Grado == g)) continue;
                string nom = g == 'B' ? "bronce" : g == 'S' ? "plata" : "oro";
                Hito("¡Todos los trofeos de " + nom + "!", "Colección de " + nom + " completa · " + deG.Count + " de " + deG.Count,
                    g, char.ToLowerInvariant(g), deG.OrderByDescending(t => t.Obtenido).First().Icono);
            }
            foreach (var kv in Juego.Grupos)
            {
                var deG = Juego.Trofeos.Where(t => t.Grupo == kv.Key).ToList();
                if (deG.Count == 0 || deG.Any(t => t.Obtenido == null) || !nuevos.Any(t => t.Grupo == kv.Key)) continue;
                Hito("¡Expansión completa!", kv.Value, 'S', 'd', Juego.IconoGrupo ?? Juego.IconoJuego);
            }
        }

        // Conteo por grado hasta cierto momento (para que los trofeos sincronizados tarde muestren el
        // conteo que había justo cuando se ganaron)
        public int[] Conteo(DateTime? hasta = null)
        {
            var r = new int[8];
            int i = 0;
            foreach (char g in "PGSB")
            {
                var deG = Juego.Trofeos.Where(t => t.Grado == g).ToList();
                r[i * 2] = deG.Count(t => t.Obtenido != null && (!hasta.HasValue || t.Obtenido.Value <= hasta.Value));
                r[i * 2 + 1] = deG.Count;
                i++;
            }
            return r;
        }

        public void Hito(string titulo, string linea, char color, char son, Bitmap icono)
        {
            Aviso.Mostrar(new Aviso.Pedido
            {
                Conteo = Conteo(),
                Titulo = titulo, Linea = linea,
                Pie = Juego.Obtenidos + " de " + Juego.Trofeos.Count + " · " + Juego.Porcentaje + "% · " +
                      (Juego.FaltanPlatino == 0 ? "¡Platino a la vista!" : "faltan " + Juego.FaltanPlatino + " para el Platino"),
                Grado = color, Son = son, Icono = icono
            });
        }

        // ------------------------------------------------------------ detección del juego

        [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint acc, bool inh, int pid);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder s, ref int size);
        delegate bool EnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out int pid);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int size);
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

        readonly HashSet<int> pidsBbport = new HashSet<int>();

        static string RutaProceso(int pid)
        {
            IntPtr h = OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION
            if (h == IntPtr.Zero) return null;
            try
            {
                var sb = new StringBuilder(1024); int n = sb.Capacity;
                return QueryFullProcessImageName(h, 0, sb, ref n) ? sb.ToString() : null;
            }
            finally { CloseHandle(h); }
        }

        readonly HashSet<int> ajenos = new HashSet<int>(); // procesos que ya sabemos que no son de bbport
        DateTime limpiezaAjenos = DateTime.Now;

        void ActualizarProcesos()
        {
            int yo = Process.GetCurrentProcess().Id;
            var pids = new HashSet<int>();
            if ((DateTime.Now - limpiezaAjenos).TotalMinutes > 2) { ajenos.Clear(); limpiezaAjenos = DateTime.Now; }
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.Id == yo || p.Id <= 4 || ajenos.Contains(p.Id)) continue;
                    string f = RutaProceso(p.Id);
                    if (f != null && f.StartsWith(Bbport + "\\", StringComparison.OrdinalIgnoreCase)) pids.Add(p.Id);
                    else if (f != null) ajenos.Add(p.Id);
                }
                catch { }
                finally { p.Dispose(); }
            }
            lock (pidsBbport) { pidsBbport.Clear(); pidsBbport.UnionWith(pids); }
            JuegoAbierto = pids.Count > 0;
        }

        // La ventana del juego: bbport dibuja con SDL (clase "SDL_app"); el launcher es Tk. Funciona en
        // ventana, ventana sin bordes y pantalla completa.
        bool VentanaJuego(out Rectangle zona, out int pidV)
        {
            HashSet<int> pids;
            lock (pidsBbport) pids = new HashSet<int>(pidsBbport);
            Rectangle mejor = Rectangle.Empty; int pidM = 0; long area = -1; bool sdl = false;
            if (pids.Count > 0)
                EnumWindows((h, l) =>
                {
                    int pid;
                    GetWindowThreadProcessId(h, out pid);
                    if (!pids.Contains(pid) || !IsWindowVisible(h)) return true;
                    var cn = new StringBuilder(64); GetClassName(h, cn, 64);
                    string clase = cn.ToString();
                    if (clase == "TkTopLevel" || clase.StartsWith("Tk") || clase == "tooltips_class32") return true;
                    bool esSdl = clase.StartsWith("SDL");
                    RECT r;
                    if (DwmGetWindowAttribute(h, 9, out r, Marshal.SizeOf(typeof(RECT))) != 0) GetWindowRect(h, out r);
                    long ar = (long)(r.R - r.L) * (r.B - r.T);
                    if (IsIconic(h)) ar = 1; // minimizado: sigue siendo el juego, pero sin zona útil
                    if ((esSdl && !sdl) || (esSdl == sdl && ar > area))
                    {
                        sdl = esSdl; area = ar; pidM = pid;
                        mejor = IsIconic(h) ? Rectangle.Empty : Rectangle.FromLTRB(r.L, r.T, r.R, r.B);
                    }
                    return true;
                }, IntPtr.Zero);
            zona = mejor; pidV = pidM;
            return pidM != 0 && (sdl || area > 300 * 200);
        }

        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();

        // ¿El juego es la ventana activa? (no Windows, no el launcher, no otra app)
        public bool JuegoAlFrente()
        {
            IntPtr h = GetForegroundWindow();
            if (h == IntPtr.Zero) return false;
            int pid;
            GetWindowThreadProcessId(h, out pid);
            lock (pidsBbport) if (!pidsBbport.Contains(pid)) return false;
            var cn = new StringBuilder(64); GetClassName(h, cn, 64);
            return cn.ToString().StartsWith("SDL");
        }

        Rectangle? ZonaJuego()
        {
            Rectangle z; int pid;
            if (!VentanaJuego(out z, out pid) || z.Width < 200 || z.Height < 150) return null;
            return z;
        }

        // ------------------------------------------------------------ reloj principal

        void Tick()
        {
            ticks++;
            if (Ajustes.Sincronizado) try { Sincronizar(false); } catch { }
            if (ticks % 3 == 0 || ticks == 1) Revisar();
            if (EnJuego && ticks % 30 == 0) Tiempo.Registrar(inicioProceso, DateTime.Now);
            if (ventana != null && ventana.Visible && EnJuego) ventana.Cab.Invalidate();
            // si se cambia el idioma en el launcher de bbport, los nombres de los trofeos lo siguen
            if (ticks % 10 == 0 && Ajustes.Idioma == "auto")
            {
                int li = Datos.IdiomaJuego();
                if (idiomaVisto == int.MinValue) idiomaVisto = li;
                else if (li != idiomaVisto)
                {
                    idiomaVisto = li;
                    if (CargarJuego()) { if (ventana != null && ventana.Visible) ventana.Refrescar(); Mensaje("Idioma actualizado", "Los trofeos siguen el idioma de bbport"); }
                }
            }
        }
        int idiomaVisto = int.MinValue;

        void Revisar()
        {
            ActualizarProcesos();
            Rectangle z; int pid;
            bool hay = VentanaJuego(out z, out pid);
            bool antes = EnJuego;
            if (hay)
            {
                finVentana = null;
                if (pid != pidJuego)
                {
                    if (pidJuego != 0) Tiempo.Registrar(inicioProceso, DateTime.Now);
                    pidJuego = pid;
                    try { using (var p = Process.GetProcessById(pid)) inicioProceso = p.StartTime; } catch { inicioProceso = DateTime.Now; }
                    if (!SesionDesde.HasValue) SesionDesde = inicioProceso;
                }
                EnJuego = true;
            }
            else if (pidJuego != 0)
            {
                // el juego se reinicia solo al arrancar: se espera 20 s antes de dar la sesión por terminada
                if (!finVentana.HasValue) { finVentana = DateTime.Now; Tiempo.Registrar(inicioProceso, DateTime.Now); }
                if ((DateTime.Now - finVentana.Value).TotalSeconds > 20) TerminoSesion();
            }
            if (antes != EnJuego && ventana != null && ventana.Visible) ventana.Refrescar();
            bandeja.Text = EnJuego ? "Logros Bloodborne · escuchando la cacería" : "Logros Bloodborne · en segundo plano";
        }

        void TerminoSesion()
        {
            pidJuego = 0; SesionDesde = null; finVentana = null; EnJuego = false;
            if (ventana != null && ventana.Visible) ventana.Refrescar();
        }

        // ------------------------------------------------------------ avisos

        // Primera sincronización, a pedido del jugador: después se hace sola cada vez que abre el programa.
        public void PrimeraSincronizacion()
        {
            Ajustes.Sincronizado = true;
            Ajustes.Guardar();
            vistos.Clear();
            foreach (var x in new[] { ArchivoVistos, ArchivoVistos + ".bak" }) try { if (File.Exists(x)) File.Delete(x); } catch { }
            Sincronizar(true);
            AvisarEspera();
            if (ventana != null && ventana.Visible) ventana.Refrescar();
        }

        // Los trofeos solo salen sobre el juego: si estás afuera, avisar que quedaron esperando
        bool AvisarEspera()
        {
            int n = Aviso.EnEspera;
            if (n == 0 || JuegoAlFrente()) return false;
            Mensaje("Sincronizado con bbport", n == 1 ? "1 aviso te espera: va a salir cuando estés en el juego" : n + " avisos te esperan: van a salir cuando estés en el juego");
            return true;
        }

        // Cuántos trofeos esperan ser mostrados (con las notificaciones apagadas)
        public int Pendientes;
        void ActualizarPendientes()
        {
            Pendientes = Juego == null ? 0 : Juego.Trofeos.Count(t => t.Obtenido != null && !vistos.Contains(t.Id));
        }

        public void Anunciar(Trofeo t, bool real, int cuentaAntes = -1, bool perdido = false, bool rapido = false)
        {
            int total = Juego.Trofeos.Count;
            int faltan = Juego.FaltanPlatino;
            int despues = cuentaAntes >= 0 ? cuentaAntes + 1 : Juego.Obtenidos;
            int pct = (int)Math.Floor(100.0 * despues / Math.Max(1, total));
            string pie;
            if (perdido) pie = "Conseguido " + Fecha.Amigable(t.Obtenido.Value) + "  ·  " + despues + " de " + total;
            else if (real && t.Obtenido.HasValue && t.Grado != 'P' && !(t.CuentaPlatino && faltan <= 1)) pie = Fecha.Hora(t.Obtenido.Value) + "  ·  ";
            else pie = "";
            if (perdido) { }
            else if (t.Grado == 'P') pie = "Todos los trofeos. Una cacería que no se olvida.";
            else if (t.CuentaPlatino && faltan == 0) pie = "¡Completaste todo! Ahí viene el Platino…";
            else if (t.CuentaPlatino && faltan == 1) pie = "¡Te falta UNO solo para el Platino!";
            else
            {
                pie += despues + " de " + total + "  ·  " + pct + "%";
                if (t.CuentaPlatino) pie += "  ·  faltan " + faltan + " para el Platino";
            }
            Aviso.Mostrar(new Aviso.Pedido
            {
                Titulo = t.Grado == 'P' ? "¡Platino obtenido!" : "Trofeo " + Datos.NombreGradoDe(t.Grado) + " obtenido" + (real ? "" : " (prueba)"),
                Linea = t.Nombre, Pie = pie, Grado = t.Grado, Icono = t.Icono, Insignia = t.Grado, Rapido = rapido, SoloEnJuego = real,
                Conteo = real && t.Obtenido.HasValue ? Conteo(t.Obtenido) : Conteo()
            });

            // hitos de progreso, con su propia fanfarria
            if (real && cuentaAntes >= 0 && t.Grado != 'P')
            {
                int antesPct = (int)Math.Floor(100.0 * cuentaAntes / Math.Max(1, total));
                foreach (int h in new[] { 90, 75, 50, 25 })
                    if (antesPct < h && pct >= h)
                    {
                        string[] frases = h == 25 ? new[] { "¡25% de la cacería!", "La noche recién empieza" }
                                        : h == 50 ? new[] { "¡Mitad del camino!", "Ya sos un cazador de verdad" }
                                        : h == 75 ? new[] { "¡75% completado!", "La pesadilla ya te conoce" }
                                        : new[] { "¡90%! Casi, casi", "El Platino está a la vuelta" };
                        Hito(frases[0], frases[1], 'R', 'H', Juego.IconoJuego);
                        break;
                    }
            }
        }

        public void Resumen()
        {
            if (Juego == null) return;
            if (!Ajustes.Sincronizado) { Mensaje("Todavía sin sincronizar", "Abrí la lista y tocá «Sincronizar con bbport»"); return; }
            string ses = SesionDesde.HasValue ? "Sesión " + Tiempo.Reloj(DateTime.Now - SesionDesde.Value) + " · " : "";
            Aviso.Mostrar(new Aviso.Pedido
            {
                Titulo = "Tu cacería" + (EnJuego ? " · en vivo" : ""),
                Linea = Juego.Obtenidos + " de " + Juego.Trofeos.Count + " trofeos · " + Juego.Porcentaje + "%",
                Pie = ses + (Juego.FaltanPlatino == 0 ? "¡Platino listo!" : "Faltan " + Juego.FaltanPlatino + " para el Platino") +
                      " · Total " + Tiempo.Largo(Tiempo.Total(EnJuego ? (DateTime?)inicioProceso : null)),
                Grado = 'R', Icono = Juego.IconoJuego, Sonar = false, Resumen = true, SoloEnJuego = false, Conteo = Conteo()
            });
        }

        public void MoverAviso()
        {
            Aviso.Mostrar(new Aviso.Pedido
            {
                Titulo = "Arrastrame a donde quieras",
                Linea = "Acá van a salir tus trofeos",
                Pie = "Doble clic o Enter: guardar · Esc: cancelar · flechas: ajuste fino",
                Grado = 'G', Icono = Juego.IconoJuego, Sonar = false, Mover = true, SoloEnJuego = false,
                AlGuardar = () => { Mensaje("Posición guardada", "Los trofeos van a aparecer ahí"); if (ventana != null) ventana.RefrescarConfig(); }
            });
        }

        public void Mensaje(string titulo, string linea)
        {
            Aviso.Mostrar(new Aviso.Pedido { Titulo = titulo, Linea = linea, Grado = 'R', Icono = Juego.IconoJuego, Sonar = false, Resumen = true, SoloEnJuego = false });
        }

        public void AlternarSonido()
        {
            Ajustes.Sonido = !Ajustes.Sonido; Ajustes.Guardar();
            Mensaje(Ajustes.Sonido ? "Sonido activado" : "Sonido silenciado", Ajustes.Sonido ? "Vas a escuchar cada trofeo" : "Los avisos van a salir sin sonido");
            if (Ajustes.Sonido) Sonido.Tocar('B', Ajustes.Volumen, UserDir);
        }

        public void AvisoBandeja()
        {
            if (avisoBandejaMostrado) return;
            avisoBandejaMostrado = true;
            bandeja.ShowBalloonTip(5000, "Sigo en segundo plano",
                "Te aviso de cada trofeo mientras jugás. Ctrl+Alt+L abre la lista. Para cerrarme del todo: clic derecho en este ícono → Salir.", ToolTipIcon.None);
        }

        // ------------------------------------------------------------ ventana

        public void MostrarVentana()
        {
            if (Juego == null) return;
            if (ventana == null || ventana.IsDisposed) ventana = new Ventana(this);
            ventana.Refrescar();
            if (!ventana.Visible) ventana.Show();
            if (ventana.WindowState == FormWindowState.Minimized) ventana.WindowState = FormWindowState.Normal;
            ventana.Activate();
        }

        public void AlternarVentana()
        {
            if (ventana != null && ventana.Visible && Form.ActiveForm == ventana) ventana.Hide();
            else MostrarVentana();
        }

        public void AbrirConfig()
        {
            MostrarVentana();
            ventana.MostrarConfig();
        }

        public List<string> AtajosFallidos { get { return atajos != null ? atajos.Fallidos : new List<string>(); } }

        public void RefrescarVentana() { if (ventana != null && ventana.Visible) ventana.Refrescar(); }

        public void CambiarAvisos(bool v)
        {
            ActualizarPendientes();
            Ajustes.Avisos = v; Ajustes.Guardar();
            if (v) { Mensaje("Notificaciones activadas", Pendientes > 0 ? "Ahí van los " + Pendientes + " que te perdiste" : "Te aviso de cada trofeo"); if (Ajustes.Sincronizado) Sincronizar(false, true); }
            else Mensaje("Notificaciones desactivadas", "Los trofeos nuevos quedan guardados para después");
        }

        public void SincronizarAhora()
        {
            if (!Ajustes.Sincronizado) { PrimeraSincronizacion(); return; }
            Sincronizar(false, true);
            if (AvisarEspera()) return;
            Mensaje("Sincronizado con bbport", Juego.Obtenidos + " de " + Juego.Trofeos.Count + " trofeos · " + Juego.Porcentaje + "%");
        }

        public void CambiarIdioma(string k)
        {
            Ajustes.Idioma = k; Ajustes.Guardar();
            if (CargarJuego()) RefrescarVentana();
        }

        public void CambiarAtajos(bool v)
        {
            Ajustes.Atajos = v; Ajustes.Guardar();
            if (v) atajos.Registrar(); else atajos.Liberar();
            RefrescarVentana();
        }

        public void CarpetaSonidos()
        {
            string c = Path.Combine(UserDir, "custom_trophy");
            try { Directory.CreateDirectory(c); Process.Start("explorer.exe", "\"" + c + "\""); } catch { }
        }

        public void CambiarCarpeta()
        {
            string d;
            using (var b = new Buscador()) { if (b.ShowDialog() != DialogResult.OK) return; d = b.Elegida; }
            if (!Datos.EsBbport(d)) { MessageBox.Show("Esa carpeta no parece ser de bbport (tiene que tener Bloodborne.exe y la carpeta user).", "Logros Bloodborne"); return; }
            Ajustes.Bbport = d; Ajustes.JuegoDir = ""; Ajustes.Sincronizado = false; Ajustes.Guardar();
            // reiniciar limpio con la carpeta nueva
            try { Process.Start(Assembly.GetExecutingAssembly().Location, "--reinicio"); } catch { }
            Salir();
        }

        // ------------------------------------------------------------ actualizaciones

        public void BuscarActualizacion(bool aPedido)
        {
            if (!aPedido && !Ajustes.Actualizaciones) return;
            new Thread(() =>
            {
                var n = Actualizador.Buscar();
                ContextoUI.Post(_ =>
                {
                    Novedad = n;
                    RefrescarVentana();
                    if (n != null && !EnJuego && !aPedido)
                        bandeja.ShowBalloonTip(6000, "Logros Bloodborne " + n.Version + " disponible", "Abrí la lista para actualizar con un clic.", ToolTipIcon.None);
                    if (aPedido)
                        MessageBox.Show(n != null ? "Hay una versión nueva: " + n.Version + ".\nUsá el botón «Actualizar» para instalarla." : "Tenés la última versión (" + Version + ").", "Logros Bloodborne");
                }, null);
            }) { IsBackground = true }.Start();
        }

        public void Actualizar()
        {
            if (Novedad == null) return;
            if (MessageBox.Show("Se va a descargar la versión " + Novedad.Version + ", el programa se cierra, se actualiza solo y vuelve a abrirse.\nTus trofeos y ajustes se mantienen.\n\n¿Actualizar ahora?",
                    "Logros Bloodborne", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            Cursor.Current = Cursors.WaitCursor;
            string error = Actualizador.Instalar(Novedad);
            Cursor.Current = Cursors.Default;
            if (error != null) { MessageBox.Show(error, "Logros Bloodborne", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            Salir(); // el instalador termina el trabajo y lo vuelve a abrir
        }

        public void AbrirJuego()
        {
            try
            {
                // Si el jugador tiene su propio script de arranque al lado de bbport, se usa ese
                string ps = Path.Combine(Path.GetDirectoryName(Bbport) ?? "", "Jugar-Bloodborne.ps1");
                if (File.Exists(ps))
                    Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"" + ps + "\"") { UseShellExecute = false, CreateNoWindow = true });
                else
                    Process.Start(new ProcessStartInfo(Path.Combine(Bbport, "Bloodborne.exe")) { WorkingDirectory = Bbport, UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show("No pude abrir el juego: " + ex.Message, "Logros Bloodborne"); }
        }

        public string TextoTiempo()
        {
            var total = Tiempo.Total(EnJuego ? (DateTime?)inicioProceso : null);
            var desde = Tiempo.Desde();
            string s = "Tiempo registrado: " + Tiempo.Largo(total);
            if (desde.HasValue) s += " (desde el " + desde.Value.ToString("d/M/yyyy") + ")";
            return s;
        }

        // ------------------------------------------------------------ inicio con Windows

        const string ClaveRun = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool IniciaConWindows
        {
            get
            {
                try { using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(ClaveRun)) return k != null && k.GetValue("LogrosBloodborne") != null; }
                catch { return false; }
            }
            set
            {
                try
                {
                    using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(ClaveRun))
                    {
                        if (value) k.SetValue("LogrosBloodborne", "\"" + Assembly.GetExecutingAssembly().Location + "\" --bandeja");
                        else k.DeleteValue("LogrosBloodborne", false);
                    }
                }
                catch { }
            }
        }

        // ------------------------------------------------------------ menú

        public static ContextMenuStrip MenuOscuro()
        {
            var m = new ContextMenuStrip { Renderer = new Pintor(), ShowImageMargin = false, ShowCheckMargin = true };
            m.Font = new Font(Tema.Sans, 10f);
            m.BackColor = Tema.Panel; m.ForeColor = Tema.Texto;
            return m;
        }

        public ContextMenuStrip Menu()
        {
            if (menu != null) { Llenar(menu); return menu; }
            menu = MenuOscuro();
            menu.Opening += delegate { Llenar(menu); };
            Llenar(menu);
            return menu;
        }

        void Llenar(ContextMenuStrip m)
        {
            m.Items.Clear();
            Func<string, Action, ToolStripMenuItem> it = (txt, act) => { var i = new ToolStripMenuItem(txt); i.Click += delegate { act(); }; return i; };
            Func<string, string, Action, ToolStripMenuItem> ita = (txt, tecla, act) => { var i = it(txt, act); i.ShortcutKeyDisplayString = Ajustes.Atajos ? tecla : ""; return i; };

            if (Novedad != null)
            {
                var up = it("Actualizar a la versión " + Novedad.Version + "…", Actualizar);
                up.ForeColor = Tema.Oro;
                m.Items.Add(up);
                m.Items.Add(new ToolStripSeparator());
            }
            var ver = ita("Ver mis trofeos", "Ctrl+Alt+L", MostrarVentana); ver.Font = new Font(m.Font, FontStyle.Bold);
            m.Items.Add(ver);
            m.Items.Add(ita("Resumen rápido en pantalla", "Ctrl+Alt+O", Resumen));
            m.Items.Add(new ToolStripSeparator());
            ActualizarPendientes();
            var av = it("Notificaciones" + (!Ajustes.Avisos && Pendientes > 0 ? "  (" + Pendientes + " pendientes)" : ""), () => CambiarAvisos(!Ajustes.Avisos));
            av.Checked = Ajustes.Avisos; m.Items.Add(av);
            var so = ita("Sonido", "Ctrl+Alt+S", AlternarSonido);
            so.Checked = Ajustes.Sonido; m.Items.Add(so);
            var vol = new Deslizador { Min = 0, Max = 100, Paso = 5, Valor = Ajustes.Volumen, Detras = Tema.Panel, Formato = v => v + "%", Size = new Size(Tema.Px(230), Tema.Px(30)) };
            vol.Soltado += v => { Ajustes.Volumen = v; Ajustes.Guardar(); Sonido.Tocar('G', v, UserDir); };
            m.Items.Add(new ToolStripControlHost(vol) { AutoSize = false, Size = vol.Size, Margin = new Padding(Tema.Px(24), Tema.Px(2), Tema.Px(8), Tema.Px(4)) });
            m.Items.Add(ita("Mover la notificación…", "Ctrl+Alt+M", MoverAviso));
            m.Items.Add(it(Ajustes.Sincronizado ? "Sincronizar con bbport" : "Sincronizar con bbport (primera vez)", SincronizarAhora));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(it("Configuración…", AbrirConfig));
            if (!JuegoAbierto) m.Items.Add(it("Abrir el juego", AbrirJuego));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(it("Salir (cerrar del todo)", Salir));
        }
        // ------------------------------------------------------------ exportar / importar

        // Respaldo automático de tus trofeos (formato importable), uno por día, se guardan los últimos 15
        void RespaldoAuto()
        {
            try
            {
                if (Juego == null || Juego.Obtenidos == 0) return;
                string dir = Path.Combine(CarpetaDatos, "respaldos");
                Directory.CreateDirectory(dir);
                var lineas = new List<string> { "# Respaldo automático de Logros Bloodborne - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"), "# bbport: " + Bbport };
                foreach (var t in Juego.Trofeos.Where(x => x.Obtenido != null).OrderBy(x => x.Obtenido))
                    lineas.Add(t.Id + " " + (long)(t.Obtenido.Value.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);
                Disco.Escribir(Path.Combine(dir, "trofeos-" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt"), lineas);
                foreach (var viejo in new DirectoryInfo(dir).GetFiles("trofeos-*.txt").OrderByDescending(x => x.Name).Skip(15)) viejo.Delete();
                foreach (var b in new DirectoryInfo(dir).GetFiles("*.bak")) b.Delete();
            }
            catch { }
        }

        public void Exportar()
        {
            if (!Ajustes.Sincronizado) { MessageBox.Show("Primero sincronizá con bbport, así hay trofeos para exportar.", "Logros Bloodborne"); return; }
            using (var d = new SaveFileDialog
            {
                Title = "Exportar mis trofeos", Filter = "Respaldo de trofeos (*.txt)|*.txt",
                FileName = "Trofeos Bloodborne " + DateTime.Now.ToString("yyyy-MM-dd") + ".txt",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            })
            {
                if (d.ShowDialog() != DialogResult.OK) return;
                var sb = new StringBuilder();
                sb.AppendLine("# Trofeos de " + Juego.Titulo + " (bbport) - exportado el " + DateTime.Now.ToString("d/M/yyyy HH:mm"));
                sb.AppendLine("# " + Juego.Obtenidos + " de " + Juego.Trofeos.Count + " trofeos - " + Juego.Porcentaje + "%");
                sb.AppendLine("# Se puede importar desde Logros Bloodborne. Las líneas con # son solo para leer.");
                sb.AppendLine("#");
                foreach (var t in Juego.Trofeos.Where(x => x.Obtenido != null).OrderBy(x => x.Obtenido))
                    sb.AppendLine("# " + t.Obtenido.Value.ToString("yyyy-MM-dd HH:mm") + "  [" + Datos.NombreGrado(t.Grado) + "]  " + t.Nombre);
                sb.AppendLine("#");
                foreach (var t in Juego.Trofeos.Where(x => x.Obtenido != null).OrderBy(x => x.Obtenido))
                    sb.AppendLine(t.Id + " " + (long)(t.Obtenido.Value.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);
                try { File.WriteAllText(d.FileName, sb.ToString(), new UTF8Encoding(false)); Mensaje("Trofeos exportados", Juego.Obtenidos + " trofeos guardados en el respaldo"); }
                catch (Exception ex) { MessageBox.Show("No pude guardar: " + ex.Message, "Logros Bloodborne"); }
            }
        }

        // Suma trofeos de un respaldo o de otro trophies.log (otra instalación). Nunca quita ninguno.
        public void Importar()
        {
            if (EnJuego)
            {
                MessageBox.Show("Cerrá el juego antes de importar trofeos, así bbport no pisa el archivo.\n(El launcher puede quedar abierto.)", "Logros Bloodborne", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var d = new OpenFileDialog
            {
                Title = "Importar trofeos (respaldo .txt o trophies.log de otra instalación)",
                Filter = "Trofeos (*.txt;*.log)|*.txt;*.log|Todos los archivos|*.*"
            })
            {
                if (d.ShowDialog() != DialogResult.OK) return;
                var otros = Datos.LeerLog(d.FileName);
                if (otros == null || otros.Count == 0) { MessageBox.Show("Ese archivo no tiene trofeos para importar.", "Logros Bloodborne"); return; }
                var faltan = otros.Where(kv => !conocidos.ContainsKey(kv.Key) && Juego.Buscar(kv.Key) != null).OrderBy(kv => kv.Value).ToList();
                if (faltan.Count == 0) { Mensaje("Nada para importar", "Ya tenías todos esos trofeos"); return; }
                if (MessageBox.Show("Se van a sumar " + faltan.Count + " trofeos a tu bbport (no se quita ninguno).\n\n¿Seguimos?", "Logros Bloodborne", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                try
                {
                    string log = Path.Combine(UserDir, "trophies.log");
                    if (File.Exists(log)) File.Copy(log, log + ".antes-de-importar", true);
                    var sb = new StringBuilder();
                    if (File.Exists(log) && new FileInfo(log).Length > 0)
                    {
                        var fin = File.ReadAllBytes(log);
                        if (fin[fin.Length - 1] != (byte)'\n') sb.Append("\r\n");
                    }
                    foreach (var kv in faltan)
                        sb.Append(kv.Key + " " + (long)(kv.Value.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds + "\r\n");
                    File.AppendAllText(log, sb.ToString(), new UTF8Encoding(false));
                    if (Ajustes.Sincronizado) Sincronizar(false, true); else PrimeraSincronizacion(); // salen en orden
                }
                catch (Exception ex) { MessageBox.Show("No pude importar: " + ex.Message, "Logros Bloodborne"); }
            }
        }

        public void Probar(char grado)
        {
            if (Juego == null || Juego.Trofeos.Count == 0) return;
            var r = new Random();
            var c = Juego.Trofeos.Where(t => (grado == 'X' || t.Grado == grado) && (!t.Oculto || t.Obtenido != null || Ajustes.MostrarOcultos)).ToList();
            if (c.Count == 0) c = Juego.Trofeos.Where(t => grado == 'X' || t.Grado == grado).ToList();
            if (c.Count == 0) return;
            Anunciar(c[r.Next(c.Count)], false);
        }

        public void Salir()
        {
            try { if (pidJuego != 0) Tiempo.Registrar(inicioProceso, finVentana ?? DateTime.Now); } catch { }
            reloj.Stop();
            if (atajos != null) atajos.Liberar();
            bandeja.Visible = false;
            bandeja.Dispose();
            ExitThread();
            Environment.Exit(0);
        }
    }
}
