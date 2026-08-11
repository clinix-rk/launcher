using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AppLauncher.Theme;

namespace AppLauncher.Controls
{
    public class StatusBadge : Control
    {
        private string _statusText = "Checking";
        private Color _accent = AppTheme.Warning;

        public StatusBadge()
        {
            DoubleBuffered = true;
            Size = new Size(140, 28);
            Font = AppTheme.SmallFont;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        public void SetStatus(string text, Color accent)
        {
            _statusText = text;
            _accent = accent;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = RoundRect(ClientRectangle, 14);
            using var fill = new SolidBrush(Color.FromArgb(40, _accent));
            e.Graphics.FillPath(fill, path);
            using var pen = new Pen(Color.FromArgb(120, _accent));
            e.Graphics.DrawPath(pen, path);

            TextRenderer.DrawText(
                e.Graphics,
                _statusText,
                Font,
                ClientRectangle,
                _accent,
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
