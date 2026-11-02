using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace LogrosBB
{
    static class Tiempo
    {
        static readonly Dictionary<long, long> sesiones = new Dictionary<long, long>();
        static bool cargado;

        static string Archivo
        {
            get { return Path.Combine(Edicion.CarpetaDatos, "tiempo.txt"); }
        }

        static void Cargar()
        {
            if (cargado) return;
            cargado = true;
            try
            {
                if (!File.Exists(Archivo)) return;
                foreach (var l in File.ReadAllLines(Archivo))
                {
                    var p = l.Split(' ');
                    long k, s;
                    if (p.Length == 2 && long.TryParse(p[0], out k) && long.TryParse(p[1], out s)) sesiones[k] = s;
                }
            }
            catch { }
        }

        // Cada tramo se guarda con su clave (el momento en que el programa empezó a verlo) y los segundos
        // que realmente se vieron. Así no se cuenta tiempo de juego anterior a abrir el programa, ni la
        // PC suspendida con el juego abierto.
        public static long Clave(DateTime inicio) { return inicio.ToUniversalTime().Ticks; }

        public static void Registrar(long k, double segundos)
        {
            Cargar();
            long s = (long)Math.Max(0, segundos);
            long prev;
            if (sesiones.TryGetValue(k, out prev) && prev >= s) return;
            sesiones[k] = s;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Archivo));
                string tmp = Archivo + ".tmp";
                File.WriteAllLines(tmp, sesiones.Select(kv => kv.Key + " " + kv.Value));
                if (File.Exists(Archivo)) File.Replace(tmp, Archivo, null); else File.Move(tmp, Archivo);
            }
            catch { }
        }

        // Total guardado + el tramo en curso (que puede no estar guardado todavía)
        public static TimeSpan Total(long kActual, double segActual)
        {
            Cargar();
            long s = 0;
            foreach (var kv in sesiones) if (kv.Key != kActual) s += kv.Value;
            if (kActual >= 0) s += (long)Math.Max(0, segActual);
            return TimeSpan.FromSeconds(s);
        }

        public static DateTime? Desde()
        {
            Cargar();
            if (sesiones.Count == 0) return null;
            return new DateTime(sesiones.Keys.Min(), DateTimeKind.Utc).ToLocalTime();
        }

        public static string Largo(TimeSpan t)
        {
            if (t.TotalMinutes < 1) return "menos de 1 min";
            if (t.TotalHours < 1) return (int)t.TotalMinutes + " min";
            return (int)t.TotalHours + " h " + t.Minutes + " min";
        }

        public static string Reloj(TimeSpan t)
        {
            return (int)t.TotalHours + ":" + t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00");
        }
    }
}
