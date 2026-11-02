using System;
using System.Drawing;
using System.Windows.Forms;

namespace LogrosBB
{
    class Pintor : ToolStripProfessionalRenderer
    {
        public Pintor() : base(new Colores()) { RoundedEdges = false; }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor = e.Item.Enabled ? Tema.Texto : Tema.Tenue; base.OnRenderItemText(e); }
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) { e.ArrowColor = Tema.Tenue; base.OnRenderArrow(e); }
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var g = e.Graphics; Tema.Calidad(g);
            var r = e.ImageRectangle;
            using (var pen = new Pen(Tema.SangreClara, 2f))
                g.DrawLines(pen, new[] { new PointF(r.X + r.Width * .2f, r.Y + r.Height * .55f), new PointF(r.X + r.Width * .42f, r.Y + r.Height * .78f), new PointF(r.X + r.Width * .82f, r.Y + r.Height * .25f) });
        }

        class Colores : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Tema.Panel; } }
            public override Color ImageMarginGradientBegin { get { return Tema.Panel; } }
            public override Color ImageMarginGradientMiddle { get { return Tema.Panel; } }
            public override Color ImageMarginGradientEnd { get { return Tema.Panel; } }
            public override Color MenuBorder { get { return Tema.Borde; } }
            public override Color MenuItemBorder { get { return Tema.Sangre; } }
            public override Color MenuItemSelected { get { return Tema.PanelAlto; } }
            public override Color MenuItemSelectedGradientBegin { get { return Tema.PanelAlto; } }
            public override Color MenuItemSelectedGradientEnd { get { return Tema.PanelAlto; } }
            public override Color SeparatorDark { get { return Tema.Borde; } }
            public override Color SeparatorLight { get { return Tema.Panel; } }
            public override Color CheckBackground { get { return Tema.Panel; } }
            public override Color CheckSelectedBackground { get { return Tema.PanelAlto; } }
            public override Color CheckPressedBackground { get { return Tema.PanelAlto; } }
        }
    }
}
