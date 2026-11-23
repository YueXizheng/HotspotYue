using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Networking.Connectivity;

namespace HotspotYue
{
    public sealed class MainForm : Form
    {
        private const int DefaultIntervalSeconds = 5;
        private const int BackoffIntervalMilliseconds = 30000;

        private readonly EventWaitHandle _showEvent;
        private readonly bool _startHidden;

        private readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer();
        private readonly NotifyIcon _tray = new NotifyIcon();
        private readonly ContextMenuStrip _trayMenu = new ContextMenuStrip();

        private readonly Label _lblDot = new Label();
        private readonly Label _lblState = new Label();
        private readonly Label _lblDetail = new Label();
        private readonly Button _btnToggle = new Button();
        private readonly Label _lblInterval = new Label();
        private readonly NumericUpDown _numInterval = new NumericUpDown();
        private readonly Label _lblSeconds = new Label();
        private readonly CheckBox _chkAutostart = new CheckBox();
        private readonly Button _btnExport = new Button();
        private readonly Label _lblLogCaption = new Label();
        private readonly RichTextBox _log = new RichTextBox();

        private Icon _iconOn;
        private Icon _iconOff;
        private Icon _iconWarn;

        private bool _running;
        private bool _busy;
        private bool _allowClose;
        private bool _syncingAutostart;
        private bool _trayHintShown;
        private bool _offEpisodeLogged;

        private int _failures;
        private string _lastState = "";
        private HotspotSnapshot _lastSnapshot = new HotspotSnapshot();
        private DateTime _runningSince = DateTime.Now;

        public MainForm(EventWaitHandle showEvent, bool startHidden)
        {
            _showEvent = showEvent;
            _startHidden = startHidden;

            BuildUi();
            BuildTray();

            Logger.LineAdded += OnLogLine;

            _timer.Interval = DefaultIntervalSeconds * 1000;
            _timer.Tick += OnTimerTick;

            SyncAutostartUi();
            RefreshIdleState();
            StartShowWatcher();

            Logger.Write("HotspotYue 已启动。");
            if (!Autostart.IsEnabled())
            {
                Logger.Write("提示：勾选“开机自动启动”后，登录 Windows 就会自动开始守护。");
            }
        }

        // ---------------------------------------------------------------
        // 界面
        // ---------------------------------------------------------------

        private void BuildUi()
        {
            SuspendLayout();

            _iconOn = MakeIcon(Color.FromArgb(40, 168, 96));
            _iconOff = MakeIcon(Color.FromArgb(132, 140, 152));
            _iconWarn = MakeIcon(Color.FromArgb(212, 72, 58));

            Text = "HotspotYue 热点保活器";
            ClientSize = new Size(620, 600);
            MinimumSize = new Size(560, 480);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = Color.FromArgb(246, 247, 250);
            Icon = _iconOff;
            AutoScaleMode = AutoScaleMode.Dpi;

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 88;
            header.BackColor = Color.Transparent;

            _lblDot.Text = "\u25CF";
            _lblDot.Font = new Font("Segoe UI", 20F);
            _lblDot.ForeColor = Color.FromArgb(132, 140, 152);
            _lblDot.AutoSize = false;
            _lblDot.Size = new Size(32, 40);
            _lblDot.Location = new Point(0, 2);
            _lblDot.TextAlign = ContentAlignment.MiddleCenter;

            _lblState.Text = "已停止";
            _lblState.Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold);
            _lblState.ForeColor = Color.FromArgb(38, 42, 52);
            _lblState.AutoSize = true;
            _lblState.Location = new Point(38, 4);

            _lblDetail.Text = "";
            _lblDetail.ForeColor = Color.FromArgb(96, 102, 116);
            _lblDetail.AutoSize = true;
            _lblDetail.Location = new Point(40, 50);

            header.Controls.Add(_lblDot);
            header.Controls.Add(_lblState);
            header.Controls.Add(_lblDetail);

            Panel controls = new Panel();
            controls.Dock = DockStyle.Top;
            controls.Height = 56;
            controls.BackColor = Color.Transparent;

            _btnToggle.Text = "开启保活";
            _btnToggle.Size = new Size(132, 40);
            _btnToggle.Location = new Point(0, 2);
            _btnToggle.FlatStyle = FlatStyle.Flat;
            _btnToggle.FlatAppearance.BorderColor = Color.FromArgb(190, 196, 208);
            _btnToggle.BackColor = Color.FromArgb(222, 246, 232);
            _btnToggle.Click += OnToggleClick;

