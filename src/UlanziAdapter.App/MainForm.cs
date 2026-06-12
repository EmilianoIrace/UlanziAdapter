using UlanziAdapter.Core.Actions;
using UlanziAdapter.Core.Configuration;
using UlanziAdapter.Core.Input;
using UlanziAdapter.Core.Mapping;
using UlanziAdapter.Windows.Input;
using UlanziAdapter.Windows.Output;
using UlanziAdapter.Windows.Startup;
using UlanziAdapter.Windows.Storage;

namespace UlanziAdapter.App;

internal sealed class MainForm : Form
{
    private readonly CommandLineOptions _options;
    private readonly SettingsStore _settingsStore = new();
    private readonly StartupRegistration _startupRegistration = new();
    private readonly SendInputKeyboardOutput _output = new();
    private readonly AudioEndpointVolumeController _audioVolume = new();
    private readonly NotifyIcon _notifyIcon;

    private readonly TextBox _configPathTextBox = new();
    private readonly Button _browseButton = new();
    private readonly Button _reloadButton = new();
    private readonly Button _startStopButton = new();
    private readonly CheckBox _startupCheckBox = new();
    private readonly CheckBox _startMinimizedCheckBox = new();
    private readonly Label _statusLabel = new();
    private readonly Label _layerLabel = new();
    private readonly ListBox _logList = new();
    private readonly ListView _bindingList = new();
    private readonly Button _captureButton = new();
    private readonly ComboBox _presetComboBox = new();
    private readonly Button _applyPresetButton = new();
    private readonly Label _captureStatusLabel = new();

    private AppSettings _settings = new();
    private AdapterConfig? _config;
    private BindingEngine? _engine;
    private KeyboardHookInputSource? _inputSource;
    private BindingSelection? _captureTarget;
    private bool _exitRequested;
    private bool _firstShown = true;

