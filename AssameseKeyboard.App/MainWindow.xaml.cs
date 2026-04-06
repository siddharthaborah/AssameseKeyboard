// =============================================================================
// FILE: AssameseKeyboard.App/MainWindow.xaml.cs
// =============================================================================

using AssameseKeyboard.App.Views;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT;

namespace AssameseKeyboard.App;

public sealed partial class MainWindow : Window
{
    private MicaController? _micaController;
    private SystemBackdropConfiguration? _backdropConfig;

    public MainWindow()
    {
        InitializeComponent();
        SetWindowProperties();
        TrySetMicaBackdrop();
    }

    // ── Window setup ──────────────────────────────────────────────────────────

    private void SetWindowProperties()
    {
        Title = "Assamese Keyboard";
        ExtendsContentIntoTitleBar = true;

        // Set minimum window size via AppWindow
        if (AppWindow is not null)
        {
            var presenter = AppWindow.Presenter as OverlappedPresenter;
            if (presenter is not null)
            {
                presenter.IsResizable = true;
                presenter.IsMaximizable = true;
                presenter.IsMinimizable = true;
            }
        }
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        // Select Keyboard Layout by default
        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(KeyboardPreviewPage));
    }

    private void NavView_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item) return;

        Type? pageType = item.Tag?.ToString() switch
        {
            "KeyboardPreview" => typeof(KeyboardPreviewPage),
            "CharReference" => typeof(CharReferencePage),
            "Settings" => typeof(SettingsPage),
            "About" => typeof(AboutPage),
            _ => null,
        };

        if (pageType is not null &&
            ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }

    // ── Mica backdrop (Windows 11) ─────────────────────────────────────────────

    private bool TrySetMicaBackdrop()
    {
        if (!MicaController.IsSupported()) return false;

        _backdropConfig = new SystemBackdropConfiguration
        {
            IsInputActive = true,
            Theme = SystemBackdropTheme.Default,
        };

        _micaController = new MicaController();
        _micaController.AddSystemBackdropTarget(
            this.As<ICompositionSupportsSystemBackdrop>());
        _micaController.SetSystemBackdropConfiguration(_backdropConfig);

        Activated += (_, e) =>
        {
            if (_backdropConfig is not null)
                _backdropConfig.IsInputActive =
                    e.WindowActivationState != WindowActivationState.Deactivated;
        };

        Closed += (_, _) =>
        {
            _micaController?.Dispose();
            _micaController = null;
        };

        return true;
    }
}