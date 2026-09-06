using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DeejFeedback;

public partial class MainWindow : Window
{
    private readonly ConfigStore _store = new();
    private readonly SerialController _serial = new();
    private readonly AudioEngine _audio = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private AppConfig _config;
    private PrivacyState _privacy = PrivacyState.Active;
    private readonly Dictionary<int, (ProgressBar actual, ProgressBar physical, TextBlock detail)> _channelUi = [];
    private readonly Dictionary<int, (TextBox name, TextBox targets, TextBox min, TextBox max, TextBox curve)> _editors = [];
    private int _selectedMappingChannel;
    private bool _loading = true;
    private DateTime _lastReconnectAttempt = DateTime.MinValue;
    private bool _lastMicButtonPressed;
    private bool _micButtonStateKnown;
    private DateTime _lastFaderFrameUtc = DateTime.MinValue;
    private readonly System.Windows.Forms.NotifyIcon _trayIcon = new();
    private System.Windows.Forms.ToolStripMenuItem? _trayPrivacyItem;
    private bool _exitRequested;
    private bool _trayHintShown;
    private PrivacyState? _displayedPrivacy;
    private bool? _displayedConnection;

    public MainWindow()
    {
        InitializeComponent();
        BuildVersionText.ToolTip = "Running executable: " + Environment.ProcessPath;
        _config = _store.Load();
        _audio.RefreshRenderInventory();
        InitializeTray();
        BuildChannelCards();
        BuildMappingEditors();
        LoadSettingsControls();
        RefreshPorts();
        _serial.FadersReceived += values => Dispatcher.Invoke(() => HandleFaders(values));
        _serial.MicButtonPressed += () => Dispatcher.Invoke(TogglePrivacy);
        _serial.MicButtonStateReceived += pressed => Dispatcher.Invoke(() => HandleMicButtonState(pressed));
        _serial.MicButtonCommandReceived += sequence => Dispatcher.Invoke(() => HandleMicButtonCommand(sequence));
        _serial.Log += message => Dispatcher.Invoke(() => StatusText.Text = message);
        _timer.Tick += (_, _) => RefreshAudioState();
        _timer.Start();
        _loading = false;
        TryConnect();
        if (_config.StartMinimized && Environment.GetCommandLineArgs().Contains("--autostart")) WindowState = WindowState.Minimized;
    }

    private void BuildChannelCards()
    {
        ChannelCards.Children.Clear();
        foreach (var channel in _config.Channels.OrderBy(c => c.Index))
        {
            var card = new Border { Background = (Brush)FindResource("PanelBrush"), CornerRadius = new CornerRadius(12), Padding = new Thickness(18), Margin = new Thickness(0, 0, 0, 12) };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            var title = new StackPanel();
            title.Children.Add(new TextBlock { Text = $"Fader {channel.Index + 1}", Foreground = (Brush)FindResource("MutedTextBrush") });
            title.Children.Add(new TextBlock { Text = channel.Name, FontSize = 18, FontWeight = FontWeights.SemiBold });
            var bars = new StackPanel(); Grid.SetColumn(bars, 1);
            var actual = new ProgressBar { Height = 14, Maximum = 100, Foreground = (Brush)FindResource("AccentBrush"), Background = (Brush)FindResource("Panel2Brush") };
            var physical = new ProgressBar { Height = 4, Maximum = 100, Margin = new Thickness(0, 5, 0, 0), Foreground = Brushes.SlateGray, Background = Brushes.Transparent };
            var detail = new TextBlock { Text = "Windows — %  ·  Fader — %", Foreground = (Brush)FindResource("MutedTextBrush"), Margin = new Thickness(0, 7, 0, 0) };
            bars.Children.Add(actual); bars.Children.Add(physical); bars.Children.Add(detail);
            grid.Children.Add(title); grid.Children.Add(bars); card.Child = grid; ChannelCards.Children.Add(card);
            _channelUi[channel.Index] = (actual, physical, detail);
        }
    }