    public MainForm(CommandLineOptions options)
    {
        _options = options;
        Text = "Ulanzi Adapter";
        MinimumSize = new Size(860, 540);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        _notifyIcon = BuildTrayIcon();
        BuildLayout();
        Load += OnLoad;
        Shown += OnShown;
        Resize += OnResize;
        FormClosing += OnFormClosing;
        KeyDown += OnKeyDown;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopAdapter();
            _notifyIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(14)
        };

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var configRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            AutoSize = true
        };

        configRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        configRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        configRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        configRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var configLabel = new Label
        {
            Text = "Config JSON",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 7, 8, 7)
        };

        _configPathTextBox.Dock = DockStyle.Fill;
        _configPathTextBox.Margin = new Padding(0, 4, 8, 4);

        _browseButton.Text = "Browse";
        _browseButton.AutoSize = true;
        _browseButton.Click += (_, _) => BrowseConfig();

        _reloadButton.Text = "Reload";
        _reloadButton.AutoSize = true;
        _reloadButton.Click += (_, _) => LoadConfigAndRestart();

        configRow.Controls.Add(configLabel, 0, 0);
        configRow.Controls.Add(_configPathTextBox, 1, 0);
        configRow.Controls.Add(_browseButton, 2, 0);
        configRow.Controls.Add(_reloadButton, 3, 0);

        var controlsRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 8, 0, 2)
        };

        _startStopButton.Text = "Start";
        _startStopButton.AutoSize = true;
        _startStopButton.Click += (_, _) => ToggleAdapter();

        _startupCheckBox.Text = "Start with Windows";
        _startupCheckBox.AutoSize = true;
        _startupCheckBox.CheckedChanged += (_, _) => UpdateStartupRegistration();

        _startMinimizedCheckBox.Text = "Start minimized";
        _startMinimizedCheckBox.AutoSize = true;
        _startMinimizedCheckBox.CheckedChanged += (_, _) => PersistSettings();

        controlsRow.Controls.Add(_startStopButton);
        controlsRow.Controls.Add(_startupCheckBox);
        controlsRow.Controls.Add(_startMinimizedCheckBox);

        _statusLabel.AutoSize = true;
        _statusLabel.Text = "Status: not started";
        _statusLabel.Margin = new Padding(0, 8, 0, 2);

        _layerLabel.AutoSize = true;
        _layerLabel.Text = "Layer: -";
        _layerLabel.Margin = new Padding(0, 2, 0, 8);

        _logList.Dock = DockStyle.Fill;
        _logList.IntegralHeight = false;

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 10, 0, 0)
        };

        tabs.TabPages.Add(BuildRuntimeTab());
        tabs.TabPages.Add(BuildSetButtonsTab());

        root.Controls.Add(configRow, 0, 0);
        root.Controls.Add(controlsRow, 0, 1);
        root.Controls.Add(tabs, 0, 2);

        Controls.Add(root);
    }

    private TabPage BuildRuntimeTab()
    {
        var page = new TabPage("Runtime");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10)
        };

        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        panel.Controls.Add(_statusLabel, 0, 0);
        panel.Controls.Add(_layerLabel, 0, 1);
        panel.Controls.Add(_logList, 0, 2);
        page.Controls.Add(panel);
        return page;
    }

    private TabPage BuildSetButtonsTab()
    {
        var page = new TabPage("Set Buttons");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10)
        };

        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _bindingList.Dock = DockStyle.Fill;
        _bindingList.View = View.Details;
        _bindingList.FullRowSelect = true;
        _bindingList.MultiSelect = false;
        _bindingList.HideSelection = false;
        _bindingList.Columns.Add("Layer", 120);
        _bindingList.Columns.Add("Control", 160);
        _bindingList.Columns.Add("Source", 160);
        _bindingList.Columns.Add("Action", 220);
        _bindingList.Columns.Add("Description", 260);
        _bindingList.DoubleClick += (_, _) => StartCaptureForSelectedBinding();

        var actionsRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0, 8, 0, 4)
        };

        _captureButton.Text = "Capture Shortcut";
        _captureButton.AutoSize = true;
        _captureButton.Click += (_, _) => StartCaptureForSelectedBinding();

        _presetComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _presetComboBox.Width = 260;
        foreach (var preset in ActionPreset.StandardPresets)
        {
            _presetComboBox.Items.Add(preset);
        }

        if (_presetComboBox.Items.Count > 0)
        {
            _presetComboBox.SelectedIndex = 0;
        }

        _applyPresetButton.Text = "Apply Preset";
        _applyPresetButton.AutoSize = true;
        _applyPresetButton.Click += (_, _) => ApplySelectedPreset();

        actionsRow.Controls.Add(_captureButton);
        actionsRow.Controls.Add(_presetComboBox);
        actionsRow.Controls.Add(_applyPresetButton);

        _captureStatusLabel.AutoSize = true;
        _captureStatusLabel.Text = "Select a row, then capture a keyboard shortcut or apply a preset. Changes are saved to the selected JSON file.";
        _captureStatusLabel.Margin = new Padding(0, 3, 0, 0);

        panel.Controls.Add(_bindingList, 0, 0);
        panel.Controls.Add(actionsRow, 0, 1);
        panel.Controls.Add(_captureStatusLabel, 0, 2);
        page.Controls.Add(panel);
        return page;
    }

    private void RefreshBindingList()
    {
        _bindingList.Items.Clear();
        if (_config is null)
        {
            return;
        }

        foreach (var (layerName, bindings) in _config.Bindings)
        {
            foreach (var (controlName, binding) in bindings)
            {
                var item = new ListViewItem(layerName);
                item.SubItems.Add(controlName);
                item.SubItems.Add(binding.Source ?? string.Empty);
                item.SubItems.Add(DescribeAction(binding));
                item.SubItems.Add(binding.Description ?? string.Empty);
                item.Tag = new BindingSelection(layerName, controlName);
                _bindingList.Items.Add(item);
            }
        }
    }

    private BindingSelection? GetSelectedBinding()
    {
        if (_bindingList.SelectedItems.Count == 0)
        {
            return null;
        }

        return _bindingList.SelectedItems[0].Tag as BindingSelection;
    }

    private bool TryGetBinding(BindingSelection selection, out BindingConfig binding)
    {
        binding = null!;
        if (_config is null)
        {
            return false;
        }

        if (!_config.Bindings.TryGetValue(selection.LayerName, out var layer))
        {
            return false;
        }

        return layer.TryGetValue(selection.ControlName, out binding!);
    }

    private void StartCaptureForSelectedBinding()
    {
        var selection = GetSelectedBinding();
        if (selection is null)
        {
            _captureStatusLabel.Text = "Select a binding row first.";
            return;
        }

        if (!TryGetBinding(selection, out _))
        {
            _captureStatusLabel.Text = "Selected binding no longer exists. Reload the config.";
            return;
        }

        _captureTarget = selection;
        StopAdapter();
        _captureButton.Text = "Press shortcut...";
        _captureStatusLabel.Text = $"Capturing output for {selection.LayerName}.{selection.ControlName}. Press a shortcut, or Esc to cancel.";
        ActiveControl = _bindingList;
    }

    private void ApplySelectedPreset()
    {
        var selection = GetSelectedBinding();
        if (selection is null)
        {
            _captureStatusLabel.Text = "Select a binding row first.";
            return;
        }

        if (_presetComboBox.SelectedItem is not ActionPreset preset)
        {
            _captureStatusLabel.Text = "Select a preset first.";
            return;
        }

        if (!TryGetBinding(selection, out var binding))
        {
            _captureStatusLabel.Text = "Selected binding no longer exists. Reload the config.";
            return;
        }

        ApplyPreset(binding, preset);
        EnsurePresetLayerExists(selection, preset);
        SaveConfigAndRestart($"Preset '{preset.Name}' applied to {selection.LayerName}.{selection.ControlName}.");
    }

    private void EnsurePresetLayerExists(BindingSelection selection, ActionPreset preset)
    {
        if (_config is null || string.IsNullOrWhiteSpace(preset.LayerTarget))
        {
            return;
        }

        if (_config.Bindings.ContainsKey(preset.LayerTarget))
        {
            return;
        }

        if (!_config.Bindings.TryGetValue(selection.LayerName, out var sourceLayer))
        {
            _config.Bindings[preset.LayerTarget] = new Dictionary<string, BindingConfig>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        _config.Bindings[preset.LayerTarget] = sourceLayer.ToDictionary(
            item => item.Key,
            item => CloneBinding(item.Value),
            StringComparer.OrdinalIgnoreCase);
    }

    private static void ApplyPreset(BindingConfig binding, ActionPreset preset)
    {
        binding.Send = preset.Send;
        binding.Text = preset.Text;
        binding.Mouse = preset.MouseWheel is null
            ? null
            : new MouseActionConfig { Wheel = preset.MouseWheel, Clicks = preset.MouseWheelClicks };
        binding.Layer = preset.LayerMode is null
            ? null
            : new LayerActionConfig
            {
                Mode = preset.LayerMode,
                Target = preset.LayerTarget ?? "knobPressed",
                Fallback = preset.LayerFallback ?? "default"
            };
    }

    private static BindingConfig CloneBinding(BindingConfig binding)
    {
        return new BindingConfig
        {
            Enabled = binding.Enabled,
            Source = binding.Source,
            Send = binding.Send,
            Text = binding.Text,
            Mouse = binding.Mouse is null
                ? null
                : new MouseActionConfig { Wheel = binding.Mouse.Wheel, Clicks = binding.Mouse.Clicks },
            Layer = binding.Layer is null
                ? null
                : new LayerActionConfig
                {
                    Mode = binding.Layer.Mode,
                    Target = binding.Layer.Target,
                    Fallback = binding.Layer.Fallback
                },
            Description = binding.Description
        };
    }

    private void SaveCapturedShortcut(BindingSelection selection, string chord)
    {
        if (!TryGetBinding(selection, out var binding))
        {
            _captureStatusLabel.Text = "Selected binding no longer exists. Reload the config.";
            return;
        }

        binding.Send = chord;
        binding.Text = null;
        binding.Mouse = null;
        binding.Layer = null;
        SaveConfigAndRestart($"Shortcut '{chord}' saved for {selection.LayerName}.{selection.ControlName}.");
    }

    private void SaveConfigAndRestart(string logMessage)
    {
        if (_config is null)
        {
            return;
        }

        var configPath = _configPathTextBox.Text.Trim();
        ConfigLoader.Save(configPath, _config);
        _captureTarget = null;
        _captureButton.Text = "Capture Shortcut";
        _captureStatusLabel.Text = logMessage;
        LoadConfigAndRestart();
    }

    private static string DescribeAction(BindingConfig binding)
    {
        if (!string.IsNullOrWhiteSpace(binding.Send))
        {
            return $"Shortcut: {binding.Send}";
        }

        if (!string.IsNullOrEmpty(binding.Text))
        {
            return $"Text: {binding.Text}";
        }

        if (binding.Mouse is not null)
        {
            return $"Mouse wheel: {binding.Mouse.Wheel} x{binding.Mouse.Clicks}";
        }

        if (binding.Layer is not null)
        {
            return $"Layer: {binding.Layer.Mode} {binding.Layer.Target}";
        }

        return "None";
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_captureTarget is null)
        {
            return;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;

        if (e.KeyCode == Keys.Escape)
        {
            _captureTarget = null;
            _captureButton.Text = "Capture Shortcut";
            _captureStatusLabel.Text = "Capture cancelled.";
            StartAdapter();
            return;
        }

        if (IsModifierKey(e.KeyCode))
        {
            return;
        }

        var chord = BuildChord(e);
        if (chord is null)
        {
            _captureStatusLabel.Text = $"Unsupported key: {e.KeyCode}. Try another key or use a preset.";
            return;
        }

        SaveCapturedShortcut(_captureTarget, chord);
    }

    private static string? BuildChord(KeyEventArgs e)
    {
        var key = ConvertKeyCode(e.KeyCode);
        if (key is null)
        {
            return null;
        }

        var parts = new List<string>(5);
        if (e.Control)
        {
            parts.Add("Ctrl");
        }

        if (e.Shift)
        {
            parts.Add("Shift");
        }

        if (e.Alt)
        {
            parts.Add("Alt");
        }

        parts.Add(key);
        return string.Join("+", parts);
    }

    private static bool IsModifierKey(Keys key)
    {
        return key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LControlKey or Keys.RControlKey or
            Keys.LShiftKey or Keys.RShiftKey or Keys.LMenu or Keys.RMenu or Keys.LWin or Keys.RWin;
    }

    private static string? ConvertKeyCode(Keys key)
    {
        if (key is >= Keys.A and <= Keys.Z)
        {
            return key.ToString();
        }

        if (key is >= Keys.D0 and <= Keys.D9)
        {
            return ((int)(key - Keys.D0)).ToString();
        }

        if (key is >= Keys.NumPad0 and <= Keys.NumPad9)
        {
            return $"Numpad{(int)(key - Keys.NumPad0)}";
        }

        if (key is >= Keys.F1 and <= Keys.F24)
        {
            return $"F{(int)(key - Keys.F1) + 1}";
        }

        return key switch
        {
            Keys.Space => "Space",
            Keys.Enter => "Enter",
            Keys.Tab => "Tab",
            Keys.Back => "Backspace",
            Keys.Delete => "Delete",
            Keys.Insert => "Insert",
            Keys.Home => "Home",
            Keys.End => "End",
            Keys.PageUp => "PageUp",
            Keys.PageDown => "PageDown",
            Keys.Left => "Left",
            Keys.Right => "Right",
            Keys.Up => "Up",
            Keys.Down => "Down",
            Keys.Add => "NumpadAdd",
            Keys.Oemplus => "NumpadAdd",
            Keys.Subtract => "NumpadSubtract",
            Keys.OemMinus => "NumpadSubtract",
            Keys.Multiply => "NumpadMultiply",
            Keys.Divide => "NumpadDivide",
            Keys.Decimal => "NumpadDecimal",
            Keys.MediaPreviousTrack => "MediaPreviousTrack",
            Keys.MediaPlayPause => "MediaPlayPause",
            Keys.MediaNextTrack => "MediaNextTrack",
            Keys.VolumeUp => "VolumeUp",
            Keys.VolumeDown => "VolumeDown",
            Keys.VolumeMute => "VolumeMute",
            _ => null
        };
    }

    private NotifyIcon BuildTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowFromTray());
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _exitRequested = true;
            Close();
        });

        return new NotifyIcon
        {
            Text = "Ulanzi Adapter",
            Icon = SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = menu
        };
    }

    private void OnLoad(object? sender, EventArgs e)
    {
        _settings = _settingsStore.Load();
        _startMinimizedCheckBox.Checked = _settings.StartMinimized;
        _startupCheckBox.Checked = _startupRegistration.IsEnabled();

        var configPath = _options.ConfigPath ??
                         _settings.LastConfigPath ??
                         EnsureUserDefaultConfig();

        _configPathTextBox.Text = configPath;
        LoadConfigAndRestart();
    }

    private void OnShown(object? sender, EventArgs e)
    {
        if (!_firstShown)
        {
            return;
        }

        _firstShown = false;
        if (_options.StartMinimized)
        {
            Hide();
            WindowState = FormWindowState.Minimized;
        }
    }

    private void OnResize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized)
        {
            Hide();
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_exitRequested || e.CloseReason != CloseReason.UserClosing)
        {
            return;
        }

        e.Cancel = true;
        Hide();
        WindowState = FormWindowState.Minimized;
    }

    private void BrowseConfig()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select UlanziAdapter configuration",
            Filter = "JSON (*.json)|*.json|All files (*.*)|*.*",
            FileName = _configPathTextBox.Text
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _configPathTextBox.Text = dialog.FileName;
        LoadConfigAndRestart();
    }

    private void ToggleAdapter()
    {
        if (_inputSource?.IsRunning == true)
        {
            StopAdapter();
            return;
        }

        StartAdapter();
    }

    private void LoadConfigAndRestart()
    {
        try
        {
            StopAdapter();
            var configPath = _configPathTextBox.Text.Trim();
            _config = ConfigLoader.Load(configPath);
            _engine = new BindingEngine(_config);
            _engine.ActiveLayerChanged += layer => BeginInvokeSafe(() => _layerLabel.Text = $"Layer: {layer}");

            _settings.LastConfigPath = configPath;
            _settings.StartMinimized = _startMinimizedCheckBox.Checked;
            _settingsStore.Save(_settings);
            UpdateStartupRegistration();
            RefreshBindingList();

            Log($"Config loaded: {Path.GetFileName(configPath)}");
            StartAdapter();
        }
        catch (Exception ex)
        {
            _config = null;
            _engine = null;
            RefreshBindingList();
            _statusLabel.Text = "Status: configuration error";
            _layerLabel.Text = "Layer: -";
            Log("Error: " + ex.Message);
        }
    }

    private void StartAdapter()
    {
        if (_config is null || _engine is null)
        {
            return;
        }

        try
        {
            _inputSource = new KeyboardHookInputSource();
            _inputSource.Start(HandleInput);
            _startStopButton.Text = "Stop";
            _statusLabel.Text = _config.Behavior.SuppressOriginalInput
                ? "Status: active, original input suppression ON"
                : "Status: active, original input passes through";
            _layerLabel.Text = $"Layer: {_engine.ActiveLayer}";
            Log("Adapter started.");
        }
        catch (Exception ex)
        {
            StopAdapter();
            _statusLabel.Text = "Status: startup error";
            Log("Hook startup error: " + ex.Message);
        }
    }

    private void StopAdapter()
    {
        if (_inputSource is not null)
        {
            _inputSource.Dispose();
            _inputSource = null;
        }

        _startStopButton.Text = "Start";
        _statusLabel.Text = "Status: stopped";
    }

    private bool HandleInput(InputEvent input)
    {
        if (_config is null || _engine is null)
        {
            return false;
        }

        BindingExecution execution;
        AudioVolumeSnapshot? volumeSnapshot = null;
        try
        {
            execution = _engine.Handle(input);
            if (!execution.Handled)
            {
                return false;
            }

            if (ShouldGuardLeakedVolumeInput(input, execution))
            {
                volumeSnapshot = _audioVolume.TryCapture();
            }

            _output.ReleaseModifiers(input.Modifiers);

            if (!string.IsNullOrWhiteSpace(execution.Send))
            {
                _output.SendChordSequence(execution.Send);
            }

            if (!string.IsNullOrEmpty(execution.Text))
            {
                _output.SendText(execution.Text);
            }

            if (!string.IsNullOrWhiteSpace(execution.MouseWheel))
            {
                _output.SendMouseWheel(execution.MouseWheel, execution.MouseWheelClicks);
            }

            if (volumeSnapshot is not null)
            {
                _audioVolume.RestoreSoon(volumeSnapshot);
            }
        }
        catch (Exception ex)
        {
            if (volumeSnapshot is not null)
            {
                _audioVolume.RestoreSoon(volumeSnapshot);
            }

            BeginInvokeSafe(() => Log("Mapping error: " + ex.Message));
            return false;
        }

        BeginInvokeSafe(() =>
        {
            _layerLabel.Text = $"Layer: {execution.ActiveLayer ?? _engine.ActiveLayer}";
            var action = execution.Send ??
                         (execution.Text is null
                             ? execution.MouseWheel is null ? "layer" : $"mouse wheel {execution.MouseWheel}"
                             : "text");
            Log($"{execution.ControlName}: {input.ToGestureString()} -> {action}");
        });

        return _config.Behavior.SuppressOriginalInput;
    }

    private static bool ShouldGuardLeakedVolumeInput(InputEvent input, BindingExecution execution)
    {
        if (input.NormalizedKey is not ("VolumeUp" or "VolumeDown" or "VolumeMute"))
        {
            return false;
        }

        return !SendsVolumeCommand(execution.Send);
    }

    private static bool SendsVolumeCommand(string? send)
    {
        if (string.IsNullOrWhiteSpace(send))
        {
            return false;
        }

        return send
            .Split(new[] { '+', ';' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(KeyName.Normalize)
            .Any(key => key is "VolumeUp" or "VolumeDown" or "VolumeMute");
    }

    private void UpdateStartupRegistration()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        try
        {
            var configPath = _configPathTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(configPath))
            {
                return;
            }

            _startupRegistration.SetEnabled(
                _startupCheckBox.Checked,
                Application.ExecutablePath,
                configPath,
                _startMinimizedCheckBox.Checked);

            PersistSettings();
        }
        catch (Exception ex)
        {
            Log("Startup error: " + ex.Message);
        }
    }

    private void PersistSettings()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        var configPath = _configPathTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(configPath))
        {
            _settings.LastConfigPath = configPath;
        }

        _settings.StartMinimized = _startMinimizedCheckBox.Checked;
        _settingsStore.Save(_settings);
    }

    private string EnsureUserDefaultConfig()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var userDirectory = Path.Combine(appData, "UlanziAdapter");
        Directory.CreateDirectory(userDirectory);

        var destination = Path.Combine(userDirectory, "d100h.json");
        if (File.Exists(destination))
        {
            return destination;
        }

        var source = Path.Combine(AppContext.BaseDirectory, "config", "d100h.sample.json");
        if (File.Exists(source))
        {
            File.Copy(source, destination, overwrite: false);
            return destination;
        }

        throw new FileNotFoundException("Default sample config was not found.", source);
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void Log(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss}  {message}";
        _logList.Items.Insert(0, line);
        while (_logList.Items.Count > 300)
        {
            _logList.Items.RemoveAt(_logList.Items.Count - 1);
        }
    }

    private void BeginInvokeSafe(Action action)
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            BeginInvoke(action);
        }
        catch (InvalidOperationException)
        {
        }
    }
}

