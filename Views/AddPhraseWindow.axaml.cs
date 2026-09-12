using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using WeaselSettings.Avalonia.Models;

namespace WeaselSettings.Avalonia.Views;

/// <summary>
/// 新增/编辑短语窗口，对应 WinForms 的 AddWordForm（短语管理工具）。
/// </summary>
public partial class AddPhraseWindow : Window
{
    private readonly List<DictItem> _extItems;
    private readonly DictItem? _editingItem;

    public AddPhraseWindow(List<DictItem> extItems, DictItem? targetItem = null)
    {
        InitializeComponent();
        _extItems = extItems;
        _editingItem = targetItem;

        txtCode.TextChanged += (_, _) => OnCodeChanged();
        txtWord.TextChanged += (_, _) => CheckCanSave();
        btnOk.Click += OnOkClick;
        btnCancel.Click += (_, _) => Close();

        if (_editingItem != null)
        {
            Title = "编辑短语";
            txtCode.Text = _editingItem.Code;
            txtWord.Text = _editingItem.Word.Replace("\\n", Environment.NewLine);
            lblDupItems.Text = "正在编辑现有短语";
            lblDupItems.Foreground = Brushes.Gray;
            txtWord.IsEnabled = true;
        }
        else
        {
            lblDupItems.Text = "请先输入快捷编码";
            lblDupItems.Foreground = Brushes.Gray;
        }
    }

    public DictItem? NewItem { get; private set; }

    public async Task<DictItem?> ShowAsync(Window owner)
    {
        await ShowDialog(owner);
        return NewItem;
    }

    private void OnCodeChanged()
    {
        string code = txtCode.Text?.Trim().ToLowerInvariant() ?? string.Empty;

        if (string.IsNullOrEmpty(code))
        {
            lblDupItems.Text = "编码不能为空";
            lblDupItems.Foreground = Brushes.Gray;
            txtWord.IsEnabled = false;
            CheckCanSave();
            return;
        }

        //bool isOriginalCode = _editingItem != null &&
        //                      _editingItem.Code.Equals(code, StringComparison.OrdinalIgnoreCase);

        //var duplicateItem = _extItems.FirstOrDefault(i =>
        //    !isOriginalCode && i.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

        //if (duplicateItem != null)
        //{
        //    lblDupItems.Text = $"⚠ 编码已占用: {duplicateItem.Word}";
        //    lblDupItems.Foreground = Brushes.Red;
        //    txtWord.IsEnabled = false;
        //}
        //else
        //{
        //    lblDupItems.Text = isOriginalCode ? "✅ 原始编码（未修改）" : "✅ 编码可用，请输入短语内容";
        //    lblDupItems.Foreground = Brushes.Green;
            txtWord.IsEnabled = true;
        //}

        CheckCanSave();
    }

    private void CheckCanSave()
    {
        btnOk.IsEnabled = txtWord.IsEnabled && !string.IsNullOrWhiteSpace(txtWord.Text);
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        string finalWord = (txtWord.Text ?? string.Empty).Trim()
            .Replace("\r\n", "\\n")
            .Replace("\n", "\\n");

        NewItem = new DictItem
        {
            Word = finalWord,
            Code = (txtCode.Text ?? string.Empty).Trim().ToLowerInvariant(),
            IsNew = true
        };
        Close();
    }
}
