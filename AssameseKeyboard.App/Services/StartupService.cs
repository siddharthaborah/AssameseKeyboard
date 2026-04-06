// =============================================================================
// FILE: AssameseKeyboard.App/Services/StartupService.cs
// INSTRUCTION: Add to the "Services" folder in AssameseKeyboard.App.
// =============================================================================

using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;

namespace AssameseKeyboard.App.Services;

/// <summary>
/// Controls whether the app starts automatically with Windows
/// by writing to / removing from HKCU\SOFTWARE\Microsoft\Windows\
/// CurrentVersion\Run in the registry.
///
/// Uses the user-level Run key (HKCU) rather than the machine-level
/// key (HKLM) to avoid requiring administrator privileges.
/// </summary>
public sealed class StartupService
{
    private const string RunKeyPath =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    private const string ValueName = "AssameseKeyboard";

    private readonly ILogger<StartupService> _logger;

    public StartupService(ILogger<StartupService> logger)
        => _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when a Windows startup entry exists for this app.
    /// </summary>
    public bool IsStartupEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                return key?.GetValue(ValueName) is not null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[StartupService] Failed to read startup registry key.");
                return false;
            }
        }
    }

    /// <summary>
    /// Enables or disables Windows startup.
    /// </summary>
    /// <param name="enable">True to add startup entry, false to remove it.</param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when the registry write fails.
    /// </exception>
    public void SetStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? throw new InvalidOperationException(
                    "[StartupService] Cannot open Run registry key for writing. " +
                    "This should never happen for HKCU.");

            if (enable)
            {
                // Wrap in quotes to handle paths with spaces
                string exePath = Environment.ProcessPath
                    ?? System.Diagnostics.Process
                       .GetCurrentProcess().MainModule!.FileName;

                key.SetValue(ValueName, $"\"{exePath}\"",
                    RegistryValueKind.String);

                _logger.LogInformation(
                    "[StartupService] Startup enabled. Path: {Path}", exePath);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                _logger.LogInformation("[StartupService] Startup disabled.");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            var msg = $"[StartupService] Failed to {(enable ? "enable" : "disable")} " +
                      "startup entry.";
            _logger.LogError(ex, msg);
            throw new InvalidOperationException(msg, ex);
        }
    }
}