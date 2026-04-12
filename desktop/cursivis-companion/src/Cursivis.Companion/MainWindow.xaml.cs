using Cursivis.Companion.Controllers;
using Cursivis.Companion.Infrastructure;
using Cursivis.Companion.Models;
using Cursivis.Companion.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Cursivis.Companion;

public partial class MainWindow : Window
{
    private const int TriggerHotkeyId = 0xCA11;
    private const int TakeActionHotkeyId = 0xCA12;
    private const int VoiceHotkeyId = 0xCA13;
    private const int WmHotKey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private readonly TriggerController _triggerController;
    private readonly SettingsService _settingsService;
    private readonly LogitechRuntimeStatusService _logitechRuntimeStatusService;
    private readonly RuntimeLaunchProfileService _runtimeLaunchProfileService;
    private readonly GeminiClient _runtimeGeminiClient;
    private int _lastDialValue;
    private bool _suppressDialEvents;
    private bool _isModeInitialized;
    private CancellationTokenSource? _longPressHoldCts;
    private Task? _longPressHoldTask;
    private HwndSource? _hwndSource;
    private readonly DispatcherTimer _logitechStatusTimer;
    private bool _showOrbDuringWorkflow;
    private TakeActionPromptPreference _takeActionPromptPreference;
    private CompanionThemeMode _themeMode;
    private TalkTriggerInputMode _talkTriggerInputMode;
    private bool _playHapticSound;
    private bool _isUpdatingThemeSelection;
    private bool _isUpdatingApiKey;

    public MainWindow(TriggerController triggerController, SettingsService settingsService, CompanionSettings initialSettings)
    {
        _triggerController = triggerController;
        _settingsService = settingsService;
        _logitechRuntimeStatusService = new LogitechRuntimeStatusService();
        _runtimeLaunchProfileService = new RuntimeLaunchProfileService();
        _runtimeGeminiClient = new GeminiClient();
        _showOrbDuringWorkflow = initialSettings.ShowOrbDuringWorkflow;
        _takeActionPromptPreference = initialSettings.TakeActionPromptPreference;
        _themeMode = initialSettings.ThemeMode;
        _talkTriggerInputMode = initialSettings.TalkTriggerInputMode;
        _playHapticSound = initialSettings.PlayHapticSound;
        InitializeComponent();

        _triggerController.OnActionChange += TriggerControllerOnActionChange;
        _triggerController.OnProcessingStart += TriggerControllerOnProcessingStart;
        _triggerController.OnProcessingComplete += TriggerControllerOnProcessingComplete;
        _triggerController.OnModeChanged += TriggerControllerOnModeChanged;
        CompanionThemeService.ThemeChanged += CompanionThemeServiceOnThemeChanged;
        _triggerController.SetShowOrbDuringWorkflow(_showOrbDuringWorkflow);
        _triggerController.SetTakeActionPromptPreference(_takeActionPromptPreference);
        _triggerController.SetTalkTriggerInputMode(_talkTriggerInputMode);

        SetModeCombo(initialSettings.Mode);
        SetTakeActionPromptCombo(_takeActionPromptPreference);
        SetThemeCombo(_themeMode);
        SetTalkTriggerInputCombo(_talkTriggerInputMode);
        ShowOrbDuringWorkflowCheckBox.IsChecked = _showOrbDuringWorkflow;
        PlayHapticSoundCheckBox.IsChecked = _playHapticSound;
        UpdateTalkTriggerUi();
        DataObject.AddPastingHandler(ApiKeyTextBox, ApiKeyTextBox_OnPaste);
        _ = LoadRuntimeApiKeyIntoTextboxAsync();
        _isModeInitialized = true;
        StatusText.Text = $"Status: Ready in {initialSettings.Mode} mode. Press Trigger for text flow.";
        _logitechStatusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _logitechStatusTimer.Tick += LogitechStatusTimerOnTick;
        RefreshLogitechRuntimeStatus();
        _logitechStatusTimer.Start();
        SourceInitialized += MainWindow_OnSourceInitialized;
        Deactivated += (_, _) =>
        {
            if (IsVisible)
            {
                Hide();
            }
        };
    }

    public event EventHandler<bool>? HapticSoundPreferenceChanged;

