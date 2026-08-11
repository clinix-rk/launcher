using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AppLauncher.Theme
{
    public static class AppTheme
    {
        public static readonly Color BgDeep = Color.FromArgb(18, 28, 34);
        public static readonly Color BgPanel = Color.FromArgb(28, 40, 48);
        public static readonly Color BgElevated = Color.FromArgb(36, 52, 62);
        public static readonly Color BgHeaderTop = Color.FromArgb(22, 48, 54);
        public static readonly Color BgHeaderBottom = Color.FromArgb(18, 28, 34);

        public static readonly Color Teal = Color.FromArgb(32, 168, 156);
        public static readonly Color TealBright = Color.FromArgb(56, 196, 180);
        public static readonly Color TealDim = Color.FromArgb(24, 110, 104);

        public static readonly Color TextPrimary = Color.FromArgb(236, 242, 244);
        public static readonly Color TextSecondary = Color.FromArgb(156, 176, 186);
        public static readonly Color TextMuted = Color.FromArgb(110, 130, 140);

        public static readonly Color Success = Color.FromArgb(72, 186, 128);
        public static readonly Color Warning = Color.FromArgb(220, 168, 72);
        public static readonly Color Danger = Color.FromArgb(220, 96, 96);
        public static readonly Color Border = Color.FromArgb(52, 72, 84);

        public static readonly Color LogBg = Color.FromArgb(12, 18, 22);
        public static readonly Color LogInfo = Color.FromArgb(190, 210, 220);
        public static readonly Color LogSuccess = Color.FromArgb(96, 210, 150);
        public static readonly Color LogWarning = Color.FromArgb(230, 190, 100);
        public static readonly Color LogError = Color.FromArgb(240, 130, 130);
        public static readonly Color LogCommand = Color.FromArgb(100, 190, 200);

        public static Font TitleFont { get; } = new("Segoe UI Semibold", 22F, FontStyle.Bold);
        public static Font SubtitleFont { get; } = new("Segoe UI", 9.5F, FontStyle.Regular);
        public static Font SectionFont { get; } = new("Segoe UI Semibold", 10F, FontStyle.Bold);
        public static Font BodyFont { get; } = new("Segoe UI", 9.5F, FontStyle.Regular);
        public static Font SmallFont { get; } = new("Segoe UI", 8.5F, FontStyle.Regular);

        public static void ApplyFormChrome(Form form)
        {
            form.BackColor = BgDeep;
            form.ForeColor = TextPrimary;
            form.Font = BodyFont;
            form.FormBorderStyle = FormBorderStyle.FixedSingle;
            form.MaximizeBox = false;
            form.StartPosition = FormStartPosition.CenterScreen;
        }

        public static void PaintHeaderGradient(Graphics g, Rectangle bounds)
        {
            using var brush = new LinearGradientBrush(bounds, BgHeaderTop, BgHeaderBottom, LinearGradientMode.Vertical);
            g.FillRectangle(brush, bounds);

            using var gridPen = new Pen(Color.FromArgb(18, 255, 255, 255), 1);
            for (int x = 0; x < bounds.Width; x += 28)
            {
                g.DrawLine(gridPen, x, 0, x, bounds.Height);
            }

            for (int y = 0; y < bounds.Height; y += 28)
            {
                g.DrawLine(gridPen, 0, y, bounds.Width, y);
            }

            using var accent = new LinearGradientBrush(
                new Rectangle(0, bounds.Bottom - 3, bounds.Width, 3),
                TealDim,
                Teal,
                LinearGradientMode.Horizontal);
            g.FillRectangle(accent, 0, bounds.Bottom - 3, bounds.Width, 3);
        }
    }
}
