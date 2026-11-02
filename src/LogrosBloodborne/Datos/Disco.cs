using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LogrosBB
{
    // Escritura segura: primero a un temporal, después reemplazo atómico guardando copia .bak.
    // Si un archivo queda dañado (corte de luz, etc.), al leer se usa la copia.
    static class Disco
    {
        public static void Escribir(string ruta, IEnumerable<string> lineas)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ruta));
                string tmp = ruta + ".tmp";
                File.WriteAllLines(tmp, lineas, new UTF8Encoding(false));
                if (File.Exists(ruta)) File.Replace(tmp, ruta, ruta + ".bak", true);
                else File.Move(tmp, ruta);
            }
            catch { }
        }

        public static string[] Leer(string ruta)
        {
            foreach (var r in new[] { ruta, ruta + ".bak" })
                try { if (File.Exists(r)) { var l = File.ReadAllLines(r); if (l.Length > 0) return l; } } catch { }
            return null;
        }
    }
}
