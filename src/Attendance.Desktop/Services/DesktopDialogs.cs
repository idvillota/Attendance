using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Attendance.Desktop.Services;

internal static class DesktopDialogs
{
    public static async Task<bool> ShowEditorAsync<TWindow>(Control host, object viewModel)
        where TWindow : Window, new()
    {
        if (TopLevel.GetTopLevel(host) is not Window owner)
        {
            return false;
        }

        var dialog = new TWindow { DataContext = viewModel };
        return await dialog.ShowDialog<bool>(owner);
    }

    public static async Task<string?> SaveFileAsync(Control host, string title, string suggestedFileName, string typeName, params string[] patterns)
    {
        var top = TopLevel.GetTopLevel(host);
        if (top is null)
        {
            return null;
        }

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = [new FilePickerFileType(typeName) { Patterns = [.. patterns] }]
        });

        return file?.Path.LocalPath;
    }

    public static async Task<string?> OpenFileAsync(Control host, string title, string typeName, params string[] patterns)
    {
        var top = TopLevel.GetTopLevel(host);
        if (top is null)
        {
            return null;
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(typeName) { Patterns = [.. patterns] }]
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }
}