            _lblInterval.Text = "检测间隔";
            _lblInterval.AutoSize = true;
            _lblInterval.Location = new Point(150, 4);

            _numInterval.Minimum = 2;
            _numInterval.Maximum = 60;
            _numInterval.Value = DefaultIntervalSeconds;
            _numInterval.Size = new Size(58, 24);
            _numInterval.Location = new Point(150, 26);
            _numInterval.TextAlign = HorizontalAlignment.Right;
            _numInterval.ValueChanged += OnIntervalChanged;

            _lblSeconds.Text = "秒";
            _lblSeconds.AutoSize = true;
            _lblSeconds.Location = new Point(212, 30);

            _chkAutostart.Text = "开机自动启动";
            _chkAutostart.AutoSize = true;
            _chkAutostart.Location = new Point(250, 30);
            _chkAutostart.CheckedChanged += OnAutostartChecked;

            _btnExport.Text = "导出日志";
            _btnExport.Size = new Size(110, 40);
            _btnExport.FlatStyle = FlatStyle.Flat;
            _btnExport.FlatAppearance.BorderColor = Color.FromArgb(190, 196, 208);
            _btnExport.BackColor = Color.FromArgb(252, 252, 254);
            _btnExport.Click += OnExportClick;

            controls.Controls.Add(_btnToggle);
            controls.Controls.Add(_lblInterval);
            controls.Controls.Add(_numInterval);
            controls.Controls.Add(_lblSeconds);
            controls.Controls.Add(_chkAutostart);
            controls.Controls.Add(_btnExport);

            controls.Resize += delegate
            {
                _btnExport.Left = controls.Width - _btnExport.Width;
            };

            Panel caption = new Panel();
            caption.Dock = DockStyle.Top;
            caption.Height = 28;
            caption.BackColor = Color.Transparent;

            _lblLogCaption.Text = "运行日志（只记录状态变化，最多保留 " + Logger.Capacity + " 行，默认不写文件）";
            _lblLogCaption.AutoSize = true;
            _lblLogCaption.ForeColor = Color.FromArgb(96, 102, 116);
            _lblLogCaption.Location = new Point(0, 6);
            caption.Controls.Add(_lblLogCaption);

            _log.Dock = DockStyle.Fill;
            _log.BorderStyle = BorderStyle.FixedSingle;
            _log.BackColor = Color.White;
            _log.ForeColor = Color.FromArgb(45, 48, 55);
            _log.Font = new Font("Microsoft YaHei UI", 9F);
            _log.ReadOnly = true;
            _log.WordWrap = true;
            _log.HideSelection = false;

            // 用一个带内边距的容器承载，避免控件贴边。
            Panel content = new Panel();
            content.Dock = DockStyle.Fill;
            content.Padding = new Padding(16, 12, 16, 12);
            content.BackColor = Color.Transparent;

            // 停靠顺序：后添加的先占边，Fill 必须最先添加（最后停靠）。
            content.Controls.Add(_log);
            content.Controls.Add(caption);
            content.Controls.Add(controls);
            content.Controls.Add(header);

            Controls.Add(content);

            ResumeLayout(false);
        }

        private void BuildTray()
        {
            ToolStripMenuItem openItem = new ToolStripMenuItem("打开主界面");
            openItem.Click += delegate { ShowMainWindow(); };

            ToolStripMenuItem toggleItem = new ToolStripMenuItem("开启保活");
            toggleItem.Name = "toggle";
            toggleItem.Click += delegate { OnToggleClick(null, EventArgs.Empty); };

            ToolStripMenuItem autoItem = new ToolStripMenuItem("开机自动启动");
            autoItem.Name = "auto";
            autoItem.Click += delegate
            {
                _chkAutostart.Checked = !_chkAutostart.Checked;
            };

            ToolStripMenuItem exportItem = new ToolStripMenuItem("导出日志…");
            exportItem.Click += delegate { ExportLogs(); };

            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += delegate
            {
                _allowClose = true;
                Close();
            };

            _trayMenu.Items.Add(openItem);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(toggleItem);
            _trayMenu.Items.Add(autoItem);
            _trayMenu.Items.Add(exportItem);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(exitItem);
            _trayMenu.Opening += delegate { SyncTrayMenu(); };

            _tray.Text = "HotspotYue";
            _tray.Icon = _iconOff;
            _tray.ContextMenuStrip = _trayMenu;
            _tray.Visible = true;
            _tray.DoubleClick += delegate { ShowMainWindow(); };
        }

