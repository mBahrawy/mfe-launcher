using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MfeLauncher
{
    sealed class MainForm : Form
    {
        // Lets a whole `git pull` (refs, working tree, submodule) settle before re-reading the apps.
        const int GitSettleMs = 3000;
        const int CardWidth = 880;

        static readonly AppStatus[] AliveStatuses = { AppStatus.Running, AppStatus.Starting, AppStatus.External, AppStatus.Error };

        readonly string root;
        readonly string projectName;
        readonly ProcessJob job = new ProcessJob();
        readonly List<AppCard> cards = new List<AppCard>();
        readonly Dictionary<MfeApp, LogForm> logForms = new Dictionary<MfeApp, LogForm>();
        readonly Timer statusTimer = new Timer { Interval = 1000 };
        readonly Timer pulseTimer = new Timer { Interval = 500 };
        readonly Timer gitSettleTimer = new Timer { Interval = GitSettleMs };
        readonly ToolTip tip = new ToolTip();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly ContextMenuStrip actionsMenu = new ContextMenuStrip { Renderer = new ToolStripProfessionalRenderer(new MenuColors()), Font = Theme.Body, ShowImageMargin = true };
        readonly List<ToolStripItem> maintenanceItems = new List<ToolStripItem>();
        readonly Label subtitle = new Label { AutoSize = true, Font = Theme.Body, ForeColor = Color.FromArgb(225, 210, 240), BackColor = Color.Transparent };
        readonly Label footer = new Label { AutoSize = false, Dock = DockStyle.Fill, Font = Theme.Small, ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleLeft };
        readonly FlowLayoutPanel list = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Theme.Background
        };
        readonly GitWatcher gitWatcher;
        MfeApp lastFailed;
        bool maintenanceRunning;

        public MainForm(string root, List<MfeApp> apps)
        {
            this.root = root;
            projectName = Path.GetFileName(root.TrimEnd('\\'));
            Text = projectName + " — MFE Launcher";
            Icon = Theme.AppIcon(32);
            Font = Theme.Body;
            BackColor = Theme.Background;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            list.Padding = new Padding(Theme.S(16), Theme.S(14), Theme.S(16), Theme.S(6));

            var footerBar = new Panel { Dock = DockStyle.Bottom, Height = Theme.S(30), Padding = new Padding(Theme.S(18), 0, Theme.S(18), Theme.S(4)) };
            footerBar.Controls.Add(footer);
            Controls.Add(list);
            Controls.Add(footerBar);
            Controls.Add(BuildHeader());
            BuildActionsMenu();

            tray.Icon = Theme.AppIcon(16);
            tray.Text = Text.Length > 63 ? Text.Substring(0, 63) : Text;
            tray.Visible = true;
            tray.BalloonTipClicked += (_, _) => { BringToFront(this); if (lastFailed != null) ShowLogs(lastFailed); };
            tray.DoubleClick += (_, _) => BringToFront(this);

            gitWatcher = new GitWatcher(new[] { root }.Concat(apps.Select(a => a.WorkspaceDir)));
            gitWatcher.Changed += OnGitChanged;
            gitSettleTimer.Tick += (_, _) =>
            {
                gitSettleTimer.Stop();
                Recheck("Auto re-checked after git change");
            };

            ShowApps(apps);
            footer.Text = gitWatcher.IsWatching
                ? "Watching git — the list re-checks itself after pull, checkout or submodule update."
                : "Not a git repository — use Re-check after updating.";

            statusTimer.Tick += (_, _) => RefreshStatus();
            pulseTimer.Tick += (_, _) => cards.ForEach(c => c.Tick());
            statusTimer.Start();
            pulseTimer.Start();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            var running = cards.Count(c => c.App.IsOwnAlive);
            if (running > 0 && !Confirm($"Stop {running} running app(s) and exit?", Text, MessageBoxIcon.Question))
            {
                e.Cancel = true;
                return;
            }
            // The job object kills every started process tree once this process exits.
            tray.Visible = false;
            tray.Dispose();
            gitWatcher.Dispose();
            base.OnFormClosing(e);
        }

        Control BuildHeader()
        {
            var header = new GradientPanel { Dock = DockStyle.Top, Height = Theme.S(104) };
            var eyebrow = new Label { Text = "MFE LAUNCHER", AutoSize = true, Font = Theme.Small, ForeColor = Color.FromArgb(200, 180, 225), BackColor = Color.Transparent };
            var title = new Label { Text = projectName, AutoSize = true, Font = Theme.Title, ForeColor = Color.White, BackColor = Color.Transparent };
            var path = new Label
            {
                Text = root, AutoSize = false, AutoEllipsis = true, Font = Theme.Small, Cursor = Cursors.Hand,
                ForeColor = Color.FromArgb(225, 210, 240), BackColor = Color.Transparent, Height = Theme.Small.Height + Theme.S(2)
            };
            path.Click += (_, _) => Process.Start("explorer.exe", $"\"{root}\"");
            var underlined = new Font(Theme.Small, FontStyle.Underline);
            path.MouseEnter += (_, _) => path.Font = underlined;
            path.MouseLeave += (_, _) => path.Font = Theme.Small;
            tip.SetToolTip(path, root + "\nClick to open the folder");
            var startAll = new FlatButton("Start all", Theme.GlyphPlay, ButtonKind.Light);
            var recheck = new FlatButton("Re-check apps", Theme.GlyphSync, ButtonKind.Ghost);
            var more = new FlatButton("", Theme.GlyphMore, ButtonKind.Ghost);
            startAll.Click += (_, _) => StartAll();
            recheck.Click += (_, _) => Recheck("Re-checked");
            more.Click += (_, _) => actionsMenu.Show(more, new Point(more.Width, more.Height + Theme.S(4)), ToolStripDropDownDirection.BelowLeft);
            tip.SetToolTip(startAll, "Start every app that is not running here");
            tip.SetToolTip(recheck, "Re-read the apps, e.g. after git pull brought a new MFE");
            tip.SetToolTip(more, "More actions");

            header.Controls.AddRange(new Control[] { eyebrow, title, path, subtitle, startAll, recheck, more });
            header.Layout += (_, _) =>
            {
                var right = header.Width - Theme.S(20);
                foreach (var button in new Control[] { more, recheck, startAll })
                {
                    button.Location = new Point(right - button.Width, (header.Height - button.Height) / 2);
                    right = button.Left - Theme.S(8);
                }

                var left = Theme.S(22);
                eyebrow.Location = new Point(left, Theme.S(10));
                title.Location = new Point(Theme.S(20), eyebrow.Bottom);
                path.Location = new Point(left, title.Bottom);
                path.Width = Math.Min(path.GetPreferredSize(Size.Empty).Width + Theme.S(4), right - Theme.S(16) - left);
                subtitle.Location = new Point(left, path.Bottom + Theme.S(2));
            };
            return header;
        }

        void BuildActionsMenu()
        {
            ToolStripMenuItem Item(string text, string glyph, Color color, Action onClick)
            {
                var item = new ToolStripMenuItem(text, Theme.GlyphImage(glyph, color)) { Padding = new Padding(0, Theme.S(3), 0, Theme.S(3)) };
                item.Click += (_, _) => onClick();
                return item;
            }

            var resetNx = Item("Reset Nx && clear caches", Theme.GlyphRepair, Theme.Text, ResetNx);
            var cleanNpm = Item("Clean npm cache", Theme.GlyphPackage, Theme.Text, CleanNpmCache);
            resetNx.ToolTipText = "nx reset (daemon, .nx cache, workspace data) and .angular/cache in every workspace";
            cleanNpm.ToolTipText = "npm cache clean --force";
            maintenanceItems.AddRange(new ToolStripItem[] { resetNx, cleanNpm });
            actionsMenu.ImageScalingSize = new Size(Theme.S(16), Theme.S(16));

            actionsMenu.Items.AddRange(new ToolStripItem[]
            {
                Item("Start all", Theme.GlyphPlay, Theme.Brand, StartAll),
                Item("Restart all", Theme.GlyphRefresh, Theme.Text, RestartAll),
                Item("Close all", Theme.GlyphStop, Theme.Danger, CloseAll),
                new ToolStripSeparator(),
                resetNx,
                cleanNpm
            });
            actionsMenu.Opening += (_, _) => maintenanceItems.ForEach(i => i.Enabled = !maintenanceRunning);
        }

        void OnGitChanged()
        {
            if (!IsHandleCreated || IsDisposed) return;
            BeginInvoke((Action)(() =>
            {
                gitSettleTimer.Stop();
                gitSettleTimer.Start();
            }));
        }

        void OnAppFailed(MfeApp app, string reason)
        {
            if (!IsHandleCreated || IsDisposed) return;
            BeginInvoke((Action)(() =>
            {
                lastFailed = app;
                tray.ShowBalloonTip(8000, $"{app.Name} — serve error", Shorten(reason, 200), ToolTipIcon.Error);
                if (!ContainsFocus) FlashTaskbar(Handle);
                RefreshStatus();
            }));
        }

        void OnStart(AppCard card)
        {
            var app = card.App;
            var owner = app.ForeignPortOwner(Ports.ListeningOwners());
            if (owner == null)
            {
                app.Start(job);
                RefreshStatus();
                return;
            }

            var question = $"Port {app.Port} is already in use by {Ports.Describe(owner.Value)}.\n\n"
                + $"Stop that process and start {app.Name} on port {app.Port}?";
            if (Confirm(question, "Port in use", MessageBoxIcon.Warning)) Run(app.RestartAsync(job));
        }

        void StartAll()
        {
            var listeners = Ports.ListeningOwners();
            var toStart = cards.Select(c => c.App).Where(a => !a.IsOwnAlive).ToList();
            if (toStart.Count == 0)
            {
                footer.Text = "Every app is already running from the launcher.";
                return;
            }

            var busy = toStart.Where(a => a.ForeignPortOwner(listeners) != null).ToList();
            var stopBusy = false;
            if (busy.Count > 0)
            {
                var lines = busy.Select(a => $"   •  {a.Name}  —  port {a.Port}, {Ports.Describe(a.ForeignPortOwner(listeners).Value)}");
                var answer = MessageBox.Show(this,
                    "These ports are already in use:\n\n" + string.Join("\n", lines) + "\n\n"
                    + "Yes  —  stop those processes and start the apps on their ports\n"
                    + "No  —  start only the apps whose ports are free",
                    "Start all", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (answer == DialogResult.Cancel) return;
                stopBusy = answer == DialogResult.Yes;
            }

            var restarts = new List<Task>();
            foreach (var app in toStart)
            {
                if (!busy.Contains(app)) app.Start(job);
                else if (stopBusy) restarts.Add(app.RestartAsync(job));
            }
            Run(Task.WhenAll(restarts));
        }

        void RestartAll()
        {
            var listeners = Ports.ListeningOwners();
            var targets = cards.Select(c => c.App).Where(a => a.IsOwnAlive || a.GetStatus(listeners) == AppStatus.Crashed).ToList();
            if (targets.Count == 0)
            {
                footer.Text = "Nothing to restart — no apps are running from the launcher.";
                return;
            }
            Run(Task.WhenAll(targets.Select(a => a.RestartAsync(job))));
        }

        void CloseAll()
        {
            var listeners = Ports.ListeningOwners();
            var running = cards.Select(c => c.App).Where(a => AliveStatuses.Contains(a.GetStatus(listeners))).ToList();
            if (running.Count == 0)
            {
                footer.Text = "Nothing to close — no apps are running.";
                return;
            }

            var external = running.Count(a => !a.IsOwnAlive);
            var question = $"Close {running.Count} running app(s)?"
                + (external > 0 ? $"\n\n{external} of them were started outside the launcher (e.g. a terminal) and will be stopped too." : "");
            if (Confirm(question, "Close all", MessageBoxIcon.Question)) Run(Task.WhenAll(running.Select(a => a.StopAsync())));
        }

        async void ResetNx()
        {
            var apps = cards.Select(c => c.App).ToList();
            var own = apps.Where(a => a.IsOwnAlive).ToList();
            var workspaces = string.Join(", ", apps.Select(a => a.Workspace).Distinct());
            var question = $"Reset Nx and clear caches in {workspaces}?\n\n"
                + "Runs nx reset (stops the Nx daemon, deletes the .nx cache and workspace data) and deletes .angular/cache."
                + (own.Count > 0 ? $"\n\n{own.Count} app(s) started here will be stopped first." : "");
            if (!Confirm(question, "Reset Nx", MessageBoxIcon.Warning)) return;

            await Task.WhenAll(own.Select(a => a.StopAsync()));
            RunMaintenance("Resetting Nx", () => Maintenance.ResetNx(apps));
        }

        void CleanNpmCache()
        {
            const string question = "Clean the npm cache (npm cache clean --force)?\n\nThe next npm install will download every package again.";
            if (Confirm(question, "Clean npm cache", MessageBoxIcon.Warning)) RunMaintenance("Cleaning npm cache", () => Maintenance.CleanNpmCache(root));
        }

        async void RunMaintenance(string title, Func<string> work)
        {
            maintenanceRunning = true;
            footer.Text = title + "…";
            try
            {
                var result = await Task.Run(work);
                footer.Text = $"{result}  ({DateTime.Now:HH:mm:ss})";
                tray.ShowBalloonTip(4000, title + " — done", result, ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                footer.Text = $"{title} failed at {DateTime.Now:HH:mm:ss}.";
                MessageBox.Show(this, ex.Message, title + " failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                maintenanceRunning = false;
                RefreshStatus();
            }
        }

        /// <summary>Re-reads the apps; known apps keep their process and log, apps still running are never dropped.</summary>
        void Recheck(string reason)
        {
            List<MfeApp> found;
            try
            {
                found = AppDiscovery.Discover(root);
            }
            catch (Exception ex)
            {
                footer.Text = $"{reason} at {DateTime.Now:HH:mm:ss} — failed: {ex.Message}";
                return;
            }

            var current = cards.Select(c => c.App).ToDictionary(Key);
            var merged = found
                .Select(app => current.TryGetValue(Key(app), out var known) && (known.IsOwnAlive || known.Port == app.Port) ? known : app)
                .ToList();
            merged.AddRange(current.Values.Where(app => app.IsOwnAlive && !merged.Contains(app)));

            var added = found.Where(app => !current.ContainsKey(Key(app))).Select(app => app.Name).ToList();
            var removed = current.Values.Where(app => found.All(f => Key(f) != Key(app))).Select(app => app.Name).ToList();

            ShowApps(merged);
            footer.Text = $"{reason} at {DateTime.Now:HH:mm:ss} — {Summary(added, removed)}";
            if (added.Count > 0) tray.ShowBalloonTip(5000, "New MFE apps found", string.Join(", ", added), ToolTipIcon.Info);
        }

        static string Summary(List<string> added, List<string> removed)
        {
            if (added.Count == 0 && removed.Count == 0) return "no app changes.";

            var parts = new List<string>();
            if (added.Count > 0) parts.Add("new: " + string.Join(", ", added));
            if (removed.Count > 0) parts.Add("removed: " + string.Join(", ", removed));
            return string.Join("; ", parts) + ".";
        }

        static string Key(MfeApp app) => app.WorkspaceDir + "|" + app.Name;

        void ShowApps(List<MfeApp> apps)
        {
            list.SuspendLayout();
            foreach (Control old in list.Controls.Cast<Control>().ToList()) old.Dispose();
            cards.Clear();

            if (apps.Count == 0)
            {
                list.Controls.Add(new Label
                {
                    Text = "No Nx apps with a serve target found in\n" + root, AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(Theme.S(4))
                });
            }
            foreach (var app in apps)
            {
                app.Failed -= OnAppFailed;
                app.Failed += OnAppFailed;
                var card = new AppCard(app, tip, OnStart, c => Run(c.App.RestartAsync(job)), c => Run(c.App.StopAsync()), c => ShowLogs(c.App))
                {
                    Width = Theme.S(CardWidth)
                };
                cards.Add(card);
                list.Controls.Add(card);
            }
            list.ResumeLayout();

            FitToContent();
            RefreshStatus();
        }

        void FitToContent()
        {
            var cardsHeight = cards.Sum(c => c.Height + c.Margin.Bottom);
            var chrome = Controls.Cast<Control>().Where(c => c != list).Sum(c => c.Height) + list.Padding.Vertical;
            var maxHeight = (int)(Screen.FromControl(this).WorkingArea.Height * 0.85) - (Height - ClientSize.Height);
            ClientSize = new Size(Theme.S(CardWidth) + list.Padding.Horizontal + SystemInformation.VerticalScrollBarWidth,
                Math.Min(maxHeight, chrome + Math.Max(cardsHeight, Theme.S(60))));
        }

        void RefreshStatus()
        {
            var listeners = Ports.ListeningOwners();
            var statuses = cards.Select(c => (Card: c, Status: c.App.GetStatus(listeners))).ToList();
            statuses.ForEach(s => s.Card.Update(s.Status, s.Card.App.ForeignPortOwner(listeners)));

            int Count(params AppStatus[] wanted) => statuses.Count(s => wanted.Contains(s.Status));
            var parts = new List<string> { $"{cards.Count} apps", $"{Count(AppStatus.Running, AppStatus.External)} running" };
            var failed = Count(AppStatus.Error, AppStatus.Crashed);
            if (failed > 0) parts.Add($"{failed} with errors");
            subtitle.Text = string.Join("  ·  ", parts);
        }

        async void Run(Task action)
        {
            RefreshStatus();
            try
            {
                await action;
            }
            finally
            {
                RefreshStatus();
            }
        }

        void ShowLogs(MfeApp app)
        {
            if (!logForms.TryGetValue(app, out var form) || form.IsDisposed)
            {
                form = new LogForm(app) { Icon = Icon };
                logForms[app] = form;
                form.Show();
            }
            BringToFront(form);
        }

        bool Confirm(string question, string title, MessageBoxIcon icon) =>
            MessageBox.Show(this, question, title, MessageBoxButtons.OKCancel, icon) == DialogResult.OK;

        static void BringToFront(Form form)
        {
            if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
            form.Show();
            form.Activate();
        }

        static string Shorten(string text, int max) => text.Length <= max ? text : text.Substring(0, max - 1) + "…";

        static void FlashTaskbar(IntPtr handle)
        {
            const uint flashTrayUntilForeground = 0x2 | 0xC;
            var info = new FlashInfo { Size = (uint)Marshal.SizeOf(typeof(FlashInfo)), Window = handle, Flags = flashTrayUntilForeground };
            FlashWindowEx(ref info);
        }

        [StructLayout(LayoutKind.Sequential)]
        struct FlashInfo
        {
            public uint Size;
            public IntPtr Window;
            public uint Flags;
            public uint Count;
            public uint Timeout;
        }

        [DllImport("user32.dll")]
        static extern bool FlashWindowEx(ref FlashInfo info);

        sealed class GradientPanel : Panel
        {
            public GradientPanel()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                using (var brush = new LinearGradientBrush(ClientRectangle, Theme.Brand, Color.FromArgb(124, 40, 196), LinearGradientMode.Horizontal))
                {
                    e.Graphics.FillRectangle(brush, ClientRectangle);
                }
            }
        }
    }
}
