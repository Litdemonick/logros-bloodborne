using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;

namespace LogrosBB
{
    static class Datos
    {
        public static int PuntosDe(char g)
        {
            switch (g) { case 'P': return 180; case 'G': return 90; case 'S': return 30; default: return 15; }
        }

        public static string NombreGrado(char g)
        {
            switch (g) { case 'P': return "Platino"; case 'G': return "Oro"; case 'S': return "Plata"; default: return "Bronce"; }
        }

        public static string NombreGradoDe(char g)
        {
            switch (g) { case 'P': return "de platino"; case 'G': return "de oro"; case 'S': return "de plata"; default: return "de bronce"; }
        }

        // Carpeta de bbport: la que tiene Bloodborne.exe (o Play Bloodborne.exe) junto a sus archivos
        public static bool EsBbport(string d)
        {
            try
            {
                if (string.IsNullOrEmpty(d) || !Directory.Exists(d)) return false;
                bool exe = File.Exists(Path.Combine(d, "Bloodborne.exe")) || File.Exists(Path.Combine(d, "Play Bloodborne.exe"));
                return exe && (Directory.Exists(Path.Combine(d, "user")) || Directory.Exists(Path.Combine(d, "out")) ||
                               Directory.Exists(Path.Combine(d, "_internal")) || File.Exists(Path.Combine(d, "bbport.ini")) ||
                               Directory.Exists(Path.Combine(d, "launcher")));
            }
            catch { return false; }
        }

        // Acepta la carpeta "casi correcta": la de arriba (donde se descomprimió todo), una de adentro
        // (user, out, launcher…) o hasta el propio Bloodborne.exe. Devuelve la carpeta de bbport o null.
        public static string NormalizarBbport(string d)
        {
            try
            {
                if (string.IsNullOrEmpty(d)) return null;
                d = d.Trim().Trim('"');
                if (File.Exists(d)) d = Path.GetDirectoryName(d);
                if (!Directory.Exists(d)) return null;
                d = Path.GetFullPath(d);
                if (d.Length > 3) d = d.TrimEnd('\\');
                // hacia arriba (eligieron una subcarpeta)
                string p = d;
                for (int i = 0; i < 4 && p != null; i++, p = Path.GetDirectoryName(p))
                    if (EsBbport(p)) return p;
                // hacia abajo (eligieron la carpeta de arriba)
                return BuscarAdentro(d, 3);
            }
            catch { return null; }
        }

        static readonly string[] noEntrar = { "windows", "$recycle.bin", "system volume information", "programdata", "appdata", "cusa03173", "user", "out", "_internal", "node_modules", ".git" };

        static string BuscarAdentro(string d, int prof)
        {
            if (prof <= 0) return null;
            string[] subs;
            try { subs = Directory.GetDirectories(d); } catch { return null; }
            // primero las carpetas con nombres probables
            var orden = subs.OrderBy(x => Regex.IsMatch(Path.GetFileName(x), "bbport|bloodborne", RegexOptions.IgnoreCase) ? 0 : 1).ToList();
            foreach (var s in orden) if (EsBbport(s)) return s;
            foreach (var s in orden)
            {
                if (noEntrar.Contains(Path.GetFileName(s).ToLowerInvariant())) continue;
                try { if ((File.GetAttributes(s) & FileAttributes.ReparsePoint) != 0) continue; } catch { continue; }
                var r = BuscarAdentro(s, prof - 1);
                if (r != null) return r;
            }
            return null;
        }

