using System;
using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace MfeLauncher
{
    sealed class LogForm : Form
    {
        const int MaxVisibleLines = 6000;
        static readonly Regex ErrorLine = new Regex(@"\[ERROR\]|\berror\b|failed|ERR!|✘", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex WarningLine = new Regex(@"\[WARNING\]|\bwarn(ing)?\b|▲", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex SuccessLine = new Regex(@"generation complete|Local:\s+http|compiled successfully", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        readonly MfeApp app;
        readonly RichTextBox output = new RichTextBox
        {
            ReadOnly = true,
            WordWrap = false,
            ScrollBars = RichTextBoxScrollBars.Both,
            Dock = DockStyle.Fill,
            Font = Theme.Mono,
            BackColor = Theme.ConsoleBack,
            ForeColor = Theme.ConsoleText,
            BorderStyle = BorderStyle.None,
            DetectUrls = false
        };

        public LogForm(MfeApp app)
        {
            this.app = app;
            Text = $"{app.Name} — logs";
            Size = new Size(Theme.S(980), Theme.S(580));
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.ConsoleBack;

            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(Theme.S(12), Theme.S(8), Theme.S(4), Theme.S(4)), BackColor = Theme.ConsoleBack };
            body.Controls.Add(output);
            Controls.Add(body);
            Controls.Add(BuildToolbar());

            foreach (var line in app.LogSnapshot()) Append(line);
            app.LineAdded += OnLineAdded;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ScrollToEnd();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            app.LineAdded -= OnLineAdded;
            base.OnFormClosed(e);
        }

        Control BuildToolbar()
        {
            var bar = new Panel { Dock = DockStyle.Top, Height = Theme.S(48), BackColor = Theme.ConsoleBar };
            var title = new Label
            {
                Text = app.Port > 0 ? $"{app.Name}   ·   {app.Workspace}   ·   localhost:{app.Port}" : $"{app.Name}   ·   {app.Workspace}",
                AutoSize = true, Font = Theme.Strong, ForeColor = Color.White, BackColor = Theme.ConsoleBar
            };
            var open = new FlatButton("Open", Theme.GlyphOpen, ButtonKind.Console) { Enabled = app.Port > 0 };
            var copy = new FlatButton("Copy", Theme.GlyphCopy, ButtonKind.Console);
            var clear = new FlatButton("Clear", Theme.GlyphClear, ButtonKind.Console);
            open.Click += (_, _) => Process.Start($"http://localhost:{app.Port}");
            copy.Click += (_, _) => { if (output.TextLength > 0) Clipboard.SetText(output.Text); };
            clear.Click += (_, _) => { app.ClearLog(); output.Clear(); };

            bar.Controls.AddRange(new Control[] { title, open, copy, clear });
            bar.Layout += (_, _) =>
            {
                title.Location = new Point(Theme.S(14), (bar.Height - title.Height) / 2);
                var right = bar.Width - Theme.S(12);
                foreach (var button in new Control[] { clear, copy, open })
                {
                    button.Location = new Point(right - button.Width, (bar.Height - button.Height) / 2);
                    right = button.Left - Theme.S(6);
                }
            };
            return bar;
        }

        void OnLineAdded(string line)
        {
            if (!IsHandleCreated || IsDisposed) return;
            BeginInvoke((Action)(() =>
            {
                Append(line);
                ScrollToEnd();
            }));
        }

        void Append(string line)
        {
            output.SelectionStart = output.TextLength;
            output.SelectionLength = 0;
            output.SelectionColor = ColorFor(line);
            output.AppendText(line + "\n");

            var lineCount = output.GetLineFromCharIndex(output.TextLength);
            if (lineCount > MaxVisibleLines) TrimTop(lineCount - MaxVisibleLines);
        }

        void TrimTop(int lines)
        {
            output.ReadOnly = false;
            output.Select(0, output.GetFirstCharIndexFromLine(lines));
            output.SelectedText = "";
            output.ReadOnly = true;
        }

        void ScrollToEnd()
        {
            output.SelectionStart = output.TextLength;
            output.ScrollToCaret();
        }

        static Color ColorFor(string line)
        {
            if (line.StartsWith("[launcher]") || line.StartsWith("> ")) return Color.FromArgb(150, 160, 255);
            if (ErrorLine.IsMatch(line)) return Color.FromArgb(255, 107, 107);
            if (WarningLine.IsMatch(line)) return Color.FromArgb(240, 190, 90);
            if (SuccessLine.IsMatch(line)) return Color.FromArgb(110, 220, 140);
            return Theme.ConsoleText;
        }
    }
}
