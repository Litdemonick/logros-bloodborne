using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;

namespace LogrosBB
{
    static class Sonido
    {
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        static extern int mciSendString(string cmd, StringBuilder ret, int len, IntPtr cb);

        static readonly Dictionary<string, byte[]> cache = new Dictionary<string, byte[]>();
        static SoundPlayer jugador;
        static MemoryStream flujo;

        public static void Tocar(char grado, int volumen, string userDir)
        {
            try
            {
                // Sonido propio opcional: user\custom_trophy\trophy.wav o trophy.mp3
                string carpeta = Path.Combine(userDir ?? "", "custom_trophy");
                bool hito = "Hbsgd".IndexOf(grado) >= 0;
                string baseNom = grado == 'P' ? "platinum" : (hito ? "hito" : "trophy");
                string wav = Path.Combine(carpeta, baseNom + ".wav");
                string mp3 = Path.Combine(carpeta, baseNom + ".mp3");
                if (grado == 'P' && !File.Exists(wav) && !File.Exists(mp3))
                {
                    wav = Path.Combine(carpeta, "trophy.wav"); mp3 = Path.Combine(carpeta, "trophy.mp3");
                    if (File.Exists(wav) || File.Exists(mp3)) grado = 'X'; else { wav = mp3 = ""; }
                }
                if (File.Exists(wav)) { Detener(); jugador = new SoundPlayer(wav); jugador.Play(); return; }
                if (File.Exists(mp3))
                {
                    mciSendString("close bbtrofeo", null, 0, IntPtr.Zero);
                    mciSendString("open \"" + mp3 + "\" type mpegvideo alias bbtrofeo", null, 0, IntPtr.Zero);
                    mciSendString("setaudio bbtrofeo volume to " + volumen * 10, null, 0, IntPtr.Zero);
                    mciSendString("play bbtrofeo", null, 0, IntPtr.Zero);
                    return;
                }
                string clave = grado + ":" + volumen;
                byte[] datos;
                if (!cache.TryGetValue(clave, out datos)) cache[clave] = datos = Sintetizar(grado, volumen / 100.0);
                Detener();
                flujo = new MemoryStream(datos);
                jugador = new SoundPlayer(flujo);
                jugador.Play();
            }
            catch { }
        }

        static void Detener()
        {
            try { if (jugador != null) jugador.Stop(); } catch { }
            jugador = null;
        }

        static void Arpegio(double[] arp, double paso, out double[] notas, out double[] tiempos, out int acorde)
        {
            var n = new List<double>(arp); var t = new List<double>();
            for (int i = 0; i < arp.Length; i++) t.Add(i * paso);
            double fin = arp.Length * paso + .12;
            for (int i = Math.Max(0, arp.Length - 3); i < arp.Length; i++) { n.Add(arp[i] / 2); t.Add(fin); n.Add(arp[i]); t.Add(fin); }
            notas = n.ToArray(); tiempos = t.ToArray(); acorde = arp.Length;
        }