        // Destino de un acceso directo .lnk (por COM, sin referencias extra)
        static string DestinoLnk(string lnk)
        {
            try
            {
                var t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) return null;
                object sh = Activator.CreateInstance(t);
                object l = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
                return l.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, l, null) as string;
            }
            catch { return null; }
        }

        // Accesos directos de bbport en el escritorio y el menú inicio
        static List<string> DesdeAccesos()
        {
            var r = new List<string>();
            var lugares = new[] {
                new { C = Environment.SpecialFolder.DesktopDirectory, Sub = false }, new { C = Environment.SpecialFolder.CommonDesktopDirectory, Sub = false },
                new { C = Environment.SpecialFolder.StartMenu, Sub = true }, new { C = Environment.SpecialFolder.CommonStartMenu, Sub = true } };
            foreach (var x in lugares)
            {
                string[] lnks;
                try { lnks = Directory.GetFiles(Environment.GetFolderPath(x.C), "*.lnk", x.Sub ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly); }
                catch { continue; }
                foreach (var l in lnks)
                {
                    if (!Regex.IsMatch(Path.GetFileNameWithoutExtension(l), "bloodborne|bbport", RegexOptions.IgnoreCase)) continue;
                    string t = DestinoLnk(l);
                    if (!string.IsNullOrEmpty(t)) r.Add(t);
                }
            }
            return r;
        }

        [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint acc, bool inh, int pid);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder s, ref int size);

        static string RutaDe(int pid)
        {
            IntPtr h = OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION
            if (h == IntPtr.Zero) return null;
            try { var sb = new StringBuilder(1024); int n = sb.Capacity; return QueryFullProcessImageName(h, 0, sb, ref n) ? sb.ToString() : null; }
            finally { CloseHandle(h); }
        }

        // Programas abiertos ahora mismo que vivan dentro de una carpeta de bbport (launcher o juego,
        // se llamen como se llamen): si bbport está abierto, se sabe dónde está sin preguntar nada.
        static List<string> DesdeProcesos()
        {
            var r = new List<string>();
            Process[] ps;
            try { ps = Process.GetProcesses(); } catch { return r; }
            foreach (var p in ps)
                try
                {
                    string f = RutaDe(p.Id);
                    if (f != null && f.IndexOf("\\windows\\",StringComparison.OrdinalIgnoreCase) < 0 &&
                        Regex.IsMatch(f, "bloodborne|bbport", RegexOptions.IgnoreCase)) r.Add(f);
                }
                catch { }
                finally { p.Dispose(); }
            return r;
        }

        public static string BbportAbierto()
        {
            foreach (var f in DesdeProcesos())
            {
                var n = NormalizarBbport(f);
                if (n != null) return n;
            }
            return null;
        }

        public static string JuegoDelLauncher() { return NormalizarJuego(LeerLauncher("game_dir")); }

        // Lugares típicos en cualquier PC; se miran pocos niveles, así tarda segundos
        static IEnumerable<KeyValuePair<string, int>> LugaresTipicos()
        {
            string perfil = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (var x in new[] { "Downloads", "Desktop", "Documents", "Games", "OneDrive\\Desktop", "OneDrive\\Documents", "OneDrive\\Escritorio", "OneDrive\\Documentos" })
                yield return new KeyValuePair<string, int>(Path.Combine(perfil, x), 3);
            DriveInfo[] discos;
            try { discos = DriveInfo.GetDrives(); } catch { yield break; }
            foreach (var dr in discos)
            {
                bool ok;
                try { ok = dr.IsReady && (dr.DriveType == DriveType.Fixed || dr.DriveType == DriveType.Removable); } catch { ok = false; }
                if (!ok) continue;
                string raiz = dr.RootDirectory.FullName;
                foreach (var s in new[] { "Games", "Juegos", "Bloodborne", "bbport", "Emuladores", "Emulators", "Program Files", "Program Files (x86)" })
                    yield return new KeyValuePair<string, int>(Path.Combine(raiz, s), 3);
                yield return new KeyValuePair<string, int>(raiz, 2);
            }
        }

        // Búsqueda instantánea al abrir: lo guardado, el launcher, bbport abierto y los accesos directos.
        // Si no aparece, el Buscador sigue con los lugares típicos en segundo plano (BuscarTipicos).
        public static string BuscarBbport(Ajustes a)
        {
            var directos = new List<string> { a.Bbport, AppDomain.CurrentDomain.BaseDirectory };
            string ud = LeerLauncher("user_dir"), gd = LeerLauncher("game_dir");
            if (ud != "") directos.Add(ud);
            if (gd != "") try { directos.Add(Path.GetDirectoryName(gd.TrimEnd('\\'))); } catch { }
            foreach (var d in directos)
            {
                // lo guardado y lo que dice el launcher: se acepta la carpeta exacta, la de arriba o una de adentro
                var n = NormalizarBbport(d);
                if (n != null) return n;
            }
            // bbport abierto: lo más seguro, va primero después de lo guardado
            var abierto = BbportAbierto();
            if (abierto != null) return abierto;
            foreach (var f in DesdeAccesos())
            {
                var n = NormalizarBbport(f);
                if (n != null) return n;
            }
            return null;
        }

        public static string BuscarTipicos()
        {
            foreach (var kv in LugaresTipicos())
            {
                try
                {
                    if (!Directory.Exists(kv.Key)) continue;
                    string n = EsBbport(kv.Key) ? kv.Key : BuscarAdentro(kv.Key, kv.Value);
                    if (n != null) return Path.GetFullPath(n);
                }
                catch { }
            }
            return null;
        }

        static string LeerLauncher(string clave)
        {
            try
            {
                string f = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "bbport-launcher", "settings.json");
                if (!File.Exists(f)) return "";
                var m = Regex.Match(File.ReadAllText(f), "\"" + clave + "\"\\s*:\\s*(\"((?:[^\"\\\\]|\\\\.)*)\"|(-?\\d+))");
                if (!m.Success) return "";
                if (m.Groups[3].Success) return m.Groups[3].Value;
                return Regex.Unescape(m.Groups[2].Value);
            }
            catch { return ""; }
        }

        static string Trp(string juego)
        {
            try
            {
                if (string.IsNullOrEmpty(juego)) return null;
                string f = Path.Combine(juego, "sce_sys", "trophy", "trophy00.trp");
                return File.Exists(f) ? f : null;
            }
            catch { return null; }
        }

        // Carpeta del juego (CUSA03173 o como se llame): la que tiene sce_sys\trophy\trophy00.trp.
        // Acepta también la carpeta de arriba o una de adentro (sce_sys, trophy…).
        public static string NormalizarJuego(string d)
        {
            try
            {
                if (string.IsNullOrEmpty(d)) return null;
                d = d.Trim().Trim('"');
                if (File.Exists(d)) d = Path.GetDirectoryName(d);
                if (!Directory.Exists(d)) return null;
                d = Path.GetFullPath(d);
                if (d.Length > 3) d = d.TrimEnd('\\');
                string p = d;
                for (int i = 0; i < 3 && p != null; i++, p = Path.GetDirectoryName(p))
                    if (Trp(p) != null) return p;
                return JuegoAdentro(d, 3);
            }
            catch { return null; }
        }

        static string JuegoAdentro(string d, int prof)
        {
            if (prof <= 0) return null;
            string[] subs;
            try { subs = Directory.GetDirectories(d); } catch { return null; }
            var orden = subs.OrderBy(x => Regex.IsMatch(Path.GetFileName(x), "CUSA|bloodborne|game|juego", RegexOptions.IgnoreCase) ? 0 : 1).ToList();
            foreach (var s in orden) if (Trp(s) != null) return s;
            foreach (var s in orden)
            {
                string n = Path.GetFileName(s).ToLowerInvariant();
                if (n == "windows" || n == "$recycle.bin" || n == "_internal" || n == "shader" || n == "cache" || n == "savedata") continue;
                try { if ((File.GetAttributes(s) & FileAttributes.ReparsePoint) != 0) continue; } catch { continue; }
                var r = JuegoAdentro(s, prof - 1);
                if (r != null) return r;
            }
            return null;
        }

        public static string BuscarJuego(Ajustes a, string bbport)
        {
            var c = new List<string> { a.JuegoDir, LeerLauncher("game_dir") };
            foreach (var d in c) if (Trp(d) != null) return Path.GetFullPath(d);
            string padre = null;
            try { padre = Path.GetDirectoryName(bbport); } catch { }
            if (padre != null)
            {
                // al lado de bbport (lo normal), o un nivel más arriba
                var r = JuegoAdentro(padre, 2);
                if (r == null) try { string abuelo = Path.GetDirectoryName(padre); if (abuelo != null && abuelo.Length > 3) r = JuegoAdentro(abuelo, 2); } catch { }
                if (r != null) return r;
            }
            return JuegoAdentro(bbport, 2);
        }

        // Carpeta de datos de bbport (partidas, trofeos, config.toml). Normalmente bbport\user, pero el
        // launcher puede apuntarla a otro lado, y bbport (shadPS4) sin carpeta user usa %APPDATA%.
        public static string UserElegida;

        public static List<string> CandidatasUser(string bbport)
        {
            var c = new List<string>();
            if (!string.IsNullOrEmpty(UserElegida)) c.Add(UserElegida); // la que eligió el jugador a mano
            string ud = LeerLauncher("user_dir");
            if (ud != "") c.Add(ud);
            c.Add(Path.Combine(bbport, "user"));
            string ad = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            c.Add(Path.Combine(ad, "bbport", "user"));
            c.Add(Path.Combine(ad, "bbport"));
            c.Add(Path.Combine(ad, "shadPS4"));
            var r = new List<string>();
            foreach (var d in c)
                try { if (Directory.Exists(d) && !r.Any(x => x.Equals(Path.GetFullPath(d), StringComparison.OrdinalIgnoreCase))) r.Add(Path.GetFullPath(d)); } catch { }
            return r;
        }

        public static string CarpetaUser(string bbport)
        {
            var c = CandidatasUser(bbport);
            // la que tiene los trofeos de bbport (y la clave, si está); si no, la que tiene la clave
            foreach (var d in c) if (File.Exists(Path.Combine(d, "trophies.log")) && LeerClave(d) != null) return d;
            foreach (var d in c) if (File.Exists(Path.Combine(d, "trophies.log"))) return d;
            foreach (var d in c) if (LeerClave(d) != null) return d;
            return c.Count > 0 ? c[0] : Path.Combine(bbport, "user");
        }

        // Clave de trofeos: user\config.toml (trophyKey) o user\keys.json (ReleaseTrophyKey). bbport NO la
        // necesita para anotar trofeos (los escribe en trophies.log igual), así que en muchas PCs no existe:
        // en ese caso el programa funciona igual, con íconos y grados, y la busca o se la dan después.
        public static string LeerClave(string userDir)
        {
            foreach (var f in new[] { "config.toml", "keys.json" })
                try { string p = Path.Combine(userDir, f); if (File.Exists(p)) { var k = LeerClaveArchivo(p); if (k != null) return k; } } catch { }
            return null;
        }

        static string ClaveDeTexto(string t)
        {
            var m = Regex.Match(t, "trophyKey\\s*=\\s*[\"']([0-9A-Fa-f]{32})[\"']");
            if (!m.Success) m = Regex.Match(t, "\"(?:Release)?TrophyKey\"\\s*:\\s*\"([0-9A-Fa-f]{32})\"", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : null;
        }

        public static string LeerClaveArchivo(string f)
        {
            try { return new FileInfo(f).Length > 2 * 1024 * 1024 ? null : ClaveDeTexto(LeerCompartido(f)); }
            catch { return null; }
        }

        // Una vez encontrada, la clave se guarda en los datos del programa: no se vuelve a pedir
        static string ArchivoClave
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LogrosBloodborne", "clave-trofeos.txt"); }
        }

        public static string ClaveGuardada()
        {
            try
            {
                if (!File.Exists(ArchivoClave)) return null;
                var m = Regex.Match(File.ReadAllText(ArchivoClave), "[0-9A-Fa-f]{32}");
                return m.Success ? m.Value : null;
            }
            catch { return null; }
        }

        public static void GuardarClave(string k)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ArchivoClave));
            File.WriteAllText(ArchivoClave, k);
        }

        public static string Clave(string userDir) { return LeerClave(userDir) ?? ClaveGuardada(); }

        // Para llevarla a otra PC: el mismo formato que usa bbport (user\keys.json)
        public static void ExportarClave(string clave, string archivo)
        {
            File.WriteAllText(archivo, "{\n    \"TrophyKeySet\": {\n        \"ReleaseTrophyKey\": \"" + clave + "\"\n    }\n}\n");
        }

        // ¿Esta clave abre los trofeos de este juego?
        public static bool ProbarClave(string clave, string juegoDir)
        {
            try
            {
                byte[] b = File.ReadAllBytes(Trp(juegoDir));
                var e = Entradas(b);
                byte[] k2 = ClaveJuego(clave, juegoDir);
                return e.ContainsKey("TROP.ESFM") && Descifrar(b, e["TROP.ESFM"], k2) != null;
            }
            catch { return false; }
        }

        // Busca config.toml / keys.json con una clave que sirva, en discos, pendrives y carpetas típicas
        public static string BuscarClaveEnDiscos(string juegoDir, Func<bool> cancelar)
        {
            var raices = new List<KeyValuePair<string, int>>();
            string ad = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            raices.Add(new KeyValuePair<string, int>(Path.Combine(ad, "shadPS4"), 2));
            raices.Add(new KeyValuePair<string, int>(Path.Combine(ad, "bbport"), 2));
            try
            {
                foreach (var dr in DriveInfo.GetDrives())
                    try { if (dr.IsReady && dr.DriveType == DriveType.Removable) raices.Add(new KeyValuePair<string, int>(dr.RootDirectory.FullName, 4)); } catch { }
            }
            catch { }
            foreach (var kv in LugaresTipicos()) raices.Add(new KeyValuePair<string, int>(kv.Key, kv.Value + 2));
            var probadas = new HashSet<string>();
            foreach (var kv in raices)
            {
                if (cancelar()) return null;
                var k = ClaveEn(kv.Key, kv.Value, juegoDir, probadas, cancelar);
                if (k != null) return k;
            }
            return null;
        }

        static string ClaveEn(string dir, int prof, string juegoDir, HashSet<string> probadas, Func<bool> cancelar)
        {
            if (prof < 0 || cancelar()) return null;
            try { if (!Directory.Exists(dir)) return null; } catch { return null; }
            foreach (var f in new[] { "keys.json", "config.toml" })
            {
                string p = Path.Combine(dir, f);
                string k = File.Exists(p) ? LeerClaveArchivo(p) : null;
                if (k != null && probadas.Add(k.ToLowerInvariant()) && ProbarClave(k, juegoDir)) return k;
            }
            string[] subs;
            try { subs = Directory.GetDirectories(dir); } catch { return null; }
            foreach (var sd in subs.OrderBy(x => Regex.IsMatch(Path.GetFileName(x), "user|bbport|bloodborne|shadps4|games|juegos", RegexOptions.IgnoreCase) ? 0 : 1))
            {
                string n = Path.GetFileName(sd).ToLowerInvariant();
                if (noEntrar.Contains(n) && n != "user") continue;
                if (n == "savedata" || n == "shader" || n == "cache" || n == "patches" || n == "mods" || n == "cusa03173") continue;
                try { if ((File.GetAttributes(sd) & FileAttributes.ReparsePoint) != 0) continue; } catch { continue; }
                var r = ClaveEn(sd, prof - 1, juegoDir, probadas, cancelar);
                if (r != null) return r;
            }
            return null;
        }

        static string LeerCompartido(string f)
        {
            using (var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var sr = new StreamReader(fs)) return sr.ReadToEnd();
        }

        public static int IdiomaJuego()
        {
            int n;
            return int.TryParse(LeerLauncher("language"), out n) ? n : -1;
        }

        static Dictionary<string, KeyValuePair<long, long>> Entradas(byte[] b)
        {
            var entradas = new Dictionary<string, KeyValuePair<long, long>>(StringComparer.OrdinalIgnoreCase);
            int n = (int)BE(b, 16, 4), es = (int)BE(b, 20, 4);
            for (int i = 0; i < n; i++)
            {
                long o = 0x60 + (long)i * es;
                string nombre = Encoding.ASCII.GetString(b, (int)o, 32).TrimEnd('\0');
                entradas[nombre] = new KeyValuePair<long, long>((long)BE(b, o + 32, 8), (long)BE(b, o + 40, 8));
            }
            return entradas;
        }

        static byte[] ClaveJuego(string clave, string juegoDir)
        {
            byte[] np = new byte[16];
            string npb = Path.Combine(juegoDir, "sce_sys", "npbind.dat");
            if (File.Exists(npb)) Array.Copy(File.ReadAllBytes(npb), 0x84, np, 0, 12);
            else Encoding.ASCII.GetBytes("NPWR05818_00").CopyTo(np, 0);
            byte[] k = new byte[16];
            for (int i = 0; i < 16; i++) k[i] = Convert.ToByte(clave.Substring(i * 2, 2), 16);
            using (var aes = Aes.Create())
            {
                aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.None; aes.Key = k; aes.IV = new byte[16];
                return aes.CreateEncryptor().TransformFinalBlock(np, 0, 16);
            }
        }

        // Grado de cada trofeo de Bloodborne (id:grado; h = oculto, x = no cuenta para el platino, g1 = DLC).
        // Junto con Textos.Bloodborne (nombres y descripciones) alcanza para funcionar sin la clave.
        const string TablaBloodborne = "0:Px 1:Gh 2:Gh 3:Gh 4:Gh 5:G 6:G 7:S 8:S 9:S 10:Sh 11:Sh 12:Sh 13:Sh 14:Bh 15:Bh 16:Bh 17:Bh 18:Bh 19:Bh " +
            "20:Bh 21:Bh 22:Bh 23:Bh 24:Bh 25:Bh 26:Bh 27:Bh 28:Bh 29:B 30:B 31:B 32:Bh 33:Bh 34:Gxg1 35:Shxg1 36:Bhxg1 37:Bhxg1 38:Bhxg1 39:Bhxg1";

        // idiomas en orden de preferencia: el elegido en el programa, el del launcher de bbport, español latino, España
        static List<int> IdiomasPreferidos(string idioma)
        {
            var pref = new List<int>();
            int fijo;
            if (idioma != "auto" && int.TryParse(idioma, out fijo)) pref.Add(fijo);
            int lj = IdiomaJuego();
            if (lj >= 0) pref.Add(lj); // mismo idioma que el launcher de bbport
            pref.Add(20); pref.Add(3);
            return pref;
        }

        static Juego SinClaveJuego(byte[] b, Dictionary<string, KeyValuePair<long, long>> entradas, string juegoDir, string idioma)
        {
            var j = new Juego { SinNombres = true };
            var tabla = new Dictionary<int, string>();
            foreach (var x in TablaBloodborne.Split(' ')) { var p = x.Split(':'); tabla[int.Parse(p[0])] = p[1]; }
            var ids = new List<int>();
            foreach (var e in entradas.Keys)
            {
                var m = Regex.Match(e, "^TROP(\\d{3})\\.PNG$", RegexOptions.IgnoreCase);
                if (m.Success) ids.Add(int.Parse(m.Groups[1].Value));
            }
            ids.Sort();
            bool conTabla = ids.Count == tabla.Count && ids.All(tabla.ContainsKey);
            // con la lista conocida de Bloodborne, los nombres salen de Textos (no hace falta la clave)
            string[] tx = null;
            if (conTabla)
                foreach (int p in IdiomasPreferidos(idioma))
                    if (Textos.Bloodborne.TryGetValue(p, out tx)) break;
            if (tx != null)
            {
                j.SinNombres = false;
                j.Titulo = tx[0];
            }
            foreach (int id in ids)
            {
                string f = conTabla ? tabla[id] : (id == 0 ? "Px" : "B");
                var t = new Trofeo
                {
                    Id = id, Grado = f[0], Oculto = f.Contains("h"), CuentaPlatino = !f.Contains("x"),
                    Grupo = f.Contains("g1") ? 1 : -1,
                    Nombre = tx != null ? tx[2 + id * 2] : f[0] == 'P' ? "Platino" : "Trofeo " + id,
                    Detalle = tx != null ? tx[3 + id * 2] : "Agregá la clave de trofeos para ver el nombre y la descripción."
                };
                t.Icono = Imagen(b, entradas["TROP" + id.ToString("000") + ".PNG"], 128);
                t.IconoGris = Gris(t.Icono);
                j.Trofeos.Add(t);
            }
            if (j.Trofeos.Any(t => t.Grupo == 1)) j.Grupos[1] = tx != null && tx[1].Length > 0 ? tx[1] : "Contenido descargable";
            if (entradas.ContainsKey("GR001.PNG")) j.IconoGrupo = Imagen(b, entradas["GR001.PNG"], 128);
            j.IconoJuego = ImagenArchivo(Path.Combine(juegoDir, "sce_sys", "icon0.png"), 256);
            if (j.IconoJuego == null && entradas.ContainsKey("ICON0.PNG")) j.IconoJuego = Imagen(b, entradas["ICON0.PNG"], 256);
            j.Fondo = ImagenArchivo(Path.Combine(juegoDir, "sce_sys", "pic1.png"), 1280);
            return j;
        }

        static ulong BE(byte[] b, long o, int n) { ulong v = 0; for (int i = 0; i < n; i++) v = (v << 8) | b[o + i]; return v; }

        public static Juego Cargar(string juegoDir, string userDir, string idioma, out string error)
        {
            error = null;
            string trp = Trp(juegoDir);
            byte[] b = File.ReadAllBytes(trp);
            if (BE(b, 0, 4) != 0xDCA24D00) { error = "El archivo de trofeos del juego no es válido."; return null; }

            var entradas = Entradas(b);

            // Clave de trofeos (user\config.toml, user\keys.json o la guardada). Sin ella, o si no sirve,
            // el programa funciona igual: íconos y grados reales, nombres y descripciones de Textos.
            string clave = Clave(userDir);
            if (clave == null) return SinClaveJuego(b, entradas, juegoDir, idioma);
            byte[] k2 = ClaveJuego(clave, juegoDir);

            // Idioma: TROP_xx.ESFM con el código de idioma de PS4; 20 = español latino, 03 = España
            string xml = null;
            foreach (int p in IdiomasPreferidos(idioma))
            {
                string nom = "TROP_" + p.ToString("00") + ".ESFM";
                if (entradas.ContainsKey(nom)) { xml = Descifrar(b, entradas[nom], k2); if (xml != null) break; }
            }
            if (xml == null && entradas.ContainsKey("TROP.ESFM")) xml = Descifrar(b, entradas["TROP.ESFM"], k2);
            if (xml == null) return SinClaveJuego(b, entradas, juegoDir, idioma); // la clave no coincide con este juego

            var j = new Juego();
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var tn = doc.SelectSingleNode("//title-name");
            if (tn != null) j.Titulo = tn.InnerText.Trim();
            foreach (XmlElement g in doc.SelectNodes("//group"))
            {
                int gid;
                if (int.TryParse(g.GetAttribute("id"), out gid))
                {
                    var nn = g.SelectSingleNode("name");
                    j.Grupos[gid] = nn != null ? nn.InnerText.Trim() : "Contenido extra";
                }
            }
            foreach (XmlElement e in doc.SelectNodes("//trophy"))
            {
                var t = new Trofeo();
                int.TryParse(e.GetAttribute("id"), out t.Id);
                t.Oculto = e.GetAttribute("hidden") == "yes";
                string tt = e.GetAttribute("ttype");
                t.Grado = tt.Length > 0 ? char.ToUpperInvariant(tt[0]) : 'B';
                int gid, pid;
                if (int.TryParse(e.GetAttribute("gid"), out gid)) t.Grupo = gid;
                t.CuentaPlatino = int.TryParse(e.GetAttribute("pid"), out pid) && pid == 0;
                var nn = e.SelectSingleNode("name"); var dd = e.SelectSingleNode("detail");
                if (nn != null) t.Nombre = nn.InnerText.Trim();
                if (dd != null) t.Detalle = Regex.Replace(dd.InnerText.Trim(), "\\s*\\n\\s*", " ");
                string png = "TROP" + t.Id.ToString("000") + ".PNG";
                if (entradas.ContainsKey(png))
                {
                    t.Icono = Imagen(b, entradas[png], 128);
                    t.IconoGris = Gris(t.Icono);
                }
                j.Trofeos.Add(t);
            }
            if (entradas.ContainsKey("GR001.PNG")) j.IconoGrupo = Imagen(b, entradas["GR001.PNG"], 128);
            // arte del juego: ícono cuadrado y fondo, del sce_sys del propio jugador
            j.IconoJuego = ImagenArchivo(Path.Combine(juegoDir, "sce_sys", "icon0.png"), 256);
            if (j.IconoJuego == null && entradas.ContainsKey("ICON0.PNG")) j.IconoJuego = Imagen(b, entradas["ICON0.PNG"], 256);
            j.Fondo = ImagenArchivo(Path.Combine(juegoDir, "sce_sys", "pic1.png"), 1280);
            return j;
        }

        static Bitmap ImagenArchivo(string f, int lado)
        {
            try
            {
                if (!File.Exists(f)) return null;
                byte[] d = File.ReadAllBytes(f);
                return Imagen(d, new KeyValuePair<long, long>(0, d.Length), lado);
            }
            catch { return null; }
        }

        static string Descifrar(byte[] b, KeyValuePair<long, long> e, byte[] k2)
        {
            try
            {
                long off = e.Key, len = e.Value;
                if (len <= 32) return null;
                byte[] iv = new byte[16];
                Array.Copy(b, off, iv, 0, 16);
                int dl = (int)((len - 16) / 16 * 16);
                byte[] d = new byte[dl];
                Array.Copy(b, off + 16, d, 0, dl);
                byte[] x;
                using (var aes = Aes.Create())
                {
                    aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.None; aes.Key = k2; aes.IV = iv;
                    x = aes.CreateDecryptor().TransformFinalBlock(d, 0, dl);
                }
                string s = Encoding.UTF8.GetString(x);
                int fin = s.LastIndexOf("</trophyconf>", StringComparison.Ordinal);
                if (fin < 0) return null;
                int ini = s.IndexOf("<trophyconf", StringComparison.Ordinal);
                return s.Substring(ini, fin - ini + 13);
            }
            catch { return null; }
        }

        static Bitmap Imagen(byte[] b, KeyValuePair<long, long> e, int lado)
        {
            try
            {
                using (var ms = new MemoryStream(b, (int)e.Key, (int)e.Value))
                using (var img = Image.FromStream(ms))
                {
                    int w = lado, h = (int)Math.Round((double)lado * img.Height / img.Width);
                    var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(bmp))
                    using (var ia = new ImageAttributes())
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.CompositingMode = CompositingMode.SourceCopy;
                        ia.SetWrapMode(WrapMode.TileFlipXY); // sin bordes con ruido al escalar
                        g.DrawImage(img, new Rectangle(0, 0, w, h), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, ia);
                    }
                    return bmp;
                }
            }
            catch { return null; }
        }

        static Bitmap Gris(Bitmap src)
        {
            if (src == null) return null;
            var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            var cm = new ColorMatrix(new[] {
                new float[] { .21f, .21f, .21f, 0, 0 },
                new float[] { .55f, .55f, .55f, 0, 0 },
                new float[] { .07f, .07f, .07f, 0, 0 },
                new float[] { 0, 0, 0, .55f, 0 },
                new float[] { -.06f, -.06f, -.06f, 0, 1 } });
            using (var ia = new ImageAttributes())
            using (var g = Graphics.FromImage(bmp))
            {
                ia.SetColorMatrix(cm);
                g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, ia);
            }
            return bmp;
        }

        public static Dictionary<int, DateTime> LeerLog(string f)
        {
            var r = new Dictionary<int, DateTime>();
            try
            {
                if (!File.Exists(f)) return r; // todavía sin trofeos
                string texto;
                using (var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var sr = new StreamReader(fs)) texto = sr.ReadToEnd();
                int finCompleto = texto.LastIndexOf('\n');
                texto = finCompleto >= 0 ? texto.Substring(0, finCompleto) : ""; // ignorar una línea a medio escribir
                foreach (var l in texto.Split('\n'))
                {
                    var p = l.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    int id; long ts;
                    if (p.Length < 1 || !int.TryParse(p[0], out id)) continue;
                    DateTime cuando = DateTime.Now;
                    if (p.Length > 1 && long.TryParse(p[1], out ts) && ts > 0)
                        cuando = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(ts).ToLocalTime();
                    if (!r.ContainsKey(id)) r[id] = cuando;
                }
            }
            catch { return null; } // archivo ocupado: se reintenta
            return r;
        }

    }
}
