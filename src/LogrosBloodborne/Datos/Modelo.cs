using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace LogrosBB
{
    class Trofeo
    {
        public int Id;
        public string Nombre = "", Detalle = "";
        public char Grado = 'B';
        public bool Oculto;
        public int Grupo = -1;      // -1 = juego base
        public bool CuentaPlatino;  // pid == 0
        public Bitmap Icono, IconoGris;
        public DateTime? Obtenido;

        public int Puntos { get { return Datos.PuntosDe(Grado); } }
    }

    class Juego
    {
        public string Titulo = "Bloodborne";
        public List<Trofeo> Trofeos = new List<Trofeo>();
        public Dictionary<int, string> Grupos = new Dictionary<int, string>();
        public Bitmap IconoJuego, IconoGrupo, Fondo;

        public Trofeo Buscar(int id) { foreach (var t in Trofeos) if (t.Id == id) return t; return null; }
        public IEnumerable<Trofeo> DelPlatino { get { return Trofeos.Where(t => t.CuentaPlatino); } }
        public int FaltanPlatino { get { return DelPlatino.Count(t => t.Obtenido == null); } }
        public Trofeo Platino { get { return Trofeos.FirstOrDefault(t => t.Grado == 'P'); } }
        public int Obtenidos { get { return Trofeos.Count(t => t.Obtenido != null); } }
        public int PuntosObtenidos { get { return Trofeos.Where(t => t.Obtenido != null).Sum(t => t.Puntos); } }
        public int PuntosTotales { get { return Trofeos.Sum(t => t.Puntos); } }
        public int Porcentaje
        {
            get { return Trofeos.Count == 0 ? 0 : (int)Math.Floor(100.0 * Obtenidos / Trofeos.Count); } // igual que bbport: por cantidad
        }
    }
}