        // Campana suave con parciales de campana, ataque rápido y una reverb corta.
        static byte[] Sintetizar(char grado, double vol)
        {
            const int sr = 44100;
            double[] notas, tiempos; double dur, brillo;
            int acorde = int.MaxValue; // desde qué nota empieza el acorde final (suena más largo y suave)
            switch (grado)
            {
                // hitos y colecciones completas: fanfarrias propias, cada una en otra tonalidad
                case 'H': Arpegio(new[] { 783.99, 987.77, 1174.66, 1567.98 }, .07, out notas, out tiempos, out acorde); dur = 3.0; brillo = .8; break;
                case 'b': Arpegio(new[] { 523.25, 659.25, 783.99, 1046.5 }, .1, out notas, out tiempos, out acorde); dur = 3.2; brillo = .7; break;
                case 's': Arpegio(new[] { 587.33, 739.99, 880.0, 1174.66, 1479.98 }, .09, out notas, out tiempos, out acorde); dur = 3.4; brillo = .8; break;
                case 'g': Arpegio(new[] { 659.25, 830.61, 987.77, 1318.51, 1661.22, 1975.53 }, .08, out notas, out tiempos, out acorde); dur = 3.8; brillo = .9; break;
                case 'd': Arpegio(new[] { 440.0, 554.37, 659.25, 880.0, 1318.51 }, .1, out notas, out tiempos, out acorde); dur = 3.4; brillo = .8; break;
                case 'P': // arpegio ascendente + acorde final
                    notas = new[] { 523.25, 659.25, 783.99, 1046.5, 1318.5, 1567.98, 1046.5, 1318.5, 1567.98, 2093.0 };
                    tiempos = new[] { 0, .09, .18, .27, .36, .45, .70, .70, .70, .70 };
                    dur = 4.2; brillo = 1.0; acorde = 6; break;
                case 'G':
                    notas = new[] { 783.99, 1174.66, 1567.98, 2349.3 };
                    tiempos = new[] { 0, .10, .20, .20 };
                    dur = 2.8; brillo = .8; break;
                case 'S':
                    notas = new[] { 880.0, 1318.5, 1760.0 };
                    tiempos = new[] { 0, .10, .20 };
                    dur = 2.4; brillo = .6; break;
                default:
                    notas = new[] { 987.77, 1479.98 };
                    tiempos = new[] { 0, .11 };
                    dur = 2.0; brillo = .5; break;
            }
            int n = (int)(dur * sr);
            var buf = new double[n];
            for (int k = 0; k < notas.Length; k++)
            {
                double f = notas[k];
                int t0 = (int)(tiempos[k] * sr);
                double amp = k >= acorde ? .55 : 1.0;
                double caida = k >= acorde ? 1.1 : 2.6;
                for (int i = t0; i < n; i++)
                {
                    double t = (i - t0) / (double)sr;
                    double env = (1 - Math.Exp(-t * 320)) * Math.Exp(-t * caida);
                    if (env < 1e-5 && t > .5) break;
                    double w = 2 * Math.PI * f * t;
                    double s = Math.Sin(w)
                             + .42 * brillo * Math.Sin(2.0 * w + .3) * Math.Exp(-t * 3)
                             + .22 * brillo * Math.Sin(3.01 * w) * Math.Exp(-t * 6)
                             + .10 * brillo * Math.Sin(4.2 * w) * Math.Exp(-t * 10)
                             + .30 * Math.Sin(.5 * w) * Math.Exp(-t * 1.5); // cuerpo grave
                    buf[i] += amp * env * s;
                }
            }
            // reverb: unas pocas reflexiones
            int[] dl = { (int)(.031 * sr), (int)(.047 * sr), (int)(.071 * sr), (int)(.113 * sr) };
            double[] gn = { .32, .26, .2, .14 };
            var outb = (double[])buf.Clone();
            for (int r = 0; r < dl.Length; r++)
                for (int i = dl[r]; i < n; i++) outb[i] += buf[i - dl[r]] * gn[r] + outb[i - dl[r]] * gn[r] * .35;
            double pico = 1e-9;
            foreach (var v in outb) pico = Math.Max(pico, Math.Abs(v));
            // fade de salida
            int fade = sr / 5;
            for (int i = 0; i < fade; i++) outb[n - 1 - i] *= i / (double)fade;
            double escala = .72 * vol / pico;

            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);
            bw.Write(Encoding.ASCII.GetBytes("RIFF")); bw.Write(36 + n * 2);
            bw.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); bw.Write(16); bw.Write((short)1); bw.Write((short)1);
            bw.Write(sr); bw.Write(sr * 2); bw.Write((short)2); bw.Write((short)16);
            bw.Write(Encoding.ASCII.GetBytes("data")); bw.Write(n * 2);
            for (int i = 0; i < n; i++) bw.Write((short)Math.Max(-32767, Math.Min(32767, outb[i] * escala * 32767)));
            bw.Flush();
            return ms.ToArray();
        }
    }
}
