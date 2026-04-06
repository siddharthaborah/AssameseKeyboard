// =============================================================================
// FILE: AssameseKeyboard.App/Views/KeyboardPreviewPage.xaml.cs
// =============================================================================
using AssameseKeyboard.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace AssameseKeyboard.App.Views;

public sealed partial class KeyboardPreviewPage : Page
{
    public KeyboardPreviewViewModel ViewModel { get; }

    public KeyboardPreviewPage()
    {
        ViewModel = App.Services.GetRequiredService<KeyboardPreviewViewModel>();
        InitializeComponent();
    }
}