using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using HLX.App.ViewModels;

namespace HLX.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel(PickFileAsync);
    }

    private async Task<string?> PickFileAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open HashLink Module",
            FileTypeFilter =
            [
                new FilePickerFileType("HashLink Bytecode") { Patterns = ["*.dat"] },
                new FilePickerFileType("All Files") { Patterns = ["*"] }
            ],
            AllowMultiple = false
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is MainWindowViewModel vm)
            vm.SearchQuery = "";
    }

    private void OnClearSearch(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.SearchQuery = "";
    }
}