    protected override void OnClosed(EventArgs e)
    {
        CancelLongPressSession();
        UnregisterHotkeys();
        if (_hwndSource is not null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }

        SourceInitialized -= MainWindow_OnSourceInitialized;
        _logitechStatusTimer.Stop();
        _logitechStatusTimer.Tick -= LogitechStatusTimerOnTick;
        _triggerController.OnActionChange -= TriggerControllerOnActionChange;
        _triggerController.OnProcessingStart -= TriggerControllerOnProcessingStart;
        _triggerController.OnProcessingComplete -= TriggerControllerOnProcessingComplete;
        _triggerController.OnModeChanged -= TriggerControllerOnModeChanged;
        CompanionThemeService.ThemeChanged -= CompanionThemeServiceOnThemeChanged;
        DataObject.RemovePastingHandler(ApiKeyTextBox, ApiKeyTextBox_OnPaste);
        _runtimeGeminiClient.Dispose();
        base.OnClosed(e);
    }

    private async void TriggerButton_OnClick(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Status: Trigger pressed.";
        await _triggerController.HandleTapAsync(CancellationToken.None);
    }

    private async void TakeActionButton_OnClick(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Status: Take Action pressed.";
        await _triggerController.HandleTakeActionAsync(CancellationToken.None);
    }

