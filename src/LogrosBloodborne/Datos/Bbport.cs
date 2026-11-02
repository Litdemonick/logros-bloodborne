using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
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

        public static bool EsBbport(string d)
        {
            if (string.IsNullOrEmpty(d) || !Directory.Exists(d)) return false;
            return File.Exists(Path.Combine(d, "Bloodborne.exe")) &&
                   (Directory.Exists(Path.Combine(d, "user")) || Directory.Exists(Path.Combine(d, "out")));
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

        public static string BuscarBbport(Ajustes a)
        {
            var c = new List<string>();
            string yo = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            c.Add(a.Bbport);
            c.Add(yo);
            c.Add(Path.Combine(yo, "bbport"));
            try { c.Add(Path.GetDirectoryName(yo)); c.Add(Path.Combine(Path.GetDirectoryName(yo), "bbport")); } catch { }
            string ud = LeerLauncher("user_dir");
            if (ud != "") try { c.Add(Path.GetDirectoryName(ud.TrimEnd('\\'))); } catch { }
            foreach (var d in c) if (EsBbport(d)) return Path.GetFullPath(d);
            return null;
        }

        static string Trp(string juego)
        {
            if (string.IsNullOrEmpty(juego)) return null;
            string f = Path.Combine(juego, "sce_sys", "trophy", "trophy00.trp");
            return File.Exists(f) ? f : null;
        }

        public static string BuscarJuego(Ajustes a, string bbport)
        {
            var c = new List<string> { a.JuegoDir, LeerLauncher("game_dir") };
            try
            {
                string padre = Path.GetDirectoryName(bbport);
                c.Add(Path.Combine(padre, "CUSA03173"));
                c.AddRange(Directory.GetDirectories(padre));
                c.AddRange(Directory.GetDirectories(bbport));
            }
            catch { }
            foreach (var d in c) if (Trp(d) != null) return Path.GetFullPath(d);
            return null;
        }

        public static string CarpetaUser(string bbport)
        {
            string ud = LeerLauncher("user_dir");
            if (ud != "" && Directory.Exists(ud) && Path.GetFullPath(ud).StartsWith(bbport, StringComparison.OrdinalIgnoreCase))
                return ud;
            return Path.Combine(bbport, "user");
        }

        public static int IdiomaJuego()
        {
            int n;
            return int.TryParse(LeerLauncher("language"), out n) ? n : -1;
        }

        static ulong BE(byte[] b, long o, int n) { ulong v = 0; for (int i = 0; i < n; i++) v = (v << 8) | b[o + i]; return v; }

        public static Juego Cargar(string juegoDir, string userDir, string idioma, out string error)
        {
            error = null;
            string trp = Trp(juegoDir);
            byte[] b = File.ReadAllBytes(trp);
            if (BE(b, 0, 4) != 0xDCA24D00) { error = "El archivo de trofeos del juego no es válido."; return null; }

            // Clave de trofeos: la trae bbport en user\config.toml
            string clave = null;
            string toml = Path.Combine(userDir, "config.toml");
            if (File.Exists(toml))
            {
                var m = Regex.Match(File.ReadAllText(toml), "trophyKey\\s*=\\s*\"([0-9A-Fa-f]{32})\"");
                if (m.Success) clave = m.Groups[1].Value;
            }
            if (clave == null) { error = "No encontré la clave de trofeos en bbport\\user\\config.toml.\nAbrí el juego una vez con bbport y volvé a intentar."; return null; }

            byte[] np = new byte[16];
            string npb = Path.Combine(juegoDir, "sce_sys", "npbind.dat");
            if (File.Exists(npb)) Array.Copy(File.ReadAllBytes(npb), 0x84, np, 0, 12);
            else Encoding.ASCII.GetBytes("NPWR05818_00").CopyTo(np, 0);

            byte[] k = new byte[16];
            for (int i = 0; i < 16; i++) k[i] = Convert.ToByte(clave.Substring(i * 2, 2), 16);
            byte[] k2;
            using (var aes = Aes.Create())
            {
                aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.None; aes.Key = k; aes.IV = new byte[16];
                k2 = aes.CreateEncryptor().TransformFinalBlock(np, 0, 16);
            }

            var entradas = new Dictionary<string, KeyValuePair<long, long>>(StringComparer.OrdinalIgnoreCase);
            int n = (int)BE(b, 16, 4), es = (int)BE(b, 20, 4);
            for (int i = 0; i < n; i++)
            {
                long o = 0x60 + (long)i * es;
                string nombre = Encoding.ASCII.GetString(b, (int)o, 32).TrimEnd('\0');
                entradas[nombre] = new KeyValuePair<long, long>((long)BE(b, o + 32, 8), (long)BE(b, o + 40, 8));
            }

            // Idioma: TROP_xx.ESFM con el código de idioma de PS4; 20 = español latino, 03 = España
            var pref = new List<int>();
            int fijo;
            if (idioma != "auto" && int.TryParse(idioma, out fijo)) pref.Add(fijo);
            int lj = IdiomaJuego();
            if (lj >= 0) pref.Add(lj); // mismo idioma que el launcher de bbport
            pref.Add(20); pref.Add(3);
            string xml = null;
            foreach (int p in pref)
            {
                string nom = "TROP_" + p.ToString("00") + ".ESFM";
                if (entradas.ContainsKey(nom)) { xml = Descifrar(b, entradas[nom], k2); if (xml != null) break; }
            }
            if (xml == null && entradas.ContainsKey("TROP.ESFM")) xml = Descifrar(b, entradas["TROP.ESFM"], k2);
            if (xml == null) { error = "No pude leer los nombres de los trofeos (la clave no coincide)."; return null; }

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
