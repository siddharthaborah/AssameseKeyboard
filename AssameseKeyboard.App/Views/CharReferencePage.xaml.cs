// =============================================================================
// FILE: AssameseKeyboard.App/Views/CharReferencePage.xaml.cs
// =============================================================================
using AssameseKeyboard.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace AssameseKeyboard.App.Views;

public sealed partial class CharReferencePage : Page
{
    public KeyboardPreviewViewModel ViewModel { get; }

    public CharReferencePage()
    {
        // Reuses KeyboardPreviewViewModel which already holds CharTable
        ViewModel = App.Services.GetRequiredService<KeyboardPreviewViewModel>();
        InitializeComponent();
    }
}