    private void LongPressButton_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_talkTriggerInputMode == TalkTriggerInputMode.Text)
        {
            StatusText.Text = "Status: Text prompt opened for the talk trigger.";
            _ = _triggerController.HandleLongPressAsync(CancellationToken.None);
            e.Handled = true;
            return;
        }

        if (_longPressHoldTask is not null && !_longPressHoldTask.IsCompleted)
        {
            return;
        }

        CancelLongPressSession();
        _longPressHoldCts = new CancellationTokenSource();
        _longPressHoldTask = _triggerController.HandleLongPressAsync(_longPressHoldCts.Token);
        StatusText.Text = "Status: Listening... hold button, release to send.";
        if (sender is ButtonBase button)
        {
            button.CaptureMouse();
        }

        e.Handled = true;
    }

    private async void LongPressButton_OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_talkTriggerInputMode == TalkTriggerInputMode.Text)
        {
            e.Handled = true;
            return;
        }

        await FinalizeLongPressSessionAsync();
        if (sender is ButtonBase button)
        {
            button.ReleaseMouseCapture();
        }

        e.Handled = true;
    }

    private async void LongPressButton_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (_talkTriggerInputMode == TalkTriggerInputMode.Text)
        {
            return;
        }

        if (sender is not ButtonBase button || button.IsPressed)
        {
            return;
        }

        await FinalizeLongPressSessionAsync();
    }

    private async void MainWindow_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_talkTriggerInputMode == TalkTriggerInputMode.Text)
        {
            return;
        }

        if (_longPressHoldTask is null)
        {
            return;
        }

        await FinalizeLongPressSessionAsync();
    }

    private async void DialPressButton_OnClick(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Status: Image selection started.";
        await _triggerController.HandleImageSelectionAsync(CancellationToken.None);
    }

    private void ExitButton_OnClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private async void SetApiKeyButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingApiKey)
        {
            return;
        }

        var apiKey = ApiKeyTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            StatusText.Text = "Status: Enter a valid API key before pressing Set.";
            return;
        }

        _isUpdatingApiKey = true;
        SetApiKeyButton.IsEnabled = false;
        var originalContent = SetApiKeyButton.Content;
        SetApiKeyButton.Content = "Saving";

        try
        {
            await _runtimeGeminiClient.UpdateRuntimeApiKeyAsync(apiKey, CancellationToken.None);
            var saved = await _runtimeLaunchProfileService.UpdateApiKeysAsync(apiKey);
            StatusText.Text = saved
                ? "Status: API key updated for this session and future restarts."
                : "Status: API key updated for this session.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Status: Failed to update the API key. {ex.Message}";
        }
        finally
        {
            _isUpdatingApiKey = false;
            SetApiKeyButton.IsEnabled = true;
            SetApiKeyButton.Content = originalContent;
        }
    }

    private void DialSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressDialEvents)
        {
            return;
        }

        var current = (int)e.NewValue;
        var delta = current - _lastDialValue;
        if (delta == 0)
        {
            return;
        }

        _lastDialValue = current;
        _triggerController.HandleDialTick(delta);
    }

    private void MainWindow_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        _triggerController.CancelLassoPlaceholder();
        StatusText.Text = "Status: Lasso canceled.";
        e.Handled = true;
    }

    private void MainWindow_OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(handle);
        _hwndSource?.AddHook(WndProc);

        RegisterHotkey(handle, TriggerHotkeyId, ModControl | ModAlt, KeyInterop.VirtualKeyFromKey(Key.Space));
        RegisterHotkey(handle, TakeActionHotkeyId, ModControl | ModAlt, KeyInterop.VirtualKeyFromKey(Key.A));
        RegisterHotkey(handle, VoiceHotkeyId, ModControl | ModAlt, KeyInterop.VirtualKeyFromKey(Key.V));
    }

    private void RegisterHotkey(IntPtr handle, int id, uint modifiers, int virtualKey)
    {
        if (!NativeMethods.RegisterGlobalHotKey(handle, id, modifiers, (uint)virtualKey))
        {
            StatusText.Text = "Status: Some global hotkeys were unavailable. Buttons still work.";
        }
    }

    private void UnregisterHotkeys()
    {
        var handle = new WindowInteropHelper(this).Handle;
        NativeMethods.UnregisterGlobalHotKey(handle, TriggerHotkeyId);
        NativeMethods.UnregisterGlobalHotKey(handle, TakeActionHotkeyId);
        NativeMethods.UnregisterGlobalHotKey(handle, VoiceHotkeyId);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotKey)
        {
            return IntPtr.Zero;
        }

        handled = true;
        switch (wParam.ToInt32())
        {
            case TriggerHotkeyId:
                StatusText.Text = "Status: Hotkey trigger pressed.";
                _ = _triggerController.HandleTapAsync(CancellationToken.None);
                break;
            case TakeActionHotkeyId:
                StatusText.Text = "Status: Hotkey take action pressed.";
                _ = _triggerController.HandleTakeActionAsync(CancellationToken.None);
                break;
            case VoiceHotkeyId:
                StatusText.Text = _talkTriggerInputMode == TalkTriggerInputMode.Text
                    ? "Status: Hotkey text prompt opened."
                    : "Status: Hotkey talk trigger pressed.";
                _ = _triggerController.HandleLongPressAsync(CancellationToken.None);
                break;
        }

        return IntPtr.Zero;
    }

    private void TriggerControllerOnActionChange(object? sender, string action)
    {
        SelectedActionText.Text = $"Selected action: {action}";
    }

    private void TriggerControllerOnProcessingStart(object? sender, EventArgs e)
    {
        StatusText.Text = "Status: Processing...";
    }

    private void TriggerControllerOnProcessingComplete(object? sender, EventArgs e)
    {
        StatusText.Text = "Status: Completed and copied.";
        _suppressDialEvents = true;
        try
        {
            _lastDialValue = 0;
            DialSlider.Value = 0;
        }
        finally
        {
            _suppressDialEvents = false;
        }
    }

    private void CancelLongPressSession()
    {
        try
        {
            _longPressHoldCts?.Cancel();
        }
        catch
        {
            // Ignore cancellation race.
        }
    }

    private async Task FinalizeLongPressSessionAsync()
    {
        if (_longPressHoldTask is null)
        {
            return;
        }

        CancelLongPressSession();
        try
        {
            await _longPressHoldTask;
        }
        catch
        {
            // Trigger controller handles its own status updates/errors.
        }
        finally
        {
            _longPressHoldCts?.Dispose();
            _longPressHoldCts = null;
            _longPressHoldTask = null;
        }
    }

    private async void ModeCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isModeInitialized)
        {
            return;
        }

        if (ModeCombo.SelectedItem is not ComboBoxItem item || item.Tag is not string tag)
        {
            return;
        }

        if (!Enum.TryParse<InteractionMode>(tag, ignoreCase: true, out var mode))
        {
            return;
        }

        if (!_showOrbDuringWorkflow && mode == InteractionMode.Guided)
        {
            SetModeCombo(InteractionMode.Smart);
            StatusText.Text = "Status: Guided mode requires orb visibility, so Smart mode stayed active.";
            return;
        }

        _triggerController.SetInteractionMode(mode);
        await _settingsService.SaveModeAsync(mode);
        StatusText.Text = $"Status: Mode switched to {mode}.";
    }

    private async void ShowOrbDuringWorkflowCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (!_isModeInitialized)
        {
            return;
        }

        _showOrbDuringWorkflow = ShowOrbDuringWorkflowCheckBox.IsChecked == true;
        _triggerController.SetShowOrbDuringWorkflow(_showOrbDuringWorkflow);

        if (!_showOrbDuringWorkflow && ModeCombo.SelectedItem is ComboBoxItem item && string.Equals(item.Tag as string, "Guided", StringComparison.OrdinalIgnoreCase))
        {
            SetModeCombo(InteractionMode.Smart);
            _triggerController.SetInteractionMode(InteractionMode.Smart);
            await _settingsService.SaveModeAsync(InteractionMode.Smart);
        }

        await _settingsService.SaveShowOrbDuringWorkflowAsync(_showOrbDuringWorkflow);
        StatusText.Text = _showOrbDuringWorkflow
            ? "Status: Orb will appear during workflows and hide after completion."
            : "Status: Orb hidden for normal smart workflows; result panel stays primary.";
    }

    private async void TakeActionPromptCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isModeInitialized)
        {
            return;
        }

        if (TakeActionPromptCombo.SelectedItem is not ComboBoxItem item || item.Tag is not string tag)
        {
            return;
        }

        if (!Enum.TryParse<TakeActionPromptPreference>(tag, true, out var preference))
        {
            return;
        }

        _takeActionPromptPreference = preference;
        _triggerController.SetTakeActionPromptPreference(preference);
        await _settingsService.SaveTakeActionPromptPreferenceAsync(preference);
        StatusText.Text = preference == TakeActionPromptPreference.AlwaysAskToRun
            ? "Status: Result-panel Take Action will always show Run preview."
            : "Status: Result-panel Take Action will show confirmation without the Run preview.";
    }

    private async void ThemeCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isModeInitialized || _isUpdatingThemeSelection)
        {
            return;
        }

        if (ThemeCombo.SelectedItem is not ComboBoxItem item || item.Tag is not string tag)
        {
            return;
        }

        if (!Enum.TryParse<CompanionThemeMode>(tag, true, out var themeMode))
        {
            return;
        }

        _themeMode = themeMode;
        CompanionThemeService.Apply(themeMode);
        await _settingsService.SaveThemeModeAsync(themeMode);
        StatusText.Text = themeMode == CompanionThemeMode.Dark
            ? "Status: Dark appearance enabled."
            : "Status: Light appearance enabled.";
    }

    private async void TalkTriggerInputCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isModeInitialized)
        {
            return;
        }

        if (TalkTriggerInputCombo.SelectedItem is not ComboBoxItem item || item.Tag is not string tag)
        {
            return;
        }

        if (!Enum.TryParse<TalkTriggerInputMode>(tag, true, out var talkTriggerInputMode))
        {
            return;
        }

        _talkTriggerInputMode = talkTriggerInputMode;
        _triggerController.SetTalkTriggerInputMode(talkTriggerInputMode);
        UpdateTalkTriggerUi();
        await _settingsService.SaveTalkTriggerInputModeAsync(talkTriggerInputMode);
        StatusText.Text = talkTriggerInputMode == TalkTriggerInputMode.Text
            ? "Status: Talk trigger now opens a typed prompt beside the orb."
            : "Status: Talk trigger now records voice input again.";
    }

    private async void PlayHapticSoundCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (!_isModeInitialized)
        {
            return;
        }

        _playHapticSound = PlayHapticSoundCheckBox.IsChecked == true;
        HapticSoundPreferenceChanged?.Invoke(this, _playHapticSound);
        await _settingsService.SavePlayHapticSoundAsync(_playHapticSound);
        StatusText.Text = _playHapticSound
            ? "Status: Companion sound will play alongside Logitech haptics."
            : "Status: Companion sound muted. Logitech haptics remain active.";
    }

    private void CompanionThemeServiceOnThemeChanged(object? sender, CompanionThemeMode themeMode)
    {
        _themeMode = themeMode;

        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.InvokeAsync(() => SetThemeCombo(themeMode));
            return;
        }

        SetThemeCombo(themeMode);
    }

    private void TriggerControllerOnModeChanged(object? sender, InteractionMode mode)
    {
        SetModeCombo(mode);
    }

    private void LogitechStatusTimerOnTick(object? sender, EventArgs e)
    {
        RefreshLogitechRuntimeStatus();
    }

    private void RefreshLogitechRuntimeStatus()
    {
        var snapshot = _logitechRuntimeStatusService.GetSnapshot();

        LogitechOptionsStatusText.Text = snapshot.OptionsRunning
            ? "Running"
            : snapshot.OptionsInstalled
                ? "Installed"
                : "Missing";

        LogitechPluginServiceStatusText.Text = snapshot.PluginServiceRunning ? "Running" : "Offline";

        LogitechPluginStatusText.Text = snapshot.PluginLoaded
            ? "Loaded"
            : snapshot.PluginInstalled
                ? "Installed"
                : snapshot.DebugLinkPresent
                    ? "Debug Link"
                    : "Not Found";

        LogitechHapticStatusText.Text = snapshot.HapticConnected ? "Connected" : "Waiting";
        LogitechRuntimeModeText.Text = snapshot.RuntimeMode;
    }

    private void SetModeCombo(InteractionMode mode)
    {
        foreach (var item in ModeCombo.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag is string tag && string.Equals(tag, mode.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                ModeCombo.SelectedItem = item;
                return;
            }
        }

        ModeCombo.SelectedIndex = 0;
    }

    private void SetTakeActionPromptCombo(TakeActionPromptPreference preference)
    {
        foreach (var item in TakeActionPromptCombo.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag is string tag && string.Equals(tag, preference.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                TakeActionPromptCombo.SelectedItem = item;
                return;
            }
        }

        TakeActionPromptCombo.SelectedIndex = 0;
    }

    private void SetThemeCombo(CompanionThemeMode themeMode)
    {
        _isUpdatingThemeSelection = true;
        try
        {
            foreach (var item in ThemeCombo.Items.OfType<ComboBoxItem>())
            {
                if (item.Tag is string tag && string.Equals(tag, themeMode.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    ThemeCombo.SelectedItem = item;
                    return;
                }
            }

            ThemeCombo.SelectedIndex = 0;
        }
        finally
        {
            _isUpdatingThemeSelection = false;
        }
    }

    private void SetTalkTriggerInputCombo(TalkTriggerInputMode talkTriggerInputMode)
    {
        foreach (var item in TalkTriggerInputCombo.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag is string tag && string.Equals(tag, talkTriggerInputMode.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                TalkTriggerInputCombo.SelectedItem = item;
                return;
            }
        }

        TalkTriggerInputCombo.SelectedIndex = 0;
    }

    private void UpdateTalkTriggerUi()
    {
        LongPressButton.Content = _talkTriggerInputMode == TalkTriggerInputMode.Text
            ? "Text Trigger"
            : "Hold to Talk";
        HotkeysText.Text = _talkTriggerInputMode == TalkTriggerInputMode.Text
            ? "Hotkeys: Ctrl+Alt+Space = Trigger   |   Ctrl+Alt+A = Take Action   |   Ctrl+Alt+V = Text Trigger"
            : "Hotkeys: Ctrl+Alt+Space = Trigger   |   Ctrl+Alt+A = Take Action   |   Ctrl+Alt+V = Talk";
    }

    private async Task LoadRuntimeApiKeyIntoTextboxAsync()
    {
        try
        {
            var profile = await _runtimeLaunchProfileService.TryLoadAsync();
            if (profile is null)
            {
                return;
            }

            ApiKeyTextBox.Text = !string.IsNullOrWhiteSpace(profile.ApiKeys)
                ? profile.ApiKeys
                : profile.ApiKey;
            ResetApiKeyViewport();
        }
        catch
        {
            // Keep the dev field empty if profile loading fails.
        }
    }

    public void ShowForSettings()
    {
        Opacity = 1;
        ShowInTaskbar = true;

        if (!IsVisible)
        {
            Show();
        }

        WindowState = WindowState.Normal;
        Topmost = true;
        Activate();
        Focus();

        _ = Dispatcher.BeginInvoke(() =>
        {
            ApiKeyTextBox.Focus();
            ResetApiKeyViewport();
        }, DispatcherPriority.Input);
    }

    private void ApiKeyTextBox_OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        _ = Dispatcher.BeginInvoke(
            ResetApiKeyViewport,
            DispatcherPriority.Background);
    }

    private void ResetApiKeyViewport()
    {
        ApiKeyTextBox.CaretIndex = 0;
        ApiKeyTextBox.Select(0, 0);
        ApiKeyTextBox.ScrollToHome();
    }
}
