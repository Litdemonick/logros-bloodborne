using System;
using System.Globalization;
using System.Linq;

namespace LogrosBB
{
    // bbport no guarda el tiempo jugado (play_time.txt quedó de shadPS4 y no se actualiza), así que
    // lo medimos nosotros: cada sesión se identifica por la hora de arranque del proceso del juego,
    // y se actualiza cada 30 s. Si este programa se cierra y vuelve a abrir, la sesión no se duplica.
    static class Fecha
    {
        static readonly CultureInfo es = CultureInfo.GetCultureInfo("es-AR");
        public static string Formato = "auto";

        public static string Hora(DateTime d)
        {
            bool h24 = Formato == "24" || (Formato == "auto" && CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains("H"));
            return h24 ? d.ToString("HH:mm") : d.ToString("h:mm", es) + (d.Hour < 12 ? " a. m." : " p. m.");
        }

        // "hoy 23:08", "ayer 21:40", "el martes", "hace 12 días", "12 de oct de 2025"
        public static string Amigable(DateTime d)
        {
            var hoy = DateTime.Today;
            int dias = (int)(hoy - d.Date).TotalDays;
            if (dias <= 0) return "hoy " + Hora(d);
            if (dias == 1) return "ayer " + Hora(d);
            if (dias < 7) return "el " + d.ToString("dddd", es) + " " + Hora(d);
            if (dias < 31) return "hace " + dias + " días";
            return d.ToString(d.Year == hoy.Year ? "d \"de\" MMM" : "d \"de\" MMM yyyy", es);
        }

        public static string Grupo(DateTime d)
        {
            int dias = (int)(DateTime.Today - d.Date).TotalDays;
            if (dias <= 0) return "Hoy";
            if (dias == 1) return "Ayer";
            if (dias < 7) return "Esta semana";
            if (dias < 31) return "Este mes";
            return d.ToString("MMMM yyyy", es).Substring(0, 1).ToUpper() + d.ToString("MMMM yyyy", es).Substring(1);
        }
    }
}