        private static Icon MakeIcon(Color color)
        {
            Bitmap bitmap = new Bitmap(32, 32);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);

                using (SolidBrush brush = new SolidBrush(color))
                {
                    graphics.FillEllipse(brush, 3, 3, 26, 26);
                }

                using (Pen pen = new Pen(Color.White, 3.2f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    graphics.DrawArc(pen, 10, 10, 12, 12, -45, 270);
                }
            }

            IntPtr handle = bitmap.GetHicon();
            Icon icon = (Icon)Icon.FromHandle(handle).Clone();
            return icon;
        }

        // ---------------------------------------------------------------
        // 保活主循环
        // ---------------------------------------------------------------

        private async void OnTimerTick(object sender, EventArgs e)
        {
            if (!_running || _busy)
            {
                return;
            }

            _busy = true;
            try
            {
                await CheckOnceAsync();
            }
            catch (Exception ex)
            {
                Logger.Write("[错误] 检查异常：" + ex.Message);
            }
            finally
            {
                _busy = false;
            }
        }

        private async Task CheckOnceAsync()
        {
            HotspotSnapshot snapshot = TetheringEngine.GetSnapshot();
            _lastSnapshot = snapshot;

            string state = snapshot.State;
            bool isOn = snapshot.Ok && state == "On";

            if (_lastState.Length == 0)
            {
                _lastState = state;
            }
            else if (!string.Equals(state, _lastState, StringComparison.Ordinal))
            {
                Logger.Write("状态变化：" + TetheringEngine.StateText(_lastState)
                    + " → " + TetheringEngine.StateText(state));
                _lastState = state;
            }

            UpdateRunningUi(snapshot, isOn);
            UpdateTrayIcon(isOn);

            if (isOn)
            {
                if (_failures > 0)
                {
                    Logger.Write("[正常] 热点已恢复。");
                }
                _failures = 0;
                _offEpisodeLogged = false;
                ApplyInterval(BaseIntervalMilliseconds);
                return;
            }

            if (state == "InTransition")
            {
                return;
            }

            if (!_offEpisodeLogged)
            {
                Logger.Write("[警告] 检测到热点未开启，正在自动恢复…");
                _offEpisodeLogged = true;
            }

            TetheringResult result = await TetheringEngine.StartAsync();
            if (result.Success)
            {
                _failures = 0;
                _offEpisodeLogged = false;
                ApplyInterval(BaseIntervalMilliseconds);
                Logger.Write("[正常] 热点已重新开启。");
                ShowBalloon("热点已自动恢复", "HotspotYue 已重新开启移动热点。");
                await RefreshAfterRestoreAsync();
            }
            else
            {
                _failures++;
                if (_failures == 1 || _failures % 10 == 0)
                {
                    Logger.Write("[错误] 恢复失败（累计 " + _failures + " 次）：" + result.Message);
                }
                if (_failures >= 3)
                {
                    ApplyInterval(BackoffIntervalMilliseconds);
                }
            }
        }

        private async Task RefreshAfterRestoreAsync()
        {
            await Task.Delay(800);

            HotspotSnapshot snapshot = TetheringEngine.GetSnapshot();
            _lastSnapshot = snapshot;
            _lastState = snapshot.State;
            UpdateRunningUi(snapshot, snapshot.State == "On");
            UpdateTrayIcon(snapshot.State == "On");
        }

        // ---------------------------------------------------------------
        // 启停
        // ---------------------------------------------------------------

        private void OnToggleClick(object sender, EventArgs e)
        {
            if (_running)
            {
                StopKeeper();
            }
            else
            {
                StartKeeper();
            }
        }

        private void StartKeeper()
        {
            if (_running)
            {
                return;
            }

            _running = true;
            _failures = 0;
            _lastState = "";
            _offEpisodeLogged = false;
            _runningSince = DateTime.Now;

            _btnToggle.Text = "停止保活";
            _btnToggle.BackColor = Color.FromArgb(255, 226, 226);
            _numInterval.Enabled = false;

            ApplyInterval(BaseIntervalMilliseconds);
            _timer.Start();

            Logger.Write("保活已开启，每 " + (_timer.Interval / 1000) + " 秒检查一次。");
            SyncTrayMenu();

            OnTimerTick(this, EventArgs.Empty);
        }

        private void StopKeeper()
        {
            if (!_running)
            {
                return;
            }

            _running = false;
            _timer.Stop();

            _btnToggle.Text = "开启保活";
            _btnToggle.BackColor = Color.FromArgb(222, 246, 232);
            _numInterval.Enabled = true;

            Logger.Write("保活已停止。");
            SyncTrayMenu();
            RefreshIdleState();
            UpdateTrayIcon(false);
        }

        private void ApplyInterval(int milliseconds)
        {
            if (milliseconds < 1000)
            {
                milliseconds = 1000;
            }
            _timer.Interval = milliseconds;
        }

        private int BaseIntervalMilliseconds
        {
            get { return (int)_numInterval.Value * 1000; }
        }

        private void OnIntervalChanged(object sender, EventArgs e)
        {
            if (_running)
            {
                ApplyInterval(BaseIntervalMilliseconds);
            }
        }

        // ---------------------------------------------------------------
        // 界面状态
        // ---------------------------------------------------------------

        private void RefreshIdleState()
        {
            _lblDot.ForeColor = Color.FromArgb(132, 140, 152);
            _lblState.Text = "已停止";
            _lblState.ForeColor = Color.FromArgb(38, 42, 52);
            _lblDetail.Text = "点击“开启保活”开始守护移动热点。";
        }

        private void UpdateRunningUi(HotspotSnapshot snapshot, bool isOn)
        {
            if (!_running)
            {
                RefreshIdleState();
                return;
            }

            if (isOn)
            {
                _lblDot.ForeColor = Color.FromArgb(40, 168, 96);
                _lblState.Text = "热点正常";
                _lblState.ForeColor = Color.FromArgb(24, 120, 72);
            }
            else if (snapshot.State == "InTransition")
            {
                _lblDot.ForeColor = Color.FromArgb(214, 160, 40);
                _lblState.Text = "热点切换中";
                _lblState.ForeColor = Color.FromArgb(150, 110, 20);
            }
            else
            {
                _lblDot.ForeColor = Color.FromArgb(212, 72, 58);
                _lblState.Text = snapshot.Ok ? "热点已断开" : "读取失败";
                _lblState.ForeColor = Color.FromArgb(170, 46, 36);
            }

            string detail = "";
            if (snapshot.Ssid != null && snapshot.Ssid.Length > 0)
            {
                detail = "SSID：" + snapshot.Ssid;
                if (snapshot.Band != null && snapshot.Band.Length > 0)
                {
                    detail = detail + " · " + snapshot.Band;
                }
                detail = detail + " · ";
            }

            TimeSpan elapsed = DateTime.Now - _runningSince;
            detail = detail + "每 " + (_timer.Interval / 1000) + " 秒检查一次 · 已守护 "
                + FormatDuration(elapsed);

            if (!snapshot.Ok && snapshot.Error != null && snapshot.Error.Length > 0)
            {
                detail = detail + " · " + snapshot.Error;
            }

            _lblDetail.Text = detail;
        }

        private static string FormatDuration(TimeSpan span)
        {
            if (span.TotalHours >= 1)
            {
                return ((int)span.TotalHours).ToString("00") + ":" + span.Minutes.ToString("00")
                    + ":" + span.Seconds.ToString("00");
            }
            return span.Minutes.ToString("00") + ":" + span.Seconds.ToString("00");
        }

        private void UpdateTrayIcon(bool isOn)
        {
            if (!_running)
            {
                _tray.Icon = _iconOff;
                _tray.Text = "HotspotYue · 已停止";
                return;
            }

            if (isOn)
            {
                _tray.Icon = _iconOn;
                _tray.Text = "HotspotYue · 热点正常";
            }
            else
            {
                _tray.Icon = _iconWarn;
                _tray.Text = "HotspotYue · 热点已断开";
            }
        }

        private void SyncTrayMenu()
        {
            ToolStripMenuItem toggle = _trayMenu.Items["toggle"] as ToolStripMenuItem;
            if (toggle != null)
            {
                toggle.Text = _running ? "停止保活" : "开启保活";
            }

            ToolStripMenuItem auto = _trayMenu.Items["auto"] as ToolStripMenuItem;
            if (auto != null)
            {
                auto.Checked = Autostart.IsEnabled();
            }
        }

        // ---------------------------------------------------------------
        // 日志
        // ---------------------------------------------------------------

        private void OnLogLine(string line)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action<string>(OnLogLine), line);
                    return;
                }
                AppendLogLine(line);
            }
            catch (Exception)
            {
                // 窗体关闭过程中忽略。
            }
        }

        private void AppendLogLine(string line)
        {
            Color color = Color.FromArgb(45, 48, 55);
            if (line.IndexOf("[错误]", StringComparison.Ordinal) >= 0)
            {
                color = Color.FromArgb(200, 40, 40);
            }
            else if (line.IndexOf("[警告]", StringComparison.Ordinal) >= 0)
            {
                color = Color.FromArgb(196, 116, 0);
            }
            else if (line.IndexOf("[正常]", StringComparison.Ordinal) >= 0)
            {
                color = Color.FromArgb(20, 130, 60);
            }

            _log.SelectionStart = _log.TextLength;
            _log.SelectionLength = 0;
            _log.SelectionColor = color;
            _log.AppendText(line + Environment.NewLine);
            _log.SelectionStart = _log.TextLength;
            _log.ScrollToCaret();
        }

        private void OnExportClick(object sender, EventArgs e)
        {
            ExportLogs();
        }

        private void ExportLogs()
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "日志文件 (*.log)|*.log|文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*";
                dialog.FileName = "HotspotYue-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log";
                dialog.Title = "导出运行日志";

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    File.WriteAllText(dialog.FileName, Logger.FullText(), Encoding.UTF8);
                    Logger.Write("日志已导出：" + dialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "导出失败：" + ex.Message, "HotspotYue",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        // ---------------------------------------------------------------
        // 开机自启
        // ---------------------------------------------------------------

        private void SyncAutostartUi()
        {
            bool enabled = Autostart.IsEnabled();

            _syncingAutostart = true;
            try
            {
                _chkAutostart.Checked = enabled;
            }
            finally
            {
                _syncingAutostart = false;
            }
            SyncTrayMenu();
        }

        private void OnAutostartChecked(object sender, EventArgs e)
        {
            if (_syncingAutostart)
            {
                return;
            }

            try
            {
                if (_chkAutostart.Checked)
                {
                    Autostart.Enable();
                    Logger.Write("已开启开机自动启动（写入当前用户启动项）。");
                }
                else
                {
                    Autostart.Disable();
                    Logger.Write("已关闭开机自动启动。");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "设置开机自启失败：" + ex.Message, "HotspotYue",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);

                _syncingAutostart = true;
                try
                {
                    _chkAutostart.Checked = Autostart.IsEnabled();
                }
                finally
                {
                    _syncingAutostart = false;
                }
            }

            SyncTrayMenu();
        }

        // ---------------------------------------------------------------
        // 窗口与托盘
        // ---------------------------------------------------------------

        private void StartShowWatcher()
        {
            Thread watcher = new Thread(delegate()
            {
                while (true)
                {
                    try
                    {
                        _showEvent.WaitOne();
                    }
                    catch (Exception)
                    {
                        return;
                    }

                    try
                    {
                        if (IsDisposed)
                        {
                            return;
                        }
                        BeginInvoke(new Action(ShowMainWindow));
                    }
                    catch (Exception)
                    {
                        return;
                    }
                }
            });

            watcher.IsBackground = true;
            watcher.Start();
        }

        private void ShowMainWindow()
        {
            if (IsDisposed)
            {
                return;
            }

            ShowInTaskbar = true;
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        private void ShowBalloon(string title, string text)
        {
            if (_tray == null || !_tray.Visible)
            {
                return;
            }

            try
            {
                _tray.ShowBalloonTip(3000, title, text, ToolTipIcon.Info);
            }
            catch (Exception)
            {
                // 通知失败不影响保活。
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (_startHidden)
            {
                Hide();
                ShowInTaskbar = false;
                StartKeeper();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                ShowInTaskbar = false;

                if (!_trayHintShown)
                {
                    _trayHintShown = true;
                    ShowBalloon("HotspotYue 仍在运行",
                        _running ? "窗口已收进托盘，热点守护继续。右键托盘图标可退出。" : "窗口已收进托盘。");
                }
                return;
            }

            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try
            {
                _timer.Stop();
                _timer.Dispose();
            }
            catch (Exception)
            {
            }

            Logger.LineAdded -= OnLogLine;

            try
            {
                _tray.Visible = false;
                _tray.Dispose();
            }
            catch (Exception)
            {
            }

            base.OnFormClosed(e);
        }
    }
}