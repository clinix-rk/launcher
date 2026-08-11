using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AppLauncher.Theme
{
    public static class AppTheme
    {
        public static readonly Color BgDeep = Color.FromArgb(245, 247, 250);
        public static readonly Color BgPanel = Color.FromArgb(255, 255, 255);
        public static readonly Color BgElevated = Color.FromArgb(236, 240, 245);
        public static readonly Color BgHeaderTop = Color.FromArgb(255, 255, 255);
        public static readonly Color BgHeaderBottom = Color.FromArgb(240, 244, 248);

        public static readonly Color Navy = Color.FromArgb(30, 58, 95);
        public static readonly Color NavyBright = Color.FromArgb(45, 80, 130);
        public static readonly Color NavyDim = Color.FromArgb(22, 44, 72);

        // Kept for any remaining references; map to navy accent.
        public static readonly Color Teal = Navy;
        public static readonly Color TealBright = NavyBright;
        public static readonly Color TealDim = NavyDim;

        public static readonly Color TextPrimary = Color.FromArgb(28, 35, 45);
        public static readonly Color TextSecondary = Color.FromArgb(90, 105, 120);
        public static readonly Color TextMuted = Color.FromArgb(130, 145, 160);

        public static readonly Color Success = Color.FromArgb(34, 140, 90);
        public static readonly Color Warning = Color.FromArgb(180, 120, 30);
        public static readonly Color Danger = Color.FromArgb(180, 55, 55);
        public static readonly Color Border = Color.FromArgb(210, 218, 228);

        public static readonly Color LogBg = Color.FromArgb(248, 250, 252);
        public static readonly Color LogInfo = Color.FromArgb(50, 65, 80);
        public static readonly Color LogSuccess = Color.FromArgb(28, 120, 75);
        public static readonly Color LogWarning = Color.FromArgb(160, 100, 20);
        public static readonly Color LogError = Color.FromArgb(170, 45, 45);
        public static readonly Color LogCommand = Color.FromArgb(40, 85, 140);

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

            using var accent = new SolidBrush(Navy);
            g.FillRectangle(accent, 0, bounds.Bottom - 3, bounds.Width, 3);
        }
    }
}