internal sealed record BindingSelection(string LayerName, string ControlName);

internal sealed record ActionPreset(
    string Name,
    string? Send = null,
    string? Text = null,
    string? MouseWheel = null,
    int MouseWheelClicks = 1,
    string? LayerMode = null,
    string? LayerTarget = null,
    string? LayerFallback = null)
{
    public override string ToString() => Name;

    public static IReadOnlyList<ActionPreset> StandardPresets { get; } = new[]
    {
        new ActionPreset("Mouse - Scroll Up", MouseWheel: "up"),
        new ActionPreset("Mouse - Scroll Down", MouseWheel: "down"),
        new ActionPreset("Mouse - Horizontal Scroll Left", MouseWheel: "left"),
        new ActionPreset("Mouse - Horizontal Scroll Right", MouseWheel: "right"),
        new ActionPreset("Navigation - Arrow Left", Send: "Left"),
        new ActionPreset("Navigation - Arrow Right", Send: "Right"),
        new ActionPreset("Navigation - Arrow Up", Send: "Up"),
        new ActionPreset("Navigation - Arrow Down", Send: "Down"),
        new ActionPreset("Navigation - Page Up", Send: "PageUp"),
        new ActionPreset("Navigation - Page Down", Send: "PageDown"),
        new ActionPreset("Editing - Copy", Send: "Ctrl+C"),
        new ActionPreset("Editing - Paste", Send: "Ctrl+V"),
        new ActionPreset("Editing - Cut", Send: "Ctrl+X"),
        new ActionPreset("Editing - Undo", Send: "Ctrl+Z"),
        new ActionPreset("Editing - Redo", Send: "Ctrl+Y"),
        new ActionPreset("Editing - Save", Send: "Ctrl+S"),
        new ActionPreset("Editing - Select All", Send: "Ctrl+A"),
        new ActionPreset("Zoom - In", Send: "Ctrl+NumpadAdd"),
        new ActionPreset("Zoom - Out", Send: "Ctrl+NumpadSubtract"),
        new ActionPreset("Media - Play/Pause", Send: "MediaPlayPause"),
        new ActionPreset("Media - Previous Track", Send: "MediaPreviousTrack"),
        new ActionPreset("Media - Next Track", Send: "MediaNextTrack"),
        new ActionPreset("Media - Volume Up", Send: "VolumeUp"),
        new ActionPreset("Media - Volume Down", Send: "VolumeDown"),
        new ActionPreset("Media - Mute", Send: "VolumeMute"),
        new ActionPreset("Layer - Toggle knobPressed", LayerMode: "toggle", LayerTarget: "knobPressed", LayerFallback: "default"),
        new ActionPreset("Layer - Hold knobPressed", LayerMode: "momentary", LayerTarget: "knobPressed", LayerFallback: "default")
    };
}
