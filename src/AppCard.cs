using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace MfeLauncher
{
    /// <summary>One app: lamp, name, port warning, details line, status pill and its actions.</summary>
    sealed class AppCard : Card
    {
        readonly Lamp lamp = new Lamp();
        readonly Label name = new Label { AutoSize = true, Font = Theme.Strong, ForeColor = Theme.Text, BackColor = Theme.Card };
        readonly Label portWarning = new Label
        {
            AutoSize = true, Visible = false, Cursor = Cursors.Help, ForeColor = Theme.Warning, BackColor = Theme.Card,
            Font = Theme.Glyphs ?? Theme.Strong, Text = Theme.Glyphs != null ? Theme.GlyphWarning : "⚠"
        };
        readonly LinkLabel details = new LinkLabel
        {
            AutoSize = false, AutoEllipsis = true, Font = Theme.Small, BackColor = Theme.Card,
            LinkColor = Theme.Brand, ActiveLinkColor = Theme.BrandHover, LinkBehavior = LinkBehavior.HoverUnderline
        };
        readonly Pill pill = new Pill();
        readonly FlatButton start = new FlatButton("Start", Theme.GlyphPlay, ButtonKind.Primary);
        readonly FlatButton restart = new FlatButton("Restart", Theme.GlyphRefresh, ButtonKind.Secondary);
        readonly FlatButton close = new FlatButton("Close", Theme.GlyphStop, ButtonKind.Danger);
        readonly FlatButton logs = new FlatButton("Logs", Theme.GlyphLogs, ButtonKind.Secondary);
        readonly ToolTip tip;
        string detailText;
        int? warnedPid;

        public AppCard(MfeApp app, ToolTip tip, Action<AppCard> onStart, Action<AppCard> onRestart, Action<AppCard> onClose, Action<AppCard> onLogs)
        {
            App = app;
            this.tip = tip;
            Height = Theme.S(66);
            Margin = new Padding(0, 0, 0, Theme.S(8));
            name.Text = app.Name;

            details.LinkClicked += (_, _) => { if (app.Port > 0) Process.Start($"http://localhost:{app.Port}"); };
            start.Click += (_, _) => onStart(this);
            restart.Click += (_, _) => onRestart(this);
            close.Click += (_, _) => onClose(this);
            logs.Click += (_, _) => onLogs(this);

            Controls.AddRange(new Control[] { lamp, name, portWarning, details, pill, start, restart, close, logs });
        }

        public MfeApp App { get; }

        public void Update(AppStatus status, int? foreignPortOwner)
        {
            var (color, text) = Theme.Statuses[status];
            var pulsing = status == AppStatus.Starting || status == AppStatus.Stopping;
            var failed = status == AppStatus.Error || status == AppStatus.Crashed;

            lamp.Set(color, pulsing);
            pill.Set(color, text);
            SetDetails(failed ? App.LastError ?? "Stopped unexpectedly — see Logs" : null);
            SetPortWarning(foreignPortOwner);

            var accent = failed ? Theme.Blend(Theme.Danger, Color.White, 0.4f) : Color.Empty;
            if (accent != Accent)
            {
                Accent = accent;
                Invalidate();
            }

            var alive = status == AppStatus.Running || status == AppStatus.Starting || status == AppStatus.External || status == AppStatus.Error;
            start.Enabled = status == AppStatus.Stopped || status == AppStatus.Crashed || status == AppStatus.External;
            restart.Enabled = alive || status == AppStatus.Crashed;
            close.Enabled = alive;
        }

        public void Tick() => lamp.Tick();

        void SetPortWarning(int? pid)
        {
            portWarning.Visible = pid != null;
            if (pid == warnedPid) return;

            warnedPid = pid;
            if (pid != null)
            {
                tip.SetToolTip(portWarning, $"Port {App.Port} is already in use by {Ports.Describe(pid.Value)}.\nStart will offer to stop it and run {App.Name} here.");
            }
        }

        void SetDetails(string error)
        {
            var text = error ?? (App.Port > 0 ? $"{App.Workspace}  ·  localhost:{App.Port}" : App.Workspace);
            if (text == detailText) return;

            detailText = text;
            details.Text = text;
            details.ForeColor = error != null ? Theme.Danger : Theme.Muted;
            var link = text.IndexOf("localhost:", StringComparison.Ordinal);
            details.LinkArea = error == null && link >= 0 ? new LinkArea(link, text.Length - link) : new LinkArea(0, 0);
            tip.SetToolTip(details, error ?? "Open in browser");
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            var gap = Theme.S(6);
            var right = Width - Theme.S(14);
            foreach (var button in new Control[] { logs, close, restart, start })
            {
                button.Location = new Point(right - button.Width, (Height - button.Height) / 2);
                right = button.Left - gap;
            }
            pill.Location = new Point(right - Theme.S(10) - pill.Width, (Height - pill.Height) / 2);

            lamp.Location = new Point(Theme.S(14), (Height - lamp.Height) / 2);
            var textLeft = lamp.Right + Theme.S(10);
            var textTop = (Height - name.Height - Theme.S(18)) / 2;
            name.Location = new Point(textLeft, textTop);
            portWarning.Location = new Point(name.Right + Theme.S(4), name.Top + (name.Height - portWarning.Height) / 2 + Theme.S(1));
            details.SetBounds(textLeft + Theme.S(1), name.Bottom, Math.Max(0, pill.Left - textLeft - Theme.S(12)), Theme.S(18));
        }
    }
}