    private void BuildMappingEditors()
    {
        MappingEditors.Children.Clear(); _editors.Clear();
        foreach (var channel in _config.Channels.OrderBy(c => c.Index))
        {
            var group = new Expander { Header = $"Fader {channel.Index + 1}: {channel.Name}", IsExpanded = channel.Index == 0, Margin = new Thickness(0, 0, 0, 8) };
            group.Expanded += (_, _) => _selectedMappingChannel = channel.Index;
            var grid = new Grid { Margin = new Thickness(4, 8, 4, 8) };
            for (var i = 0; i < 5; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
            var name = AddField(grid, "Name", channel.Name, 0);
            var targets = AddField(grid, "Processes", string.Join(Environment.NewLine, channel.Targets), 1, 76, true);
            var min = AddField(grid, "Minimum %", channel.MinimumPercent.ToString(), 2);
            var max = AddField(grid, "Maximum %", channel.MaximumPercent.ToString(), 3);
            var curve = AddField(grid, "Exponent", channel.CurveExponent.ToString("0.00"), 4);
            group.Content = grid; MappingEditors.Children.Add(group); _editors[channel.Index] = (name, targets, min, max, curve);
        }
    }

    private static TextBox AddField(Grid grid, string label, string value, int row, double height = double.NaN, bool multi = false)
    {
        var caption = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 8, 4) };
        var box = new TextBox { Text = value, Margin = new Thickness(0, 4, 0, 4), AcceptsReturn = multi, VerticalScrollBarVisibility = multi ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled };
        if (!double.IsNaN(height)) box.Height = height;
        Grid.SetRow(caption, row); Grid.SetRow(box, row); Grid.SetColumn(box, 1); grid.Children.Add(caption); grid.Children.Add(box); return box;
    }

    private void HandleFaders(int[] values)
    {
        _lastFaderFrameUtc = DateTime.UtcNow;
        for (var i = 0; i < Math.Min(4, values.Length); i++)
        {
            var channel = _config.Channels.First(c => c.Index == i);
            if (channel.HasRawValue && Math.Abs(values[i] - channel.RawValue) < channel.DeadbandAdc) continue;
            channel.RawValue = values[i]; channel.HasRawValue = true; channel.RequestedVolume = channel.MapRawToVolume(values[i]);
            channel.ActualVolume = _audio.SetTargetsVolume(channel.Targets, channel.RequestedVolume);
        }
        UpdateRawValuesText(values);
    }

    private void HandleMicButtonState(bool pressed)
    {
        _micButtonStateKnown = true;
        _lastMicButtonPressed = pressed;
        var values = _config.Channels.OrderBy(c => c.Index).Select(c => c.RawValue).ToArray();
        UpdateRawValuesText(values);
    }

    private void HandleMicButtonCommand(int sequence)
    {
        TogglePrivacy();
        StatusText.Text = $"Microphone button #{sequence} received and acknowledged";
    }

    private void UpdateRawValuesText(IReadOnlyList<int> values)
    {
        if (!IsVisible || WindowState == WindowState.Minimized) return;
        RawValuesText.Text = string.Join("   ", values.Select((v, i) => $"A{i} {v,4}"))
            + $"   BTN {(_micButtonStateKnown ? (_lastMicButtonPressed ? "PRESSED" : "released") : "—")}";
    }

    private void RefreshAudioState()
    {
        try
        {
            _audio.RefreshRenderInventory();
            var mixerVisible = IsVisible && WindowState != WindowState.Minimized && MixerTab.IsSelected;
            _privacy = _audio.GetPrivacyState(_config.PrivacyMuteAllCaptureDevices);
            if (!_serial.IsConnected && DateTime.UtcNow - _lastReconnectAttempt > TimeSpan.FromSeconds(5))
            {
                _lastReconnectAttempt = DateTime.UtcNow;
                TryConnect();
            }
            if (_config.ContinuousFaderUpdates && _serial.IsConnected &&
                (DateTime.UtcNow - _lastFaderFrameUtc).TotalMilliseconds < Math.Max(3000, _config.ControllerHeartbeatMs * 3))
            {
                foreach (var channel in _config.Channels.Where(c => c.HasRawValue))
                {
                    channel.RequestedVolume = channel.MapRawToVolume(channel.RawValue);
                    _audio.SetTargetsVolume(channel.Targets, channel.RequestedVolume);
                }
            }
            var sessions = _audio.GetRenderSessions();
            foreach (var channel in _config.Channels)
            {
                var matches = sessions.Where(s => channel.Targets.Contains(s.ProcessName, StringComparer.OrdinalIgnoreCase)).ToList();
                if (matches.Count > 0) channel.ActualVolume = matches.Average(x => x.Volume);
                if (!mixerVisible || !_channelUi.TryGetValue(channel.Index, out var ui)) continue;
                ui.actual.Value = channel.ActualVolume * 100; ui.physical.Value = channel.MapRawToVolume(channel.RawValue) * 100;
                ui.detail.Text = $"Windows {channel.ActualVolume:P0}  ·  Fader {channel.MapRawToVolume(channel.RawValue):P0}  ·  {matches.Count} Session(s)";
            }
            if (mixerVisible)
            {
                var rows = _audio.GetCaptureDevices().Select(d => $"{(d.Muted ? "● MUTED" : "○ AVAILABLE")}  {d.Name}  ·  Level {ToDb(d.Peak):0} dBFS").ToList();
                if (CaptureDevicesList.ItemsSource is not List<string> previous || !previous.SequenceEqual(rows))
                    CaptureDevicesList.ItemsSource = rows;
            }
            UpdatePrivacyBanner();
            var volumes = _config.Channels.OrderBy(c => c.Index).Select(c => (int)Math.Round(c.ActualVolume * 100)).ToArray();
            _serial.SendFeedback(volumes, _serial.IsConnected ? _privacy : PrivacyState.Disconnected, _config.DisplaySleepSeconds, _config.ControllerHeartbeatMs, _config.FeedbackIntervalMs);
            SetConnectionUi(_serial.IsConnected);
        }
        catch (Exception ex) { StatusText.Text = "Audio error: " + ex.Message; _privacy = PrivacyState.Uncertain; UpdatePrivacyBanner(); }
    }

    private static double ToDb(float value) => value <= 0.000001 ? -96 : 20 * Math.Log10(value);

    private void OnMainTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Ignore selection events bubbling from nested lists and combo boxes.
        if (!_loading && ReferenceEquals(e.OriginalSource, sender) &&
            AssignmentsTab.IsSelected && SessionsList.ItemsSource is null)
            RefreshApplications();
    }

    private void OnRefreshApplications(object sender, RoutedEventArgs e) => RefreshApplications();

    private void RefreshApplications()
    {
        try
        {
            var selected = SessionsList.SelectedItem as string;
            _audio.RefreshRenderInventory(force: true);
            SessionsList.ItemsSource = _audio.GetRenderSessions().Select(s => s.ProcessName)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
            SessionsList.SelectedItem = selected;
            StatusText.Text = $"Applications refreshed at {DateTime.Now:T}. Play audio in an app if it is missing.";
        }
        catch (Exception ex) { StatusText.Text = "Application scan failed: " + ex.Message; }
    }
    private void TogglePrivacy()
    {
        try { _privacy = _audio.TogglePrivacyMute(_config.PrivacyMuteAllCaptureDevices); }
        catch (Exception ex) { _privacy = PrivacyState.Uncertain; StatusText.Text = "Microphone error: " + ex.Message; }
        UpdatePrivacyBanner();
        var volumes = _config.Channels.OrderBy(c => c.Index).Select(c => (int)Math.Round(c.ActualVolume * 100)).ToArray();
        _serial.SendFeedback(volumes, _serial.IsConnected ? _privacy : PrivacyState.Disconnected,
            _config.DisplaySleepSeconds, _config.ControllerHeartbeatMs, _config.FeedbackIntervalMs);
    }
    private void OnPrivacyClicked(object sender, RoutedEventArgs e) => TogglePrivacy();

    private void UpdatePrivacyBanner()
    {
        if (_displayedPrivacy == _privacy) return;
        _displayedPrivacy = _privacy;
        var (title, detail, color) = _privacy switch
        {
            PrivacyState.MutedConfirmed => ("INPUT MUTE CONFIRMED", "All monitored Windows input endpoints report muted.", "#48272C"),
            PrivacyState.Uncertain => ("MIXED / UNKNOWN", "Inputs have different mute states, are unavailable, or could not be verified. Press mute to mute all monitored inputs.", "#57451E"),
            PrivacyState.Disconnected => ("CONTROLLER DISCONNECTED", "The hardware button is unavailable.", "#343A46"),
            _ => ("MICROPHONE AVAILABLE", "An input may be available to applications.", "#29423E")
        };
        PrivacyTitle.Text = title; PrivacyDetail.Text = detail; PrivacyBanner.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        if (_trayPrivacyItem is not null)
            _trayPrivacyItem.Text = _privacy == PrivacyState.MutedConfirmed ? "Release privacy mute" : "Enable privacy mute";
        _trayIcon.Text = _privacy switch
        {
            PrivacyState.MutedConfirmed => "Deej Feedback – Inputs muted",
            PrivacyState.Uncertain => "Deej Feedback – State unknown",
            _ => "Deej Feedback – Inputs available"
        };
    }

    private void SetConnectionUi(bool connected)
    {
        if (_displayedConnection == connected) return;
        _displayedConnection = connected;
        ConnectionDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(connected ? "#63D7C6" : "#E15B64"));
        ConnectionText.Text = connected ? "Controller connected" : "Controller disconnected";
        ConnectButton.Content = connected ? "Disconnect" : "Connect";
    }

    private void TryConnect() { try { _serial.Connect(_config.ComPort, _config.BaudRate); } catch (Exception ex) { StatusText.Text = "Not connected: " + ex.Message; } }
    private void OnConnectClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_serial.IsConnected) _serial.Disconnect(); else { ReadSettingsControls(); TryConnect(); }
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Settings error"); }
        SetConnectionUi(_serial.IsConnected);
    }
    private void OnRefreshPorts(object sender, RoutedEventArgs e) => RefreshPorts();
    private void RefreshPorts() { var selected = _config.ComPort; PortCombo.ItemsSource = SerialController.AvailablePorts(); PortCombo.SelectedItem = selected; if (PortCombo.SelectedItem is null && PortCombo.Items.Count > 0) PortCombo.SelectedIndex = 0; }

    private void LoadSettingsControls()
    {
        _config.ControllerHeartbeatMs = Math.Clamp(_config.ControllerHeartbeatMs, 50, 5000);
        _config.FeedbackIntervalMs = Math.Clamp(_config.FeedbackIntervalMs, 100, 2000);
        ContinuousFadersCheck.IsChecked = _config.ContinuousFaderUpdates;
        HeartbeatText.Text = _config.ControllerHeartbeatMs.ToString();
        FeedbackIntervalText.Text = _config.FeedbackIntervalMs.ToString();
        _timer.Interval = TimeSpan.FromMilliseconds(_config.FeedbackIntervalMs);
        AutostartCheck.IsChecked = _config.StartWithWindows; StartMinimizedCheck.IsChecked = _config.StartMinimized;
        AllCaptureCheck.IsChecked = _config.PrivacyMuteAllCaptureDevices;
        DisplaySleepText.Text = _config.DisplaySleepSeconds.ToString(); BaudCombo.SelectedIndex = _config.BaudRate == 9600 ? 0 : 1;
    }

    private void ReadSettingsControls()
    {
        if (!int.TryParse(HeartbeatText.Text, out var heartbeat) || heartbeat < 50 || heartbeat > 5000 ||
            !int.TryParse(FeedbackIntervalText.Text, out var feedback) || feedback < 100 || feedback > 2000)
            throw new ArgumentException("Heartbeat must be 50–5000 ms and PC feedback must be 100–2000 ms.");
        _config.ContinuousFaderUpdates = ContinuousFadersCheck.IsChecked == true;
        _config.ControllerHeartbeatMs = heartbeat;
        _config.FeedbackIntervalMs = feedback;
        _timer.Interval = TimeSpan.FromMilliseconds(feedback);
        _config.ComPort = PortCombo.SelectedItem?.ToString() ?? _config.ComPort;
        _config.BaudRate = int.TryParse((BaudCombo.SelectedItem as ComboBoxItem)?.Content?.ToString(), out var baud) ? baud : 115200;
        _config.StartWithWindows = AutostartCheck.IsChecked == true; _config.StartMinimized = StartMinimizedCheck.IsChecked == true;
        _config.PrivacyMuteAllCaptureDevices = AllCaptureCheck.IsChecked == true;
        if (int.TryParse(DisplaySleepText.Text, out var sleep)) _config.DisplaySleepSeconds = Math.Clamp(sleep, 0, 3600);
        foreach (var channel in _config.Channels)
        {
            if (!_editors.TryGetValue(channel.Index, out var e)) continue;
            channel.Name = e.name.Text.Trim(); channel.Targets = e.targets.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (int.TryParse(e.min.Text, out var min)) channel.MinimumPercent = Math.Clamp(min, 0, 100);
            if (int.TryParse(e.max.Text, out var max)) channel.MaximumPercent = Math.Clamp(max, 0, 100);
            if (double.TryParse(e.curve.Text.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var curve)) channel.CurveExponent = Math.Clamp(curve, 0.1, 5);
        }
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            ReadSettingsControls(); _store.Save(_config); UpdateAutostart(); BuildChannelCards(); StatusText.Text = "Settings saved";
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not save settings"); }
    }
    private void OnSettingChanged(object sender, RoutedEventArgs e) { if (!_loading) StatusText.Text = "Unsaved changes"; }
    private void OnSettingChanged(object sender, TextChangedEventArgs e) { if (!_loading) StatusText.Text = "Unsaved changes"; }

    private void UpdateAutostart()
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (_config.StartWithWindows) key.SetValue("DeejFeedback", $"\"{Environment.ProcessPath}\" --autostart"); else key.DeleteValue("DeejFeedback", false);
    }

    private void OnImportYaml(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Deej configuration|*.yaml;*.yml|All files|*.*" };
        if (dialog.ShowDialog() != true) return;
        try { _config = _store.ImportLegacyYaml(dialog.FileName, _config); BuildChannelCards(); BuildMappingEditors(); LoadSettingsControls(); StatusText.Text = "Deej configuration imported; please save."; }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Import failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void OnSessionDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (SessionsList.SelectedItem is not string selected || !_editors.TryGetValue(_selectedMappingChannel, out var editor)) return;
        var process = selected.Split(new[] { "   " }, StringSplitOptions.None)[0];
        if (string.IsNullOrWhiteSpace(process)) return;
        var existing = editor.targets.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (!existing.Contains(process, StringComparer.OrdinalIgnoreCase)) { existing.Add(process); editor.targets.Text = string.Join(Environment.NewLine, existing); }
    }

    private void InitializeTray()
    {
        _trayIcon.Icon = BrandIcon.Create();
        Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
            _trayIcon.Icon.Handle,
            Int32Rect.Empty,
            System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
        _trayIcon.Text = "Deej Feedback";
        _trayIcon.Visible = true;
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);

        var menu = new System.Windows.Forms.ContextMenuStrip();
        var openItem = new System.Windows.Forms.ToolStripMenuItem("Open Deej Feedback");
        openItem.Click += (_, _) => Dispatcher.Invoke(ShowFromTray);
        _trayPrivacyItem = new System.Windows.Forms.ToolStripMenuItem("Enable privacy mute");
        _trayPrivacyItem.Click += (_, _) => Dispatcher.Invoke(TogglePrivacy);
        var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => Dispatcher.Invoke(ExitApplication);
        menu.Items.Add(openItem);
        menu.Items.Add(_trayPrivacyItem);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);
        _trayIcon.ContextMenuStrip = menu;
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void OnWindowStateChanged(object sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized) return;
        Hide();
        if (_trayHintShown) return;
        _trayHintShown = true;
        _trayIcon.ShowBalloonTip(2500, "Deej Feedback is still running", "Double-click the tray icon to open the window.", System.Windows.Forms.ToolTipIcon.Info);
    }

    private void ExitApplication()
    {
        _exitRequested = true;
        Close();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_exitRequested)
        {
            e.Cancel = true;
            Hide();
            if (!_trayHintShown)
            {
                _trayHintShown = true;
                _trayIcon.ShowBalloonTip(2500, "Deej Feedback is still running", "The application has been minimized to the notification area.", System.Windows.Forms.ToolTipIcon.Info);
            }
            return;
        }
        _timer.Stop();
        _serial.Dispose();
        _audio.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Icon?.Dispose();
        _trayIcon.Dispose();
    }
}
