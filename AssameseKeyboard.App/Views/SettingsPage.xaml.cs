// =============================================================================
// FILE: AssameseKeyboard.App/Views/SettingsPage.xaml.cs
// =============================================================================
using AssameseKeyboard.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace AssameseKeyboard.App.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        InitializeComponent();
    }
}