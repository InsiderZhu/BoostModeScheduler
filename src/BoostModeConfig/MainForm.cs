using System.Diagnostics;
using System.ServiceProcess;
using BoostModeCommon;
using BoostModeCommon.Models;

namespace BoostModeConfig;

public class MainForm : Form
{
    private AppConfig _config;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private ServiceController? _service;
    private Dictionary<int, string> _modeNames;

    private GroupBox grpService = null!, grpStatus = null!, grpSettings = null!;
    private GroupBox grpWhitelist = null!, grpOverride = null!, grpLog = null!;
    private GroupBox grpSwitchLog = null!;
    private Label lblServiceStatus = null!, lblCurrentMode = null!, lblCpuUsage = null!;
    private Label lblGameProcesses = null!, lblLastSwitch = null!, lblLastAutoSwitch = null!;
    private Button btnStart = null!, btnStop = null!, btnRestart = null!;
    private NumericUpDown numLoadThreshold = null!, numIdleThreshold = null!;
    private NumericUpDown numPollInterval = null!, numLoadHold = null!, numIdleHold = null!;
    private ComboBox cmbIdleModeAc = null!, cmbIdleModeDc = null!;
    private ComboBox cmbLoadModeAc = null!, cmbLoadModeDc = null!;
    private ListBox lstProcesses = null!;
    private TextBox txtNewProcess = null!, txtSwitchLog = null!;
    private Button btnAdd = null!, btnRemove = null!;
    private ComboBox cmbManualAc = null!, cmbManualDc = null!;
    private Button btnApplyManual = null!;
    private Button btnSave = null!, btnRefresh = null!;
    private Button btnEditConfig = null!, btnOpenLogFolder = null!, btnViewLog = null!;
    private CheckBox chkAutoStart = null!, chkCloseToTray = null!, chkEnableNotify = null!;
    private NotifyIcon trayIcon = null!;
    private bool _realClosing;
    private DateTime _lastNotifiedSwitch = DateTime.MinValue;

