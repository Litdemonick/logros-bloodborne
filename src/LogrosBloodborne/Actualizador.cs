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
    // Busca versiones nuevas en GitHub Releases y actualiza con el instalador (Inno Setup) en modo
    // silencioso. Es lo único que se conecta a internet, y se puede desactivar en Configuración.
    static class Actualizador
    {
        const string Api = "https://api.github.com/repos/Litdemonick/logros-bloodborne/releases/latest";
        public const string NombreInstalador = "Instalar-Logros-Bloodborne.exe";

        public class Novedad
        {
            public Version Version;
            public string Url, UrlSha;
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

        // Devuelve la versión nueva si existe, o null (también si no hay internet)
        public static Novedad Buscar()
        {
            try
            {
                string json;
                using (var wc = Cliente()) json = wc.DownloadString(Api);
                var tag = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"v?(\\d+\\.\\d+(?:\\.\\d+)?)\"");
                if (!tag.Success) return null;
                var v = Normal(new Version(tag.Groups[1].Value));
                if (v <= Actual) return null;
                string patron = "\"browser_download_url\"\\s*:\\s*\"([^\"]+/" + Regex.Escape(NombreInstalador) + "{0})\"";
                var url = Regex.Match(json, string.Format(patron, ""));
                var sha = Regex.Match(json, string.Format(patron, "\\.sha256"));
                if (!url.Success || !sha.Success) return null; // sin verificación no se ofrece
                return new Novedad { Version = v, Url = url.Groups[1].Value, UrlSha = sha.Groups[1].Value };
            }
            catch { return null; }
        }

        // Descarga, verifica el SHA-256 publicado y lanza el instalador silencioso.
        // Devuelve null si todo salió bien, o el motivo por el que no se instaló.
        public static string Instalar(Novedad n)
        {
            try
            {
                string dir = Path.Combine(Path.GetTempPath(), "LogrosBloodborne-actualizacion");
                Directory.CreateDirectory(dir);
                string exe = Path.Combine(dir, NombreInstalador);
                string esperado;
                using (var wc = Cliente())
                {
                    wc.DownloadFile(n.Url, exe);
                    esperado = wc.DownloadString(n.UrlSha).Trim().Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();
                }
                string real;
                using (var s = File.OpenRead(exe))
                using (var h = SHA256.Create())
                    real = BitConverter.ToString(h.ComputeHash(s)).Replace("-", "").ToLowerInvariant();
                if (esperado.Length != 64 || esperado != real)
                {
                    File.Delete(exe);
                    return "La descarga no pasó la verificación de seguridad (SHA-256). No se instaló nada.";
                }
                // el instalador cierra esta copia, actualiza en la misma carpeta y la vuelve a abrir
                Process.Start(new ProcessStartInfo(exe, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /RELANZAR=1") { UseShellExecute = true });
                return null;
            }
            catch (Exception ex) { return "No se pudo actualizar: " + ex.Message; }
        }
    }
}
