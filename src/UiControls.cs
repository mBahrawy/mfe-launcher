using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MfeLauncher
{
    enum ButtonKind { Primary, Secondary, Danger, Ghost, Light, Console }

    /// <summary>Flat rounded button with an optional Segoe MDL2 glyph, hover and pressed states.</summary>
    sealed class FlatButton : Control
    {
        readonly ButtonKind kind;
        readonly string glyph;
        bool hover, pressed;

        public FlatButton(string text, string glyph, ButtonKind kind)
        {
            Text = text;
            this.glyph = glyph;
            this.kind = kind;
            Font = Theme.Body;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
                | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = PreferredSize;
        }

        bool HasGlyph => glyph != null && Theme.Glyphs != null;
        bool HasText => !string.IsNullOrEmpty(Text);

        public override Size GetPreferredSize(Size proposed)
        {
            if (!HasText) return new Size(Theme.S(36), Theme.S(32));

            var textWidth = TextRenderer.MeasureText(Text, Font).Width;
            var glyphWidth = HasGlyph ? Theme.S(18) : 0;
            return new Size(textWidth + glyphWidth + Theme.S(22), Theme.S(32));
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var (fill, border, fore) = Colors();
            if (!Enabled)
            {
                var back = kind == ButtonKind.Ghost || kind == ButtonKind.Light ? Theme.Brand : kind == ButtonKind.Console ? Theme.ConsoleBar : Theme.Card;
                fill = fill.A == 0 ? fill : Theme.Blend(fill, back, 0.6f);
                border = border.A == 0 ? border : Theme.Blend(border, back, 0.5f);
                fore = Theme.Blend(fore, back, 0.6f);
            }

            var bounds = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = Theme.RoundedRect(bounds, Theme.S(6)))
            {
                if (fill.A > 0) using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
                if (border.A > 0) using (var pen = new Pen(border)) g.DrawPath(pen, path);
            }

            const TextFormatFlags centered = TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding;
            if (!HasText)
            {
                if (HasGlyph) TextRenderer.DrawText(g, glyph, Theme.Glyphs, ClientRectangle, fore, centered);
                return;
            }

            var textSize = TextRenderer.MeasureText(Text, Font);
            var glyphWidth = HasGlyph ? Theme.S(18) : 0;
            var x = (Width - textSize.Width - glyphWidth) / 2;
            if (HasGlyph) TextRenderer.DrawText(g, glyph, Theme.Glyphs, new Rectangle(x, 0, Theme.S(16), Height), fore, centered);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(x + glyphWidth, 0, textSize.Width, Height), fore,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
        }

        (Color Fill, Color Border, Color Fore) Colors()
        {
            var active = Enabled && (hover || pressed);
            switch (kind)
            {
                case ButtonKind.Primary:
                    return (pressed ? Theme.Brand : active ? Theme.BrandHover : Theme.Brand, Color.Empty, Color.White);
                case ButtonKind.Danger:
                    return (active ? Color.FromArgb(253, 236, 237) : Theme.Card, active ? Theme.Danger : Theme.Border, Theme.Danger);
                case ButtonKind.Ghost:
                    return (Color.FromArgb(active ? 70 : 40, Color.White), Color.FromArgb(90, Color.White), Color.White);
                case ButtonKind.Light:
                    return (active ? Theme.BrandTint : Color.White, Color.Empty, Theme.Brand);
                case ButtonKind.Console:
                    return (active ? Color.FromArgb(60, 60, 72) : Color.FromArgb(46, 46, 56), Color.Empty, Theme.ConsoleText);
                default:
                    return (active ? Theme.BrandTint : Theme.Card, active ? Theme.Brand : Theme.Border, active ? Theme.Brand : Theme.Text);
            }
        }
    }

    /// <summary>Status dot with a soft halo; pulses while the app is starting or stopping.</summary>
    sealed class Lamp : Control
    {
        Color color = Color.Gray;
        bool pulse, dim;

        public Lamp()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(Theme.S(22), Theme.S(22));
        }

        public void Set(Color value, bool pulsing)
        {
            if (value == color && pulsing == pulse) return;
            color = value;
            pulse = pulsing;
            dim = false;
            Invalidate();
        }

        public void Tick()
        {
            if (!pulse) return;
            dim = !dim;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var halo = Math.Min(Width, Height) - 1f;
            var dot = halo * 0.5f;
            using (var haloBrush = new SolidBrush(Color.FromArgb(dim ? 20 : 55, color)))
            using (var dotBrush = new SolidBrush(dim ? Theme.Blend(color, Color.White, 0.45f) : color))
            {
                g.FillEllipse(haloBrush, 0, 0, halo, halo);
                g.FillEllipse(dotBrush, (halo - dot) / 2, (halo - dot) / 2, dot, dot);
            }
        }
    }

    /// <summary>Rounded status badge: tinted background, coloured text.</summary>
    sealed class Pill : Control
    {
        Color color = Color.Gray;

        public Pill()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI Semibold", 8.5f);
            Size = new Size(Theme.S(96), Theme.S(24));
        }

        public void Set(Color value, string text)
        {
            if (value == color && text == Text) return;
            color = value;
            Text = text;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Theme.RoundedRect(new RectangleF(0, 0, Width - 1, Height - 1), Height / 2f))
            using (var brush = new SolidBrush(Theme.Blend(color, Color.White, 0.86f)))
            {
                g.FillPath(brush, path);
            }
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    /// <summary>White rounded card drawn on the window background; children sit inside its padding.</summary>
    class Card : Panel
    {
        public Card()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Background;
        }

        public Color Accent { get; set; } = Color.Empty;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = Theme.RoundedRect(bounds, Theme.S(10)))
            using (var fill = new SolidBrush(Theme.Card))
            using (var pen = new Pen(Accent.IsEmpty ? Theme.Border : Accent, Accent.IsEmpty ? 1f : 1.5f))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }
        }
    }

    /// <summary>Flat light menu with a brand-tinted hover, matching the cards.</summary>
    sealed class MenuColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Theme.BrandTint;
        public override Color MenuItemBorder => Theme.BrandTint;
        public override Color MenuBorder => Theme.Border;
        public override Color ToolStripDropDownBackground => Color.White;
        public override Color ImageMarginGradientBegin => Color.White;
        public override Color ImageMarginGradientMiddle => Color.White;
        public override Color ImageMarginGradientEnd => Color.White;
        public override Color SeparatorDark => Theme.Border;
        public override Color SeparatorLight => Color.White;
    }
}
