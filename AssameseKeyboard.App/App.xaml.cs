// =============================================================================
// FILE: AssameseKeyboard.App/App.xaml.cs
// INSTRUCTION:
//   Replace the entire auto-generated App.xaml.cs content with this.
//   Ensure all NuGet packages are installed before building.
// =============================================================================

using AssameseKeyboard.App.Services;
using AssameseKeyboard.Core.Hook;
using AssameseKeyboard.Core.Injection;
using AssameseKeyboard.Core.Mapping;
using AssameseKeyboard.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using System;

namespace AssameseKeyboard.App;

/// <summary>
/// Application entry point.
/// Sets up the DI container, loads the keyboard layout,
/// starts the engine, and shows the system tray icon.
/// No window is shown on startup — the app lives in the tray.
/// </summary>
public partial class App : Application
{
    // ── Public access to DI container ─────────────────────────────────────────

    /// <summary>
    /// The application-wide service provider.
    /// Initialised before OnLaunched() is called.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>Convenience accessor for the current App instance.</summary>
    public new static App Current => (App)Application.Current;

    // ── Private fields ────────────────────────────────────────────────────────

    private TrayService? _tray;
    private KeyboardEngineService? _engine;

    // ── Constructor ───────────────────────────────────────────────────────────

    public App()
    {
        InitializeComponent();
        Services = BuildServiceProvider();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // 1. Load the default Assamese layout into the mapper
        var mapper = Services.GetRequiredService<KeyMapper>();
        mapper.LoadEmbeddedDefault();

        // 2. Start the keyboard engine (installs the WH_KEYBOARD_LL hook)
        _engine = Services.GetRequiredService<KeyboardEngineService>();
        _engine.Start();

        // 3. Initialise the tray icon — this is the app's only visible surface
        _tray = Services.GetRequiredService<TrayService>();
        _tray.Initialize();
    }

    // ── DI container ──────────────────────────────────────────────────────────

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        // ── Logging ───────────────────────────────────────────────────────────
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(
#if DEBUG
                LogLevel.Debug);
            builder.AddDebug();
#else
                LogLevel.Information);
#endif
        });

        // ── Core singletons ───────────────────────────────────────────────────
        // All core services are singletons — one instance for the entire app lifetime.
        services.AddSingleton<KeyboardHook>();
        services.AddSingleton<InputInjector>();
        services.AddSingleton<KeyMapper>();
        services.AddSingleton<ShiftStateTracker>();
        services.AddSingleton<JuktakkhorEngine>();
        services.AddSingleton<KeyboardEngineService>();

        // ── App services ──────────────────────────────────────────────────────
        services.AddSingleton<TrayService>();
        services.AddSingleton<StartupService>();

        // ── Views and ViewModels ──────────────────────────────────────────────
        // MainWindow is transient — a new instance each time Settings is opened
        services.AddTransient<MainWindow>();

        // ViewModels are transient — fresh state each time a page is navigated to
        services.AddTransient<ViewModels.SettingsViewModel>();
        services.AddTransient<ViewModels.KeyboardPreviewViewModel>();

        return services.BuildServiceProvider();
    }
}