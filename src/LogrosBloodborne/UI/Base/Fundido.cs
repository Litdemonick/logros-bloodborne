using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace LogrosBB
{
    // Transición suave: una "foto" de cómo se veía la ventana queda encima y se desvanece mientras
    // abajo ya está la pantalla nueva. Es una ventana en capas: la mezcla la hace Windows (DWM), fluida
    // y sin cargar al programa ni al juego.
    class Fundido : Form
    {
        readonly Timer reloj = new Timer { Interval = 10 };
        readonly Stopwatch cron = new Stopwatch();
        const double Duracion = 190;

        Fundido(Bitmap foto, Rectangle donde, Form dueño)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Bounds = donde;
            BackgroundImage = foto;
            BackgroundImageLayout = ImageLayout.None;
            Owner = dueño;
            DoubleBuffered = true;
            reloj.Tick += delegate
            {
                double t = Math.Min(1, cron.ElapsedMilliseconds / Duracion);
                Opacity = 1 - (1 - Math.Pow(1 - t, 3));
                if (t >= 1) { reloj.Stop(); Close(); }
            };
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.DrawImageUnscaled(BackgroundImage, 0, 0); }
        protected override void OnFormClosed(FormClosedEventArgs e) { reloj.Dispose(); if (BackgroundImage != null) BackgroundImage.Dispose(); base.OnFormClosed(e); }

        // Hace el cambio tapado por la foto y después la desvanece
        public static void Hacer(Form v, Action cambio)
        {
            Fundido f = null;
            try
            {
                if (v.Visible && v.WindowState != FormWindowState.Minimized && v.ClientSize.Width > 0 && v.ClientSize.Height > 0 && v.ContainsFocus)
                {
                    var donde = v.RectangleToScreen(v.ClientRectangle);
                    var foto = new Bitmap(donde.Width, donde.Height);
                    using (var g = Graphics.FromImage(foto)) g.CopyFromScreen(donde.Location, Point.Empty, donde.Size);
                    f = new Fundido(foto, donde, v);
                    f.Opacity = 1;
                    f.Show();
                    f.Update();
                }
            }
            catch { if (f != null) { try { f.Close(); } catch { } f = null; } }
            try { cambio(); }
            finally
            {
                // pase lo que pase, la foto se desvanece: nunca queda pegada encima
                if (f != null)
                {
                    try { v.Update(); } catch { } // la pantalla nueva ya dibujada debajo
                    f.cron.Start();
                    f.reloj.Start();
                }
            }
        }
    }
}
