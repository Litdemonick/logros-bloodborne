using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace LogrosBB
{
    class Ajustes
    {
        public string Bbport = "", JuegoDir = "", UserDir = "";
        public bool Sonido = true;
        public int Volumen = 60;           // ni muy fuerte ni muy bajo
        public bool Avisos = true;         // notificaciones activadas
        public bool UnoPorUno = true;      // al sincronizar: los perdidos de a uno (si no, un resumen)
        public double Tamano = 1.0;        // tamaño de la notificación
        public double Velocidad = 1.0;     // velocidad de aparecer / desaparecer
        public string Idioma = "auto";
        public string Posicion = "arriba-centro";
        public bool MostrarOcultos = false;
        public int Segundos = 7;
        public double PosX = .5, PosY = 0; // posición libre, relativa a la ventana del juego (0..1)
        public bool Atajos = true;
        public string Hora = "auto";       // auto (como Windows) / 12 / 24
        public bool Sincronizado = false;
        public bool Actualizaciones = true; // buscar versiones nuevas en GitHub  // la primera sincronización la hace el jugador con el botón

        static string Carpeta
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LogrosBloodborne"); }
        }
        static string Archivo { get { return Path.Combine(Carpeta, "ajustes.ini"); } }

        public static Ajustes Cargar()
        {
            var a = new Ajustes();
            try
            {
                var todas = Disco.Leer(Archivo);
                if (todas == null) return a;
                foreach (var linea in todas)
                {
                    int i = linea.IndexOf('=');
                    if (i < 1) continue;
                    string k = linea.Substring(0, i).Trim(), v = linea.Substring(i + 1).Trim();
                    int n;
                    switch (k)
                    {
                        case "bbport": a.Bbport = v; break;
                        case "juego": a.JuegoDir = v; break;
                        case "user": a.UserDir = v; break;
                        case "sonido": a.Sonido = v != "0"; break;
                        case "volumen": if (int.TryParse(v, out n)) a.Volumen = Math.Max(0, Math.Min(100, n)); break;
                        case "idioma": a.Idioma = v; break;
                        case "posicion": a.Posicion = v; break;
                        case "ocultos": a.MostrarOcultos = v == "1"; break;
                        case "segundos": if (int.TryParse(v, out n)) a.Segundos = n; break;
                        case "posx": double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out a.PosX); break;
                        case "posy": double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out a.PosY); break;
                        case "atajos": a.Atajos = v != "0"; break;
                        case "hora": a.Hora = v; break;
                        case "velocidad": double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out a.Velocidad); a.Velocidad = Math.Max(.5, Math.Min(2, a.Velocidad)); break;
                        case "sync": a.Sincronizado = v == "1"; break;
                        case "actualizaciones": a.Actualizaciones = v != "0"; break;
                        case "avisos": a.Avisos = v != "0"; break;
                        case "unoporuno": a.UnoPorUno = v != "0"; break;
                        case "tamano": double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out a.Tamano); a.Tamano = Math.Max(.7, Math.Min(1.5, a.Tamano)); break;
                    }
                }
            }
            catch { }
            // valores fuera de rango (archivo editado a mano o dañado): se corrigen a algo válido
            a.Volumen = Math.Max(0, Math.Min(100, a.Volumen));
            a.Segundos = Math.Max(3, Math.Min(15, a.Segundos));
            a.Tamano = Math.Max(.7, Math.Min(1.5, double.IsNaN(a.Tamano) ? 1 : a.Tamano));
            a.Velocidad = Math.Max(.5, Math.Min(2, double.IsNaN(a.Velocidad) ? 1 : a.Velocidad));
            a.PosX = Math.Max(0, Math.Min(1, double.IsNaN(a.PosX) ? .5 : a.PosX));
            a.PosY = Math.Max(0, Math.Min(1, double.IsNaN(a.PosY) ? 0 : a.PosY));
            if (Array.IndexOf(new[] { "arriba-centro", "arriba-izquierda", "arriba-derecha", "abajo-izquierda", "abajo-derecha", "libre" }, a.Posicion) < 0) a.Posicion = "arriba-centro";
            if (Array.IndexOf(new[] { "auto", "12", "24" }, a.Hora) < 0) a.Hora = "auto";
            int idi;
            if (a.Idioma != "auto" && !(int.TryParse(a.Idioma, out idi) && idi >= 0 && idi <= 30)) a.Idioma = "auto";
            return a;
        }

        public void Guardar()
        {
            {
                Disco.Escribir(Archivo, new[] {
                    "bbport=" + Bbport, "juego=" + JuegoDir, "user=" + UserDir,
                    "sonido=" + (Sonido ? "1" : "0"), "volumen=" + Volumen,
                    "idioma=" + Idioma, "posicion=" + Posicion,
                    "ocultos=" + (MostrarOcultos ? "1" : "0"), "segundos=" + Segundos,
                    "posx=" + PosX.ToString("0.####", CultureInfo.InvariantCulture), "posy=" + PosY.ToString("0.####", CultureInfo.InvariantCulture),
                    "atajos=" + (Atajos ? "1" : "0"), "avisos=" + (Avisos ? "1" : "0"), "unoporuno=" + (UnoPorUno ? "1" : "0"),
                    "tamano=" + Tamano.ToString("0.##", CultureInfo.InvariantCulture), "hora=" + Hora, "velocidad=" + Velocidad.ToString("0.##", CultureInfo.InvariantCulture), "sync=" + (Sincronizado ? "1" : "0"), "actualizaciones=" + (Actualizaciones ? "1" : "0")
                });
            }
        }
    }
}
