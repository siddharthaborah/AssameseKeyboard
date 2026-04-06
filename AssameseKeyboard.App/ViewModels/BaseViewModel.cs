// =============================================================================
// FILE: AssameseKeyboard.App/ViewModels/BaseViewModel.cs
// INSTRUCTION: Create folder "ViewModels" in AssameseKeyboard.App.
//              Add this file inside it.
//              Requires NuGet: CommunityToolkit.Mvvm (version 8.2.2)
// =============================================================================

using CommunityToolkit.Mvvm.ComponentModel;

namespace AssameseKeyboard.App.ViewModels;

/// <summary>
/// Base class for all ViewModels.
/// Inherits ObservableObject from CommunityToolkit.Mvvm which provides
/// INotifyPropertyChanged and source-generator support for [ObservableProperty].
/// </summary>
public abstract class BaseViewModel : ObservableObject { }