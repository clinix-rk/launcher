using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using AppLauncher.Services;
using AppLauncher.Theme;

namespace AppLauncher.Controls
{
    public class ServicePill : Control
    {
        private ServiceHealth _health = ServiceHealth.Unknown;

        public string ServiceName { get; set; } = "Service";

        public ServiceHealth Health
        {
            get => _health;
            set
            {
                if (_health == value) return;
                _health = value;
                Invalidate();
            }
        }

        public ServicePill()
        {
            DoubleBuffered = true;
            Size = new Size(140, 56);
            Font = AppTheme.SmallFont;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using var path = RoundRect(ClientRectangle, 8);
            using var bg = new SolidBrush(AppTheme.BgPanel);
            e.Graphics.FillPath(bg, path);
            using var border = new Pen(AppTheme.Border);
            e.Graphics.DrawPath(border, path);

            Color statusColor = _health switch
            {
                ServiceHealth.Healthy => AppTheme.Success,
                ServiceHealth.Starting => AppTheme.Warning,
                ServiceHealth.Down => AppTheme.Danger,
                _ => AppTheme.TextMuted
            };

            string statusText = _health switch
            {
                ServiceHealth.Healthy => "Healthy",
                ServiceHealth.Starting => "Starting",
                ServiceHealth.Down => "Down",
                _ => "Unknown"
            };

            using var dot = new SolidBrush(statusColor);
            e.Graphics.FillEllipse(dot, 14, 14, 10, 10);

            TextRenderer.DrawText(
                e.Graphics,
                ServiceName,
                AppTheme.SectionFont,
                new Rectangle(30, 8, Width - 40, 22),
                AppTheme.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            TextRenderer.DrawText(
                e.Graphics,
                statusText,
                AppTheme.SmallFont,
                new Rectangle(30, 28, Width - 40, 20),
                statusColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
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
