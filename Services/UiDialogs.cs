using Avalonia.Controls;
using Avalonia.Platform.Storage;
using WeaselSettings.Avalonia.Views.Dialogs;

namespace WeaselSettings.Avalonia.Services;

/// <summary>
/// 轻量对话框辅助：消息框、确认框、文件选择。
/// Avalonia 没有内置 MessageBox，这里用自定义 MessageDialog 实现同等体验。
/// </summary>
public static class UiDialogs
{
    public static Task ShowInfoAsync(Window owner, string message, string title = "提示")
        => MessageDialog.Create(title, message, yesNo: false).ShowDialogAsync(owner);

    public static Task ShowErrorAsync(Window owner, string message, string title = "错误")
        => MessageDialog.Create(title, message, yesNo: false).ShowDialogAsync(owner);

    public static Task<bool> ConfirmAsync(Window owner, string message, string title = "提示")
        => MessageDialog.Create(title, message, yesNo: true).ShowDialogAsync(owner).ContinueWith(t => t.Result == true);

    public static async Task<string?> PickOpenFileAsync(
        Window owner,
        string title,
        params (string Name, string[] Patterns)[] filters)
    {
        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = filters.Select(f => new FilePickerFileType(f.Name) { Patterns = f.Patterns }).ToList()
        };

        var files = await owner.StorageProvider.OpenFilePickerAsync(options);
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public static async Task<string?> PickSaveFileAsync(
        Window owner,
        string title,
        string suggestedFileName,
        params (string Name, string[] Patterns)[] filters)
    {
        var options = new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = filters.Select(f => new FilePickerFileType(f.Name) { Patterns = f.Patterns }).ToList()
        };

        var file = await owner.StorageProvider.SaveFilePickerAsync(options);
        return file?.TryGetLocalPath();
    }
}
