using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace WeaselSettings.Avalonia.Views.Dialogs;

public partial class MessageDialog : Window
{
    private readonly TaskCompletionSource<bool?> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public MessageDialog()
    {
        InitializeComponent();
    }

    public bool? Result { get; private set; }

    public Task<bool?> ShowDialogAsync(Window owner)
    {
        _ = ShowDialog(owner);
        return _tcs.Task;
    }

    public static MessageDialog Create(string title, string message, bool yesNo)
    {
        var dialog = new MessageDialog
        {
            Title = title,
            Result = null
        };
        dialog.MessageText.Text = message;

        if (yesNo)
        {
            dialog.ButtonsPanel.Children.Add(CreateButton("是", true, dialog));
            dialog.ButtonsPanel.Children.Add(CreateButton("否", false, dialog));
        }
        else
        {
            dialog.ButtonsPanel.Children.Add(CreateButton("确定", true, dialog));
        }

        return dialog;
    }

    private static Button CreateButton(string text, bool result, MessageDialog dialog)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 76,
            Padding = new Thickness(12, 4)
        };
        button.Click += (_, _) =>
        {
            dialog.Result = result;
            dialog.Close();
        };
        return button;
    }

    protected override void OnClosed(EventArgs e)
    {
        _tcs.TrySetResult(Result);
        base.OnClosed(e);
    }
}
