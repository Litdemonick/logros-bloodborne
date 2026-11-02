using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace LogrosBB
{
    // Busca versiones nuevas en GitHub Releases y actualiza abriendo el instalador (Inno Setup). Es lo único que se conecta a internet, y se puede desactivar en Configuración.
    static class Actualizador
    {
        const string Api = "https://api.github.com/repos/Litdemonick/logros-bloodborne/releases/latest";
        public const string NombreInstalador = "Instalar-Logros-Bloodborne.exe";

        public class Novedad
        {
            public Version Version;
            public string Url, Sha256; // huella que publica GitHub para el instalador
        }

        public static Version Actual { get { return Normal(Assembly.GetExecutingAssembly().GetName().Version); } }

        static Version Normal(Version v) { return new Version(v.Major, v.Minor, Math.Max(0, v.Build)); }

        static WebClient Cliente()
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
            var wc = new WebClient { Encoding = Encoding.UTF8 };
            wc.Headers.Add("User-Agent", "LogrosBloodborne/" + Actual);
            return wc;
        }

        // Devuelve la versión nueva si existe, o null. Si no se pudo revisar, error dice por qué.
        public static Novedad Buscar(out string error)
        {
            error = null;
            try
            {
                string json;
                using (var wc = Cliente()) json = wc.DownloadString(Api);
                var tag = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"v?(\\d+\\.\\d+(?:\\.\\d+)?)\"");
                if (!tag.Success) { error = "GitHub respondió algo inesperado. Probá de nuevo en un rato."; return null; }
                var v = Normal(new Version(tag.Groups[1].Value));
                if (v <= Actual) return null;
                var url = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+/" + Regex.Escape(NombreInstalador) + ")\"");
                // GitHub publica la huella SHA-256 de cada archivo de la release ("digest")
                var sha = Regex.Match(json, "\"name\"\\s*:\\s*\"" + Regex.Escape(NombreInstalador) + "\".*?\"digest\"\\s*:\\s*\"sha256:([0-9a-f]{64})\"", RegexOptions.Singleline);
                if (!url.Success || !sha.Success) { error = "La versión " + v + " todavía se está publicando. Probá de nuevo en unos minutos."; return null; } // sin verificación no se ofrece
                return new Novedad { Version = v, Url = url.Groups[1].Value, Sha256 = sha.Groups[1].Value };
            }
            catch (WebException) { error = "Sin conexión con GitHub. Revisá internet y probá de nuevo."; return null; }
            catch (Exception ex) { error = "No pude revisar: " + ex.Message; return null; }
        }

        // Descarga, verifica la huella SHA-256 que publica GitHub y lanza el instalador silencioso.
        // Devuelve null si todo salió bien, o el motivo por el que no se instaló.
        public static string Instalar(Novedad n)
        {
            try
            {
                string dir = Path.Combine(Path.GetTempPath(), "LogrosBloodborne-actualizacion");
                Directory.CreateDirectory(dir);
                string exe = Path.Combine(dir, NombreInstalador);
                using (var wc = Cliente()) wc.DownloadFile(n.Url, exe);
                string esperado = n.Sha256;
                string real;
                using (var s = File.OpenRead(exe))
                using (var h = SHA256.Create())
                    real = BitConverter.ToString(h.ComputeHash(s)).Replace("-", "").ToLowerInvariant();
                if (esperado.Length != 64 || esperado != real)
                {
                    File.Delete(exe);
                    return "La descarga no pasó la verificación de seguridad (SHA-256). No se instaló nada.";
                }
                // se abre el instalador normal (con sus pantallas) y el programa se cierra enseguida para
                // dejarle libre la carpeta; al terminar, el instalador ofrece volver a abrirlo
                Process.Start(new ProcessStartInfo(exe, "/SP- /NORESTART") { UseShellExecute = true });
                return null;
            }
            catch (Exception ex) { return "No se pudo actualizar: " + ex.Message; }
        }
    }
}
