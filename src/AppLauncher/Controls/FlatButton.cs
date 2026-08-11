using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AppLauncher.Theme;

namespace AppLauncher.Controls
{
    public class FlatButton : Button
    {
        private bool _hover;
        private bool _pressed;

        public Color NormalColor { get; set; } = AppTheme.BgElevated;
        public Color HoverColor { get; set; } = Color.FromArgb(46, 66, 78);
        public Color PressedColor { get; set; } = AppTheme.TealDim;
        public Color BorderColor { get; set; } = AppTheme.Border;
        public bool IsPrimary { get; set; }

        public FlatButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            ForeColor = AppTheme.TextPrimary;
            Font = AppTheme.BodyFont;
            Height = 36;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            _pressed = true;
            Invalidate();
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            Color fill = NormalColor;
            if (IsPrimary)
            {
                fill = Enabled ? AppTheme.Teal : AppTheme.TealDim;
                if (Enabled && _hover) fill = AppTheme.TealBright;
                if (Enabled && _pressed) fill = AppTheme.TealDim;
            }
            else
            {
                if (Enabled && _hover) fill = HoverColor;
                if (Enabled && _pressed) fill = PressedColor;
                if (!Enabled) fill = AppTheme.BgPanel;
            }

            using var path = RoundRect(ClientRectangle, 8);
            using var brush = new SolidBrush(fill);
            e.Graphics.FillPath(brush, path);

            if (!IsPrimary)
            {
                using var pen = new Pen(Enabled ? BorderColor : AppTheme.BgPanel);
                e.Graphics.DrawPath(pen, path);
            }

            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                ClientRectangle,
                Enabled ? (IsPrimary ? Color.FromArgb(10, 30, 28) : ForeColor) : AppTheme.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private static GraphicsPath RoundRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            var r = new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
