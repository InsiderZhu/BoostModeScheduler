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
    private Label lblServiceStatus = null!, lblCurrentMode = null!, lblCpuUsage = null!;
    private Label lblGameProcesses = null!, lblLastSwitch = null!;
    private Button btnStart = null!, btnStop = null!, btnRestart = null!;
    private NumericUpDown numLoadThreshold = null!, numIdleThreshold = null!;
    private NumericUpDown numPollInterval = null!, numLoadHold = null!, numIdleHold = null!;
    private ComboBox cmbIdleModeAc = null!, cmbIdleModeDc = null!;
    private ComboBox cmbLoadModeAc = null!, cmbLoadModeDc = null!;
    private ListBox lstProcesses = null!;
    private TextBox txtNewProcess = null!;
    private Button btnAdd = null!, btnRemove = null!;
    private ComboBox cmbManualAc = null!, cmbManualDc = null!;
    private Button btnApplyManual = null!;
    private Button btnSave = null!, btnRefresh = null!;
    private Button btnEditConfig = null!, btnOpenLogFolder = null!, btnViewLog = null!;

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
        grpService = new GroupBox { Text = "服务控制", Top = y, Height = 70 };
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
        Controls.Add(grpService);
        y += 80;

        // ─── Current Status ───
        grpStatus = new GroupBox { Text = "当前状态", Top = y, Height = 90 };
        lblCurrentMode = new Label { Text = "当前模式: --", Left = 12, Top = 22, Width = 300, Height = 18 };
        lblCurrentMode.Font = new Font(lblCurrentMode.Font, FontStyle.Bold);
        grpStatus.Controls.Add(lblCurrentMode);

        lblCpuUsage = new Label { Text = "CPU 占用: --", Top = 22, Width = 200, Height = 18 };
        grpStatus.Controls.Add(lblCpuUsage);

        lblGameProcesses = new Label { Text = "检测到游戏进程: 无", Left = 12, Top = 44, Width = 610, Height = 18, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        grpStatus.Controls.Add(lblGameProcesses);

        lblLastSwitch = new Label { Text = "上次切换原因: --", Left = 12, Top = 64, Width = 610, Height = 18, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        grpStatus.Controls.Add(lblLastSwitch);
        Controls.Add(grpStatus);
        y += 100;

        // ─── Settings ───
        grpSettings = new GroupBox { Text = "检测设置", Top = y, Height = 250 };

        AddLabeledControl(grpSettings, "CPU 负载阈值 (≥此值切负载):", 8, 22);
        numLoadThreshold = new NumericUpDown { Top = 20, Width = 65, Minimum = 1, Maximum = 100, Value = _config.CpuLoadThreshold };
        grpSettings.Controls.Add(numLoadThreshold);

        AddLabeledControl(grpSettings, "CPU 空闲阈值 (<此值切空闲):", 8, 52);
        numIdleThreshold = new NumericUpDown { Top = 50, Width = 65, Minimum = 0, Maximum = 100, Value = _config.CpuIdleThreshold };
        grpSettings.Controls.Add(numIdleThreshold);

        AddLabeledControl(grpSettings, "升载确认时间:", 8, 82);
        numLoadHold = new NumericUpDown { Top = 80, Width = 65, Minimum = 1, Maximum = 120, Value = _config.LoadHoldSeconds };
        grpSettings.Controls.Add(numLoadHold);

        AddLabeledControl(grpSettings, "降载确认时间:", 8, 112);
        numIdleHold = new NumericUpDown { Top = 110, Width = 65, Minimum = 1, Maximum = 300, Value = _config.IdleHoldSeconds };
        grpSettings.Controls.Add(numIdleHold);

        AddLabeledControl(grpSettings, "轮询间隔:", 8, 142);
        numPollInterval = new NumericUpDown { Top = 140, Width = 65, Minimum = 500, Maximum = 30000, Increment = 500, Value = _config.PollIntervalMs };
        grpSettings.Controls.Add(numPollInterval);

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

        cmbManualAc = CreateModeCombo(_config.LoadModeValueAc, 130);
        cmbManualAc.Width = 150;
        grpOverride.Controls.Add(cmbManualAc);

        cmbManualDc = CreateModeCombo(_config.LoadModeValueDc, 130);
        cmbManualDc.Width = 150;
        grpOverride.Controls.Add(cmbManualDc);

        btnApplyManual = new Button { Text = "立即切换", Top = 18, Width = 130, Height = 28 };
        btnApplyManual.BackColor = Color.LightCoral;
        btnApplyManual.Click += (_, _) => ForceManualMode();
        grpOverride.Controls.Add(btnApplyManual);

        Controls.Add(grpOverride);
        y += 66;

        // ─── Log ───
        grpLog = new GroupBox { Text = "日志管理", Top = y, Height = 56 };

        btnOpenLogFolder = new Button { Text = "打开日志文件夹", Left = 12, Top = 18, Width = 150, Height = 28 };
        btnOpenLogFolder.Click += (_, _) =>
        {
            try { Process.Start("explorer.exe", ConfigManager.GetLogDir()); }
            catch (Exception ex) { ShowError($"无法打开: {ex.Message}"); }
        };
        grpLog.Controls.Add(btnOpenLogFolder);

        btnViewLog = new Button { Text = "查看最近日志", Left = 172, Top = 18, Width = 150, Height = 28 };
        btnViewLog.Click += (_, _) => ShowLogViewer();
        grpLog.Controls.Add(btnViewLog);

        Controls.Add(grpLog);
        y += 66;

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
        foreach (var g in new[] { grpService, grpStatus, grpOverride, grpLog })
        {
            if (g != null) { g.Left = x; g.Width = w; }
        }

        // Two-column
        grpSettings.Left = x;
        grpSettings.Width = halfW;

        grpWhitelist.Left = rightX;
        grpWhitelist.Width = w - halfW - 12;

        // ── grpSettings internal ──
        int upDownLeft = halfW - 100;
        int labelWidth = halfW - 110;
        foreach (Control c in grpSettings.Controls)
        {
            if (c is Label lbl && lbl.Left == 8 && lbl.Width == 200)
            {
                c.Left = 8;
                c.Width = Math.Max(labelWidth, 80);
            }
            else if (c is NumericUpDown nud && nud.Top >= 20 && nud.Top <= 145)
            {
                nud.Left = halfW - 90;
            }
            else if (c is ComboBox)
            {
                // reposition below
            }
        }

        // AC/DC column headers — positioned right after mode labels, proportional to panel width
        int acLeft = 81;                    // label (8+65=73) + 8px gap
        int comboW = Math.Max(55, (halfW - 93) / 2);
        if (comboW > 80) comboW = 80;
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

        // ── lblCpuUsage ──
        lblCpuUsage.Left = w - 360;

        // ── Manual override ──
        int overrideWidth = grpOverride.ClientSize.Width;
        new Action(() =>
        {
            int labelW = 25;
            int comboW2 = Math.Max(100, (overrideWidth - 180) / 3);
            if (comboW2 > 160) comboW2 = 160;

            int cx = 12;
            foreach (Control c in grpOverride.Controls)
            {
                if (c is Label lbl && lbl.Text == "AC:")
                { c.Left = cx; c.Width = labelW; cx += labelW + 2; }
                else if (c == cmbManualAc)
                { c.Left = cx; c.Width = comboW2; cx += comboW2 + 12; }
                else if (c is Label lbl2 && lbl2.Text == "DC:")
                { c.Left = cx; c.Width = labelW; cx += labelW + 2; }
                else if (c == cmbManualDc)
                { c.Left = cx; c.Width = comboW2; cx += comboW2 + 12; }
                else if (c == btnApplyManual)
                { c.Left = Math.Max(cx, overrideWidth - 150); }
            }
        })();

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
            var status = _service.Status;
            lblServiceStatus.Text = $"状态: {(status == ServiceControllerStatus.Running ? "● 运行中" : "○ 已停止")}";
            lblServiceStatus.ForeColor = status == ServiceControllerStatus.Running ? Color.Green : Color.Red;
            btnStart.Enabled = status != ServiceControllerStatus.Running;
            btnStop.Enabled = status == ServiceControllerStatus.Running;
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
                lblLastSwitch.Text = $"上次切换原因: {statusInfo.LastSwitchReason ?? "--"}";
            }
        }
        catch
        {
            lblCurrentMode.Text = "当前模式: 读取失败";
            lblCpuUsage.Text = "CPU 占用: --";
        }
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
                    _service.Start();
                    _service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                    Logger.Info("Service started manually via config tool");
                    break;
                case "stop":
                    _service.Stop();
                    _service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                    Logger.Info("Service stopped manually via config tool");
                    break;
                case "restart":
                    if (_service.Status == ServiceControllerStatus.Running)
                    {
                        _service.Stop();
                        _service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                    }
                    _service.Start();
                    _service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                    Logger.Info("Service restarted via config tool");
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
                Logger.Info($"Manual override: AC={acVal} ({acName}), DC={dcVal} ({dcName})");
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
