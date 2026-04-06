// =============================================================================
// FILE: AssameseKeyboard.App/Services/TrayService.cs
// INSTRUCTION:
//   Create folder "Services" inside AssameseKeyboard.App.
//   Add this file inside it.
//   Requires NuGet: H.NotifyIcon.WinUI (version 2.1.0)
//
// IMPORTANT — H.NotifyIcon setup:
//   After installing the package, add this to App.xaml resources if needed:
//   The package provides TaskbarIcon which hosts the tray icon.
// =============================================================================

using AssameseKeyboard.Core.Services;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;

namespace AssameseKeyboard.App.Services;

/// <summary>
/// Build-safe background integration service.
/// </summary>
public sealed class TrayService : IDisposable
{
    private const string IconOnPath = "ms-appx:///Assets/tray-on.ico";
    private const string IconOffPath = "ms-appx:///Assets/tray-off.ico";

    // ── Dependencies ──────────────────────────────────────────────────────────
    private readonly KeyboardEngineService _engine;
    private readonly ILogger<TrayService> _logger;

    // ── State ─────────────────────────────────────────────────────────────────
    private TaskbarIcon? _trayIcon;
    private ToggleMenuFlyoutItem? _toggleMenuItem;
    private bool _disposed;

    // ── Constructor ───────────────────────────────────────────────────────────

    public TrayService(
        KeyboardEngineService engine,
        ILogger<TrayService> logger)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Keep the tray icon in sync when the engine state changes externally
        _engine.RunningStateChanged += OnEngineStateChanged;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates and shows the tray icon with its context menu.
    /// Must be called on the UI thread.
    /// </summary>
    public void Initialize()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Assamese Keyboard — Active",
            ContextMenuMode = ContextMenuMode.PopupMenu,
        };

        _trayIcon.ContextFlyout = BuildContextMenu();
        UpdateIcon(_engine.IsRunning);
        _trayIcon.ForceCreate();

        _logger.LogInformation("[TrayService] Tray icon initialized.");
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private MenuFlyout BuildContextMenu()
    {
        var flyout = new MenuFlyout();

        _toggleMenuItem = new ToggleMenuFlyoutItem
        {
            Text = "Assamese Input Active",
            IsChecked = _engine.IsRunning,
        };
        _toggleMenuItem.Click += OnToggleClicked;
        flyout.Items.Add(_toggleMenuItem);

        flyout.Items.Add(new MenuFlyoutSeparator());

        var settingsItem = new MenuFlyoutItem { Text = "Settings..." };
        settingsItem.Click += (_, _) => OpenSettings();
        flyout.Items.Add(settingsItem);

        var exitItem = new MenuFlyoutItem { Text = "Exit" };
        exitItem.Click += (_, _) => ExitApp();
        flyout.Items.Add(exitItem);

        return flyout;
    }

    private void OnToggleClicked(object sender, RoutedEventArgs e)
    {
        if (_engine.IsRunning)
            _engine.Stop();
        else
            _engine.Start();
    }

    private void OnEngineStateChanged(object? sender, bool isRunning)
    {
        if (_toggleMenuItem is not null)
            _toggleMenuItem.IsChecked = isRunning;

        UpdateIcon(isRunning);
    }

    private void UpdateIcon(bool active)
    {
        if (_trayIcon is null) return;

        _trayIcon.IconSource = new BitmapImage(new Uri(active ? IconOnPath : IconOffPath));
        _trayIcon.ToolTipText = active
            ? "Assamese Keyboard — Active"
            : "Assamese Keyboard — Paused";
    }

    private static void OpenSettings()
    {
        var win = App.Services.GetRequiredService<MainWindow>();
        win.Activate();
    }

    private static void ExitApp()
    {
        var engine = App.Services.GetRequiredService<KeyboardEngineService>();
        engine.Stop();
        engine.Dispose();
        Microsoft.UI.Xaml.Application.Current.Exit();
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;

        _engine.RunningStateChanged -= OnEngineStateChanged;
        _trayIcon?.Dispose();
        _trayIcon = null;
        _disposed = true;
    }
}