    public MainForm()
    {
        Text = "BoostModeScheduler - 配置工具";
        MinimumSize = new Size(680, 720);
        StartPosition = FormStartPosition.CenterScreen;

        _config = ConfigManager.Load();
        _modeNames = LoadModeNames();

        BuildUI();
        LayoutControls();
        Resize += (_, _) => LayoutControls();

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _refreshTimer.Tick += (_, _) => RefreshStatus();
        _refreshTimer.Start();

        RefreshStatus();

        // ─── Tray Icon ───
        trayIcon = new NotifyIcon
        {
            Icon = Icon,
            Text = "BoostModeScheduler",
            Visible = true
        };
        trayIcon.DoubleClick += (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); };
        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("显示窗口", null, (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); });
        trayMenu.Items.Add("退出", null, (_, _) => { _realClosing = true; Close(); });
        trayIcon.ContextMenuStrip = trayMenu;

        Resize += (_, _) =>
        {
            if (_config.CloseToTray && WindowState == FormWindowState.Minimized)
                Hide();
        };
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_config.CloseToTray && !_realClosing && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        trayIcon?.Dispose();
        base.OnFormClosing(e);
    }

    private Dictionary<int, string> LoadModeNames()
    {
        var fallback = new Dictionary<int, string>
        {
            { 0, "Disabled" }, { 1, "Enabled" }, { 2, "Aggressive" },
            { 3, "Efficient" }, { 4, "Aggressive Efficient" },
            { 5, "Performance Preferred" }, { 6, "Efficient Performance Preferred" }
        };

        try
        {
            var switcher = new PowerModeSwitcher();
            var names = switcher.ReadModeNames();
            if (names.Count > 0) return names;
        }
        catch { }

        return fallback;
    }

    private void BuildUI()
    {
        int y = 12;

        // ─── Service Control ───
        grpService = new GroupBox { Text = "服务控制", Top = y, Height = 118 };
        lblServiceStatus = new Label { Text = "状态: 检测中...", Left = 12, Top = 22, Width = 300, Height = 20 };
        lblServiceStatus.Font = new Font(lblServiceStatus.Font, FontStyle.Bold);
        grpService.Controls.Add(lblServiceStatus);

        btnStart = new Button { Text = "启动", Top = 18, Width = 70, Height = 28 };
        btnStart.Click += (_, _) => ControlService("start");
        grpService.Controls.Add(btnStart);

        btnStop = new Button { Text = "停止", Top = 18, Width = 70, Height = 28 };
        btnStop.Click += (_, _) => ControlService("stop");
        grpService.Controls.Add(btnStop);

        btnRestart = new Button { Text = "重启", Top = 18, Width = 70, Height = 28 };
        btnRestart.Click += (_, _) => ControlService("restart");
        grpService.Controls.Add(btnRestart);

        chkAutoStart = new CheckBox { Text = "开机自启", Left = 12, Top = 52, Width = 120, Height = 22 };
        chkAutoStart.CheckedChanged += (_, _) =>
        {
            try { SetServiceAutoStart(chkAutoStart.Checked); }
            catch (Exception ex) { ShowError($"设置失败: {ex.Message}"); chkAutoStart.Checked = !chkAutoStart.Checked; }
        };
        grpService.Controls.Add(chkAutoStart);

        chkCloseToTray = new CheckBox { Text = "关闭时最小化到托盘", Left = 140, Top = 52, Width = 180, Height = 22 };
        chkCloseToTray.Checked = _config.CloseToTray;
        chkCloseToTray.CheckedChanged += (_, _) =>
        {
            _config.CloseToTray = chkCloseToTray.Checked;
            ConfigManager.Save(_config);
        };
        grpService.Controls.Add(chkCloseToTray);

        chkEnableNotify = new CheckBox { Text = "模式切换时显示通知", Left = 12, Top = 78, Width = 180, Height = 22 };
        chkEnableNotify.Checked = _config.EnableSwitchNotification;
        chkEnableNotify.CheckedChanged += (_, _) =>
        {
            _config.EnableSwitchNotification = chkEnableNotify.Checked;
            ConfigManager.Save(_config);
        };
        grpService.Controls.Add(chkEnableNotify);

        Controls.Add(grpService);
        y += 128;

        // ─── Current Status ───
        grpStatus = new GroupBox { Text = "当前状态", Top = y, Height = 110 };
        lblCurrentMode = new Label { Text = "当前模式: --", Left = 12, Top = 22, Width = 300, Height = 18 };
        lblCurrentMode.Font = new Font(lblCurrentMode.Font, FontStyle.Bold);
        grpStatus.Controls.Add(lblCurrentMode);

        lblCpuUsage = new Label { Text = "CPU 占用: --", Top = 22, Width = 200, Height = 18 };
        grpStatus.Controls.Add(lblCpuUsage);

        lblGameProcesses = new Label { Text = "检测到游戏进程: 无", Left = 12, Top = 44, Width = 610, Height = 18, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        grpStatus.Controls.Add(lblGameProcesses);

        lblLastSwitch = new Label { Text = "上次切换原因: --", Left = 12, Top = 64, Width = 610, Height = 18, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        grpStatus.Controls.Add(lblLastSwitch);

        lblLastAutoSwitch = new Label { Text = "上次自动切换: --", Left = 12, Top = 84, Width = 610, Height = 18, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        grpStatus.Controls.Add(lblLastAutoSwitch);
        Controls.Add(grpStatus);
        y += 120;

        // ─── Settings ───
        grpSettings = new GroupBox { Text = "检测设置", Top = y, Height = 250 };

        AddLabeledControl(grpSettings, "CPU 负载阈值 (≥此值切负载):", 8, 22);
        numLoadThreshold = new NumericUpDown { Top = 20, Width = 65, Minimum = 1, Maximum = 100, Value = _config.CpuLoadThreshold };
        grpSettings.Controls.Add(numLoadThreshold);
        new Label { Text = "%", Top = 23, Height = 18 }.Let(l => grpSettings.Controls.Add(l));

        AddLabeledControl(grpSettings, "CPU 空闲阈值 (<此值切空闲):", 8, 52);
        numIdleThreshold = new NumericUpDown { Top = 50, Width = 65, Minimum = 0, Maximum = 100, Value = _config.CpuIdleThreshold };
        grpSettings.Controls.Add(numIdleThreshold);
        new Label { Text = "%", Top = 53, Height = 18 }.Let(l => grpSettings.Controls.Add(l));

        AddLabeledControl(grpSettings, "升载确认时间:", 8, 82);
        numLoadHold = new NumericUpDown { Top = 80, Width = 65, Minimum = 1, Maximum = 120, Value = _config.LoadHoldSeconds };
        grpSettings.Controls.Add(numLoadHold);
        new Label { Text = "秒", Top = 83, Height = 18 }.Let(l => grpSettings.Controls.Add(l));

        AddLabeledControl(grpSettings, "降载确认时间:", 8, 112);
        numIdleHold = new NumericUpDown { Top = 110, Width = 65, Minimum = 1, Maximum = 300, Value = _config.IdleHoldSeconds };
        grpSettings.Controls.Add(numIdleHold);
        new Label { Text = "秒", Top = 113, Height = 18 }.Let(l => grpSettings.Controls.Add(l));

        AddLabeledControl(grpSettings, "轮询间隔:", 8, 142);
        numPollInterval = new NumericUpDown { Top = 140, Width = 65, Minimum = 500, Maximum = 30000, Increment = 500, Value = _config.PollIntervalMs };
        grpSettings.Controls.Add(numPollInterval);
        new Label { Text = "毫秒", Top = 143, Height = 18 }.Let(l => grpSettings.Controls.Add(l));

        // AC/DC column headers
        new Label { Text = "AC", Width = 60, Height = 18, Font = new Font(Font, FontStyle.Underline), TextAlign = ContentAlignment.MiddleCenter }.Let(l => grpSettings.Controls.Add(l));
        new Label { Text = "DC", Width = 60, Height = 18, Font = new Font(Font, FontStyle.Underline), TextAlign = ContentAlignment.MiddleCenter }.Let(l => grpSettings.Controls.Add(l));

        // Mode labels (narrow width to avoid combo overlap)
        grpSettings.Controls.Add(new Label { Text = "空闲模式:", Left = 8, Top = 192, Width = 65, Height = 18, TextAlign = ContentAlignment.MiddleRight });
        grpSettings.Controls.Add(new Label { Text = "负载模式:", Left = 8, Top = 218, Width = 65, Height = 18, TextAlign = ContentAlignment.MiddleRight });

        // AC/DC mode selectors
        cmbIdleModeAc = CreateModeCombo(_config.IdleModeValueAc, 60);
        grpSettings.Controls.Add(cmbIdleModeAc);
        cmbIdleModeDc = CreateModeCombo(_config.IdleModeValueDc, 60);
        grpSettings.Controls.Add(cmbIdleModeDc);

        cmbLoadModeAc = CreateModeCombo(_config.LoadModeValueAc, 60);
        grpSettings.Controls.Add(cmbLoadModeAc);
        cmbLoadModeDc = CreateModeCombo(_config.LoadModeValueDc, 60);
        grpSettings.Controls.Add(cmbLoadModeDc);

        Controls.Add(grpSettings);

        // ─── Whitelist ───
        grpWhitelist = new GroupBox { Text = "进程白名单", Top = y, Height = 250 };

        lstProcesses = new ListBox { Top = 18, Height = 140 };
        foreach (var p in _config.ProcessWhitelist) lstProcesses.Items.Add(p);
        grpWhitelist.Controls.Add(lstProcesses);

        txtNewProcess = new TextBox { Top = 164, Height = 22, PlaceholderText = "输入进程名 (如 valorant.exe)" };
        grpWhitelist.Controls.Add(txtNewProcess);

        btnAdd = new Button { Text = "添加", Top = 163, Width = 76, Height = 24 };
        btnAdd.Click += (_, _) =>
        {
            var name = txtNewProcess.Text.Trim();
            if (!string.IsNullOrEmpty(name) && !lstProcesses.Items.Contains(name))
            {
                lstProcesses.Items.Add(name);
                txtNewProcess.Clear();
            }
        };
        grpWhitelist.Controls.Add(btnAdd);

        btnRemove = new Button { Text = "删除选中", Top = 192, Height = 24 };
        btnRemove.Click += (_, _) =>
        {
            var selected = lstProcesses.SelectedItems.Cast<string>().ToList();
            foreach (var item in selected) lstProcesses.Items.Remove(item);
        };
        grpWhitelist.Controls.Add(btnRemove);

        Controls.Add(grpWhitelist);
        y += 260;

        // ─── Manual Override ───
        grpOverride = new GroupBox { Text = "手动切换模式 (AC=电源, DC=电池)", Top = y, Height = 56 };

        new Label { Text = "AC:", Width = 25, Height = 20, TextAlign = ContentAlignment.MiddleRight }.Let(l => grpOverride.Controls.Add(l));
        cmbManualAc = CreateModeCombo(_config.LoadModeValueAc, 120);
        grpOverride.Controls.Add(cmbManualAc);

        new Label { Text = "DC:", Width = 25, Height = 20, TextAlign = ContentAlignment.MiddleRight }.Let(l => grpOverride.Controls.Add(l));
        cmbManualDc = CreateModeCombo(_config.LoadModeValueDc, 120);
        grpOverride.Controls.Add(cmbManualDc);

        btnApplyManual = new Button { Text = "立即切换", Width = 120, Height = 28 };
        btnApplyManual.BackColor = Color.LightCoral;
        btnApplyManual.Click += (_, _) => ForceManualMode();
        grpOverride.Controls.Add(btnApplyManual);

        Controls.Add(grpOverride);
        y += 66;

        // ─── Log ───
        grpLog = new GroupBox { Text = "日志管理", Top = y, Height = 56 };

        btnOpenLogFolder = new Button { Text = "打开日志文件夹", Left = 12, Top = 18, Width = 120, Height = 28 };
        btnOpenLogFolder.Click += (_, _) =>
        {
            try { Process.Start("explorer.exe", ConfigManager.GetLogDir()); }
            catch (Exception ex) { ShowError($"无法打开: {ex.Message}"); }
        };
        grpLog.Controls.Add(btnOpenLogFolder);

        btnViewLog = new Button { Text = "查看完整日志", Left = 268, Top = 18, Width = 150, Height = 28 };
        btnViewLog.Click += (_, _) => ShowLogViewer();
        grpLog.Controls.Add(btnViewLog);

        Controls.Add(grpLog);
        y += 66;

        // ─── Switch Log (inline) ───
        grpSwitchLog = new GroupBox { Text = "切换记录", Top = y, Height = 120 };
        txtSwitchLog = new TextBox
        {
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            WordWrap = true, Font = new Font("Microsoft YaHei", 9),
            Text = "暂无切换记录"
        };
        grpSwitchLog.Controls.Add(txtSwitchLog);
        Controls.Add(grpSwitchLog);
        y += 130;

        // ─── Bottom Buttons ───
        btnEditConfig = new Button { Text = "用记事本编辑 config", Top = y, Width = 150, Height = 36 };
        btnEditConfig.Click += (_, _) =>
        {
            try { Process.Start("notepad.exe", ConfigManager.GetConfigPath()); }
            catch (Exception ex) { ShowError($"无法打开: {ex.Message}"); }
        };
        Controls.Add(btnEditConfig);

        btnSave = new Button { Text = "保存配置并重启服务", Top = y, Width = 160, Height = 36 };
        btnSave.BackColor = Color.LightGreen;
        btnSave.Click += (_, _) => SaveAndRestart();
        Controls.Add(btnSave);

        btnRefresh = new Button { Text = "刷新", Top = y, Width = 140, Height = 36 };
        btnRefresh.Click += (_, _) => RefreshStatus();
        Controls.Add(btnRefresh);

        Height = y + 90;
    }

    private void LayoutControls()
    {
        int cw = ClientSize.Width;
        int x = 12;
        int w = cw - 24;          // full width usable
        int halfW = (w - 12) / 2;  // left/right column width
        int rightX = x + halfW + 12;

        // Full-width groupboxes
        foreach (var g in new[] { grpService, grpStatus, grpOverride, grpLog, grpSwitchLog })
        {
            if (g != null) { g.Left = x; g.Width = w; }
        }

        // Two-column
        grpSettings.Left = x;
        grpSettings.Width = halfW;

        grpWhitelist.Left = rightX;
        grpWhitelist.Width = w - halfW - 12;

        // ── grpSettings internal ──
        int labelWidth = Math.Max(110, halfW - 128);
        foreach (Control c in grpSettings.Controls)
        {
            if (c is Label lbl && lbl.Left == 8 && lbl.Top >= 20 && lbl.Top <= 145)
            {
                lbl.Left = 8;
                lbl.Width = labelWidth;
            }
            else if (c is NumericUpDown nud && nud.Top >= 20 && nud.Top <= 145)
            {
                nud.Left = halfW - 86;
            }
            else if (c is Label unit && unit.Top >= 23 && unit.Top <= 145 && (unit.Text == "%" || unit.Text == "秒" || unit.Text == "毫秒"))
            {
                unit.Left = halfW - 24;
                unit.Width = 18;
                unit.TextAlign = ContentAlignment.MiddleLeft;
            }
            else if (c is ComboBox)
            {
                // reposition below
            }
        }

        // AC/DC column headers — scale with panel width, right after mode labels
        int modeSectionLeft = halfW > 160 ? 81 : halfW - 79;
        int comboW = Math.Max(50, (halfW - modeSectionLeft - 16) / 2);
        if (comboW > 90) comboW = 90;
        int acLeft = modeSectionLeft;
        int dcLeft = acLeft + comboW + 6;

        foreach (Control c in grpSettings.Controls)
        {
            if (c is Label lbl && lbl.Font.Underline && (lbl.Text == "AC" || lbl.Text == "DC"))
            {
                lbl.Left = lbl.Text == "AC" ? acLeft : dcLeft;
                lbl.Top = 168;
                lbl.Width = comboW;
                lbl.TextAlign = ContentAlignment.MiddleCenter;
            }
        }

        // Reposition the 4 combos — aligned with their headers
        cmbIdleModeAc.Left = acLeft; cmbIdleModeAc.Top = 190; cmbIdleModeAc.Width = comboW;
        cmbIdleModeDc.Left = dcLeft; cmbIdleModeDc.Top = 190; cmbIdleModeDc.Width = comboW;
        cmbLoadModeAc.Left = acLeft; cmbLoadModeAc.Top = 216; cmbLoadModeAc.Width = comboW;
        cmbLoadModeDc.Left = dcLeft; cmbLoadModeDc.Top = 216; cmbLoadModeDc.Width = comboW;

        // ── Whiltelist internal ──
        int ww = grpWhitelist.ClientSize.Width;
        lstProcesses.Left = 8;
        lstProcesses.Width = ww - 16;
        txtNewProcess.Left = 8;
        txtNewProcess.Width = ww - 96;
        btnAdd.Left = ww - 86;
        btnRemove.Left = 8;
        btnRemove.Width = ww - 16;

        // ── Service buttons ──
        btnStart.Left = w - 320;
        btnStop.Left = w - 244;
        btnRestart.Left = w - 168;

        // ── Service checkboxes ──
        chkAutoStart.Left = 12;
        chkCloseToTray.Left = Math.Max(130, w - 280);
        chkEnableNotify.Left = Math.Max(300, w - 80);

        // ── lblCpuUsage ──
        lblCpuUsage.Left = w - 360;

        // ── grpSwitchLog internal ──
        txtSwitchLog.Left = 8;
        txtSwitchLog.Top = 18;
        txtSwitchLog.Width = grpSwitchLog.ClientSize.Width - 16;
        txtSwitchLog.Height = grpSwitchLog.ClientSize.Height - 28;

        // ── Manual override ──
        int overrideWidth = grpOverride.ClientSize.Width;
        int labelW = 25;
        int comboW2 = Math.Max(100, (overrideWidth - 200) / 3);
        if (comboW2 > 160) comboW2 = 160;
        int cx = 10;
        int topY = 18;

        foreach (Control c in grpOverride.Controls)
        {
            if (c is Label lbl && lbl.Text == "AC:")
            {
                lbl.Left = cx; lbl.Width = labelW; lbl.Top = topY; cx += labelW + 2;
            }
            else if (c == cmbManualAc)
            {
                cmbManualAc.Left = cx; cmbManualAc.Width = comboW2; cmbManualAc.Top = topY - 2; cx += comboW2 + 6;
            }
            else if (c is Label lbl2 && lbl2.Text == "DC:")
            {
                lbl2.Left = cx; lbl2.Width = labelW; lbl2.Top = topY; cx += labelW + 2;
            }
            else if (c == cmbManualDc)
            {
                cmbManualDc.Left = cx; cmbManualDc.Width = comboW2; cmbManualDc.Top = topY - 2; cx += comboW2 + 6;
            }
            else if (c == btnApplyManual)
            {
                btnApplyManual.Left = Math.Max(cx + 6, overrideWidth - 136);
                btnApplyManual.Top = topY - 2;
            }
        }

        // ── Bottom buttons ──
        btnEditConfig.Left = x;
        btnSave.Left = w - 300;
        btnRefresh.Left = w - 130;
    }

    private ComboBox CreateModeCombo(int selected, int width)
    {
        var cmb = new ComboBox { Width = width, DropDownStyle = ComboBoxStyle.DropDownList };

        var items = _modeNames
            .OrderBy(kv => kv.Key)
            .Select(kv => (object)new KeyValuePair<int, string>(kv.Key, $"{kv.Key} - {kv.Value}"))
            .ToArray();

        cmb.Items.AddRange(items);
        cmb.DisplayMember = "Value";
        cmb.ValueMember = "Key";

        for (int i = 0; i < cmb.Items.Count; i++)
        {
            var item = (KeyValuePair<int, string>)cmb.Items[i]!;
            if (item.Key == selected)
            {
                cmb.SelectedIndex = i;
                break;
            }
        }
        if (cmb.SelectedIndex < 0) cmb.SelectedIndex = 0;
        return cmb;
    }

    private static int GetModeKey(ComboBox cmb)
    {
        var kv = cmb.SelectedItem;
        return kv is KeyValuePair<int, string> pair ? pair.Key : 0;
    }

    private static void AddLabeledControl(GroupBox parent, string text, int x, int y)
    {
        parent.Controls.Add(new Label { Text = text, Left = x, Top = y, Width = 200, Height = 18, TextAlign = ContentAlignment.MiddleRight });
    }

    private void RefreshStatus()
    {
        try
        {
            _service ??= new ServiceController("BoostModeSvc");
            _service.Refresh();
            var status = _service.Status;
            lblServiceStatus.Text = $"状态: {(status == ServiceControllerStatus.Running ? "● 运行中" : "○ 已停止")}";
            lblServiceStatus.ForeColor = status == ServiceControllerStatus.Running ? Color.Green : Color.Red;
            btnStart.Enabled = status != ServiceControllerStatus.Running;
            btnStop.Enabled = status == ServiceControllerStatus.Running;

            bool autoStart = IsServiceAutoStart();
            chkAutoStart.CheckedChanged -= (_, _) => { };
            chkAutoStart.Checked = autoStart;
            chkAutoStart.CheckedChanged += (_, _) =>
            {
                try { SetServiceAutoStart(chkAutoStart.Checked); }
                catch (Exception ex) { ShowError($"设置失败: {ex.Message}"); chkAutoStart.Checked = !chkAutoStart.Checked; }
            };
        }
        catch (InvalidOperationException)
        {
            lblServiceStatus.Text = "状态: 服务未安装";
            lblServiceStatus.ForeColor = Color.Gray;
            btnStart.Enabled = btnStop.Enabled = false;
        }
        catch (Exception ex)
        {
            lblServiceStatus.Text = $"状态: 错误 - {ex.Message}";
            lblServiceStatus.ForeColor = Color.Red;
        }

        try
        {
            var statusInfo = ConfigManager.LoadStatus();
            if (statusInfo != null)
            {
                var acName = _modeNames.GetValueOrDefault(statusInfo.CurrentModeValueAc, $"?({statusInfo.CurrentModeValueAc})");
                var dcName = _modeNames.GetValueOrDefault(statusInfo.CurrentModeValueDc, $"?({statusInfo.CurrentModeValueDc})");
                lblCurrentMode.Text = $"当前模式: {statusInfo.CurrentMode} (AC={acName}, DC={dcName})";
                lblCpuUsage.Text = $"CPU 占用: {statusInfo.CpuUsage:F1}%";
                lblGameProcesses.Text = $"检测到游戏进程: {(statusInfo.GameProcesses.Count > 0 ? string.Join(", ", statusInfo.GameProcesses) : "无")}";
            }
        }
        catch
        {
            lblCurrentMode.Text = "当前模式: 读取失败";
            lblCpuUsage.Text = "CPU 占用: --";
        }

        try
        {
            var entries = ParseSwitchLogs();
            if (entries.Count > 0)
            {
                var latest = entries[0];
                string modeLabel = latest.Mode switch { "LOAD" => "负载模式", "IDLE" => "空闲模式", "MANUAL" => "手动切换", "SERVICE" => "服务事件", _ => latest.Mode };
                lblLastSwitch.Text = $"上次切换原因: [{latest.Time:HH:mm:ss}] {modeLabel}  {latest.Reason}";

                var lastAuto = entries.FirstOrDefault(e => e.Mode is "LOAD" or "IDLE");
                if (lastAuto.Mode != null)
                {
                    string autoLabel = lastAuto.Mode switch { "LOAD" => "负载模式", "IDLE" => "空闲模式", _ => lastAuto.Mode };
                    lblLastAutoSwitch.Text = $"上次自动切换: [{lastAuto.Time:HH:mm:ss}] {autoLabel}  {lastAuto.Reason}";
                }
                else
                {
                    lblLastAutoSwitch.Text = "上次自动切换: 暂无";
                }

                // ─── Toast notification on new switch ───
                if (_config.EnableSwitchNotification && latest.Time > _lastNotifiedSwitch && latest.Mode is "LOAD" or "IDLE" or "MANUAL")
                {
                    _lastNotifiedSwitch = latest.Time;
                    string acName = _modeNames.GetValueOrDefault(latest.AcValue, latest.AcValue.ToString());
                    string dcName = _modeNames.GetValueOrDefault(latest.DcValue, latest.DcValue.ToString());
                    string title = $"BoostMode: {modeLabel}";
                    string body = $"AC={acName}  DC={dcName}\n{latest.Reason}";
                    trayIcon.BalloonTipTitle = title;
                    trayIcon.BalloonTipText = body;
                    trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                    trayIcon.ShowBalloonTip(5000);
                }
            }
            else
            {
                lblLastSwitch.Text = "上次切换原因: 暂无记录";
                lblLastAutoSwitch.Text = "上次自动切换: 暂无";
            }
        }
        catch
        {
            lblLastSwitch.Text = "上次切换原因: --";
            lblLastAutoSwitch.Text = "上次自动切换: --";
        }

        RefreshSwitchLog();
    }

    private void RefreshSwitchLog()
    {
        var entries = ParseSwitchLogs();
        if (entries.Count == 0)
        {
            txtSwitchLog.Text = "暂无切换记录";
            return;
        }

        var modeNames = _modeNames;
        var lines = entries.Select(e =>
        {
            string modeLabel = e.Mode switch { "LOAD" => "负载模式", "IDLE" => "空闲模式", "MANUAL" => "手动切换", "SERVICE" => "服务事件", _ => e.Mode };
            string entry;
            if (e.Mode == "SERVICE")
            {
                entry = $"[{e.Time:HH:mm:ss}]  {modeLabel}";
                if (!string.IsNullOrEmpty(e.Reason)) entry += $"  原因: {e.Reason}";
            }
            else
            {
                string acName = modeNames.GetValueOrDefault(e.AcValue, e.AcValue.ToString());
                string dcName = modeNames.GetValueOrDefault(e.DcValue, e.DcValue.ToString());
                entry = $"[{e.Time:HH:mm:ss}]  {modeLabel}  AC={acName}  DC={dcName}";
                if (!string.IsNullOrEmpty(e.Reason)) entry += $"  原因: {e.Reason}";
            }
            return entry;
        });

        txtSwitchLog.Text = string.Join(Environment.NewLine, lines);
    }

    private struct SwitchEntry
    {
        public DateTime Time;
        public string Mode;
        public int AcValue;
        public int DcValue;
        public string Reason;
    }

    private List<SwitchEntry> ParseSwitchLogs()
    {
        var result = new List<SwitchEntry>();
        try
        {
            var logDir = ConfigManager.GetLogDir();
            if (!Directory.Exists(logDir)) return result;

            var logFiles = Directory.GetFiles(logDir, "*.log").OrderByDescending(f => f).Take(5).ToArray();
            if (logFiles.Length == 0) return result;

            var lines = new List<string>();
            foreach (var file in logFiles)
            {
                try { lines.AddRange(File.ReadAllLines(file)); }
                catch { }
            }

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (!line.Contains("SWITCH:")) continue;

                var tsMatch = System.Text.RegularExpressions.Regex.Match(line, @"(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}|\d{2}:\d{2}:\d{2})");
                string ts = tsMatch.Success ? tsMatch.Groups[1].Value : "";

                var swMatch = System.Text.RegularExpressions.Regex.Match(line, @"-> (\w+)(?: \(AC=(\d+), DC=(\d+)\))?");
                if (!swMatch.Success) continue;
                string mode = swMatch.Groups[1].Value;
                int acVal = 0, dcVal = 0;
                if (swMatch.Groups[2].Success)
                    int.TryParse(swMatch.Groups[2].Value, out acVal);
                if (swMatch.Groups[3].Success)
                    int.TryParse(swMatch.Groups[3].Value, out dcVal);

                string reason = "";
                if (i + 1 < lines.Count)
                {
                    var rMatch = System.Text.RegularExpressions.Regex.Match(lines[i + 1], @"Reason: (.+)");
                    if (rMatch.Success) reason = rMatch.Groups[1].Value;
                }

                DateTime.TryParse(ts, out DateTime time);

                result.Add(new SwitchEntry { Time = time, Mode = mode, AcValue = acVal, DcValue = dcVal, Reason = reason });
            }
        }
        catch { }

        result.Reverse();
        return result;
    }

    private void SaveAndRestart()
    {
        try
        {
            _config.CpuLoadThreshold = (int)numLoadThreshold.Value;
            _config.CpuIdleThreshold = (int)numIdleThreshold.Value;
            _config.LoadHoldSeconds = (int)numLoadHold.Value;
            _config.IdleHoldSeconds = (int)numIdleHold.Value;
            _config.PollIntervalMs = (int)numPollInterval.Value;
            _config.IdleModeValueAc = GetModeKey(cmbIdleModeAc);
            _config.IdleModeValueDc = GetModeKey(cmbIdleModeDc);
            _config.LoadModeValueAc = GetModeKey(cmbLoadModeAc);
            _config.LoadModeValueDc = GetModeKey(cmbLoadModeDc);
            _config.ProcessWhitelist = lstProcesses.Items.Cast<string>().ToList();

            if (!ConfigManager.Save(_config))
            {
                ShowError("保存配置失败");
                return;
            }

            ControlService("restart");
            MessageBox.Show("配置已保存并重启服务", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowError($"保存失败: {ex.Message}");
        }
    }

    private void ControlService(string action)
    {
        try
        {
            _service ??= new ServiceController("BoostModeSvc");

            switch (action)
            {
                case "start":
                    _service.Refresh();
                    Logger.Info("SWITCH: -> SERVICE START");
                    Logger.Info("  Reason: 用户在配置工具手动启动");
                    _service.Start();
                    _service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                    break;
                case "stop":
                    _service.Refresh();
                    Logger.Info("SWITCH: -> SERVICE STOP");
                    Logger.Info("  Reason: 用户在配置工具手动停止");
                    _service.Stop();
                    _service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                    break;
                case "restart":
                    _service.Refresh();
                    Logger.Info("SWITCH: -> SERVICE STOP");
                    Logger.Info("  Reason: 配置工具准备重启服务");
                    if (_service.Status == ServiceControllerStatus.Running)
                    {
                        _service.Stop();
                        _service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                    }
                    Logger.Info("SWITCH: -> SERVICE START");
                    Logger.Info("  Reason: 配置工具重启服务");
                    _service.Start();
                    _service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                    break;
            }
        }
        catch (InvalidOperationException)
        {
            ShowError("服务未安装。请先运行 install-service.bat 安装服务。");
        }
        catch (Exception ex)
        {
            ShowError($"操作失败: {ex.Message}");
        }
        RefreshStatus();
    }

    private void ForceManualMode()
    {
        if (cmbManualAc.SelectedItem == null || cmbManualDc.SelectedItem == null)
        {
            ShowError("请先选择 AC 和 DC 模式");
            return;
        }

        int acVal = GetModeKey(cmbManualAc);
        string acName = _modeNames.GetValueOrDefault(acVal, "?");
        int dcVal = GetModeKey(cmbManualDc);
        string dcName = _modeNames.GetValueOrDefault(dcVal, "?");

        try
        {
            var switcher = new PowerModeSwitcher();
            if (switcher.SwitchToSeparate(acVal, dcVal, out string output))
            {
                MessageBox.Show($"已强制切换\nAC={acName}\nDC={dcName}\n\n结果: {output}",
                    "手动切换", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Logger.Info($"SWITCH: -> MANUAL (AC={acVal}, DC={dcVal})");
                Logger.Info($"  Reason: 用户手动切换");
                Logger.Info($"  Result: {output}");
            }
            else
            {
                ShowError($"切换失败: {output}");
            }
        RefreshStatus();
        }
        catch (Exception ex)
        {
            ShowError($"切换失败: {ex.Message}");
        }
    }

    private static bool IsServiceAutoStart()
    {
        try
        {
            var psi = new ProcessStartInfo("sc.exe", "qc BoostModeSvc")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(3000);
            return output.Contains("AUTO_START");
        }
        catch { return false; }
    }

    private static void SetServiceAutoStart(bool enable)
    {
        var mode = enable ? "auto" : "demand";
        var psi = new ProcessStartInfo("sc.exe", $"config BoostModeSvc start={mode}")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit(5000);
    }

    private void ShowLogViewer()
    {
        var logDir = ConfigManager.GetLogDir();
        if (!Directory.Exists(logDir))
        {
            ShowError("日志目录不存在，服务可能尚未写日志");
            return;
        }

        var logFiles = Directory.GetFiles(logDir, "*.log").OrderByDescending(f => f).Take(3).ToArray();
        if (logFiles.Length == 0)
        {
            ShowError("没有找到日志文件");
            return;
        }

        var logContent = new System.Text.StringBuilder();
        logContent.AppendLine($"=== 最近日志 (来自 {logFiles.Length} 个文件) ===");
        logContent.AppendLine();

        foreach (var file in logFiles)
        {
            try
            {
                var lines = File.ReadAllLines(file);
                var tail = lines.Length > 60 ? lines[^60..] : lines;
                logContent.AppendLine($"--- {Path.GetFileName(file)} ({tail.Length}/{lines.Length} 行) ---");
                logContent.AppendLine(string.Join(Environment.NewLine, tail));
                logContent.AppendLine();
            }
            catch { }
        }

        var viewer = new Form
        {
            Text = "日志查看器",
            Size = new Size(800, 600),
            StartPosition = FormStartPosition.CenterParent
        };

        var textBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font("Consolas", 10),
            Dock = DockStyle.Fill,
            Text = logContent.ToString()
        };
        viewer.Controls.Add(textBox);

        var closeBtn = new Button
        {
            Text = "关闭",
            Dock = DockStyle.Bottom,
            Height = 36
        };
        closeBtn.Click += (_, _) => viewer.Close();
        viewer.Controls.Add(closeBtn);

        viewer.ShowDialog();
    }

    private void ShowError(string message)
    {
        MessageBox.Show(message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

public static class Extensions
{
    public static T Let<T>(this T obj, Action<T> action)
    {
        action(obj);
        return obj;
    }
}
