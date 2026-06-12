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

    private AppSettings _settings = new();
    private AdapterConfig? _config;
    private BindingEngine? _engine;
    private KeyboardHookInputSource? _inputSource;
    private bool _exitRequested;
    private bool _firstShown = true;

    public MainForm(CommandLineOptions options)
    {
        _options = options;
        Text = "Ulanzi Adapter";
        MinimumSize = new Size(720, 440);
        StartPosition = FormStartPosition.CenterScreen;

        _notifyIcon = BuildTrayIcon();
        BuildLayout();
        Load += OnLoad;
        Shown += OnShown;
        Resize += OnResize;
        FormClosing += OnFormClosing;
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
            RowCount = 5,
            Padding = new Padding(14)
        };

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
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

        _browseButton.Text = "Sfoglia";
        _browseButton.AutoSize = true;
        _browseButton.Click += (_, _) => BrowseConfig();

        _reloadButton.Text = "Ricarica";
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

        _startStopButton.Text = "Avvia";
        _startStopButton.AutoSize = true;
        _startStopButton.Click += (_, _) => ToggleAdapter();

        _startupCheckBox.Text = "Avvia con Windows";
        _startupCheckBox.AutoSize = true;
        _startupCheckBox.CheckedChanged += (_, _) => UpdateStartupRegistration();

        _startMinimizedCheckBox.Text = "Avvio minimizzato";
        _startMinimizedCheckBox.AutoSize = true;
        _startMinimizedCheckBox.CheckedChanged += (_, _) => PersistSettings();

        controlsRow.Controls.Add(_startStopButton);
        controlsRow.Controls.Add(_startupCheckBox);
        controlsRow.Controls.Add(_startMinimizedCheckBox);

        _statusLabel.AutoSize = true;
        _statusLabel.Text = "Stato: non avviato";
        _statusLabel.Margin = new Padding(0, 8, 0, 2);

        _layerLabel.AutoSize = true;
        _layerLabel.Text = "Layer: -";
        _layerLabel.Margin = new Padding(0, 2, 0, 8);

        _logList.Dock = DockStyle.Fill;
        _logList.IntegralHeight = false;

        root.Controls.Add(configRow, 0, 0);
        root.Controls.Add(controlsRow, 0, 1);
        root.Controls.Add(_statusLabel, 0, 2);
        root.Controls.Add(_layerLabel, 0, 3);
        root.Controls.Add(_logList, 0, 4);

        Controls.Add(root);
    }

    private NotifyIcon BuildTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Apri", null, (_, _) => ShowFromTray());
        menu.Items.Add("Esci", null, (_, _) =>
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
            Title = "Seleziona configurazione UlanziAdapter",
            Filter = "JSON (*.json)|*.json|Tutti i file (*.*)|*.*",
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

            Log($"Config caricata: {Path.GetFileName(configPath)}");
            StartAdapter();
        }
        catch (Exception ex)
        {
            _config = null;
            _engine = null;
            _statusLabel.Text = "Stato: errore configurazione";
            _layerLabel.Text = "Layer: -";
            Log("Errore: " + ex.Message);
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
            _startStopButton.Text = "Ferma";
            _statusLabel.Text = _config.Behavior.SuppressOriginalInput
                ? "Stato: attivo, soppressione input originale ON"
                : "Stato: attivo, input originale lasciato passare";
            _layerLabel.Text = $"Layer: {_engine.ActiveLayer}";
            Log("Adapter avviato.");
        }
        catch (Exception ex)
        {
            StopAdapter();
            _statusLabel.Text = "Stato: errore avvio";
            Log("Errore avvio hook: " + ex.Message);
        }
    }

    private void StopAdapter()
    {
        if (_inputSource is not null)
        {
            _inputSource.Dispose();
            _inputSource = null;
        }

        _startStopButton.Text = "Avvia";
        _statusLabel.Text = "Stato: fermo";
    }

    private bool HandleInput(InputEvent input)
    {
        if (_config is null || _engine is null)
        {
            return false;
        }

        BindingExecution execution;
        try
        {
            execution = _engine.Handle(input);
            if (!execution.Handled)
            {
                return false;
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
        }
        catch (Exception ex)
        {
            BeginInvokeSafe(() => Log("Errore mapping: " + ex.Message));
            return false;
        }

        BeginInvokeSafe(() =>
        {
            _layerLabel.Text = $"Layer: {execution.ActiveLayer ?? _engine.ActiveLayer}";
            var action = execution.Send ?? (execution.Text is null ? "layer" : "text");
            Log($"{execution.ControlName}: {input.ToGestureString()} -> {action}");
        });

        return _config.Behavior.SuppressOriginalInput;
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
            Log("Errore startup: " + ex.Message);
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
