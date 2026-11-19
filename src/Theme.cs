using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;

namespace MfeLauncher
{
    static class Theme
    {
        public static readonly Color Brand = Color.FromArgb(79, 0, 141);
        public static readonly Color BrandHover = Color.FromArgb(99, 22, 165);
        public static readonly Color BrandTint = Color.FromArgb(246, 242, 251);
        public static readonly Color Background = Color.FromArgb(243, 244, 248);
        public static readonly Color Card = Color.White;
        public static readonly Color Border = Color.FromArgb(226, 228, 235);
        public static readonly Color Text = Color.FromArgb(28, 31, 38);
        public static readonly Color Muted = Color.FromArgb(110, 116, 130);
        public static readonly Color Danger = Color.FromArgb(222, 54, 66);
        public static readonly Color Warning = Color.FromArgb(226, 140, 0);

        public static readonly Color ConsoleBack = Color.FromArgb(24, 24, 30);
        public static readonly Color ConsoleBar = Color.FromArgb(36, 36, 44);
        public static readonly Color ConsoleText = Color.FromArgb(214, 216, 224);

        public static readonly Dictionary<AppStatus, (Color Color, string Text)> Statuses = new Dictionary<AppStatus, (Color, string)>
        {
            [AppStatus.Stopped] = (Color.FromArgb(150, 155, 166), "Stopped"),
            [AppStatus.Starting] = (Color.FromArgb(232, 150, 20), "Starting"),
            [AppStatus.Running] = (Color.FromArgb(30, 164, 90), "Running"),
            [AppStatus.External] = (Color.FromArgb(46, 112, 240), "External"),
            [AppStatus.Error] = (Danger, "Build error"),
            [AppStatus.Crashed] = (Danger, "Crashed"),
            [AppStatus.Stopping] = (Color.FromArgb(232, 150, 20), "Stopping")
        };

        public static readonly float Scale = DetectScale();

        public static readonly Font Body = new Font("Segoe UI", 9.5f);
        public static readonly Font Small = new Font("Segoe UI", 8.5f);
        public static readonly Font Strong = new Font("Segoe UI Semibold", 10.5f);
        public static readonly Font Title = new Font("Segoe UI Semibold", 14f);
        public static readonly Font Mono = new Font("Consolas", 9.5f);

        /// <summary>Segoe MDL2 Assets ships with Windows 10+; without it buttons fall back to text only.</summary>
        public static readonly Font Glyphs = FontFamily.Families.Any(f => f.Name == "Segoe MDL2 Assets")
            ? new Font("Segoe MDL2 Assets", 9f)
            : null;

        public const string GlyphPlay = "";
        public const string GlyphRefresh = "";
        public const string GlyphStop = "";
        public const string GlyphLogs = "";
        public const string GlyphSync = "";
        public const string GlyphOpen = "";
        public const string GlyphCopy = "";
        public const string GlyphClear = "";
        public const string GlyphMore = "";
        public const string GlyphWarning = "";
        public const string GlyphRepair = "";
        public const string GlyphPackage = "";

        public static int S(int pixels) => (int)Math.Round(pixels * Scale);

        public static Icon AppIcon(int size)
        {
            using (var stream = typeof(Theme).Assembly.GetManifestResourceStream("app.ico"))
            {
                return stream == null ? SystemIcons.Application : new Icon(stream, size, size);
            }
        }

        /// <summary>A glyph rendered to a transparent bitmap, for menu items.</summary>
        public static Image GlyphImage(string glyph, Color color)
        {
            var size = S(16);
            var bitmap = new Bitmap(size, size);
            if (Glyphs == null) return bitmap;

            using (var g = Graphics.FromImage(bitmap))
            using (var font = new Font(Glyphs.FontFamily, size * 0.8f, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.DrawString(glyph, font, brush, new RectangleF(0, 0, size, size), format);
            }
            return bitmap;
        }

        public static GraphicsPath RoundedRect(RectangleF bounds, float radius)
        {
            var d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Color Blend(Color color, Color over, float amount) => Color.FromArgb(
            (int)(color.R + (over.R - color.R) * amount),
            (int)(color.G + (over.G - color.G) * amount),
            (int)(color.B + (over.B - color.B) * amount));

        static float DetectScale()
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) return g.DpiX / 96f;
        }
    }
}
