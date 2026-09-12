using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using WeaselSettings.Avalonia.Models;
using WeaselSettings.Avalonia.Services;

namespace WeaselSettings.Avalonia.Views;

/// <summary>
/// 五笔用户词库管理窗口，对应 WinForms 的 WubiDict（词库管理工具）。
/// </summary>
public partial class WubiDictWindow : Window
{
    private readonly DispatcherTimer _searchTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private string _originalHeader = string.Empty;
    private readonly string _dictFilePath;
    private readonly List<DictItem> _allItems = new();
    private List<DictItem> _currentView = new();
    private readonly Dictionary<string, HashSet<string>> _mainDictIndex = new();

    public WubiDictWindow()
    {
        InitializeComponent();

        string userDir = YamlHelper.GetUserDataDir();
        _dictFilePath = Path.Combine(userDir, "wubi.extended.dict.yaml");

        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            PerformSearch(txtFilter.Text?.Trim() ?? string.Empty);
        };
        txtFilter.TextChanged += (_, _) =>
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        };
        btnAdd.Click += BtnAdd_Click;
        btnDelete.Click += BtnDelete_Click;
        btnImport.Click += BtnImport_Click;
        btnExport.Click += BtnExport_Click;
        btnClear.Click += BtnClear_Click;
        btnOk.Click += BtnOk_Click;
        btnCancel.Click += (_, _) => Close();
        dgvList.DoubleTapped += OnGridDoubleTapped;

        if (!File.Exists(_dictFilePath))
        {
            _ = UiDialogs.ShowErrorAsync(this, $"未找到词库文件：\r\n{_dictFilePath}");
            Close();
            return;
        }

        LoadMainDictIndex();
        LoadData();
    }

    private void LoadData()
    {
        var result = RimeDictParser.LoadDict(_dictFilePath);
        _originalHeader = result.Header;

        _allItems.Clear();
        _allItems.AddRange(result.Items);
        PerformSearch(string.Empty);
    }

    private void PerformSearch(string keyword)
    {
        if (string.IsNullOrEmpty(keyword))
        {
            _currentView = _allItems.ToList();
        }
        else
        {
            _currentView = _allItems
                .Where(x => x.Code.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                            x.Word.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        dgvList.ItemsSource = null;
        dgvList.ItemsSource = _currentView;
        lblPageInfo.Text = $"共 {_currentView.Count} 条数据";
    }

    private void BtnOk_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            RimeDictParser.SaveDict(_dictFilePath, _originalHeader, _allItems);
            _ = UiDialogs.ShowInfoAsync(this, "保存成功，点击下方保存后才能生效。");
            Close();
        }
        catch (Exception ex)
        {
            _ = UiDialogs.ShowErrorAsync(this, "保存失败：" + ex.Message);
        }
    }

    private async void BtnAdd_Click(object? sender, RoutedEventArgs e)
    {
        var addForm = new AddDictWindow(_allItems, _mainDictIndex);
        var newItem = await addForm.ShowAsync(this);
        if (newItem != null)
        {
            _allItems.Add(newItem);
            PerformSearch(txtFilter.Text?.Trim() ?? string.Empty);
        }
    }

    private void BtnDelete_Click(object? sender, RoutedEventArgs e)
    {
        var selected = dgvList.SelectedItems.Cast<DictItem>().ToList();
        foreach (var item in selected)
            _allItems.Remove(item);

        PerformSearch(txtFilter.Text?.Trim() ?? string.Empty);
    }

    private async void BtnImport_Click(object? sender, RoutedEventArgs e)
    {
        var path = await UiDialogs.PickOpenFileAsync(this, "选择词库文本", ("文本文件 (UTF8无BOM.txt)", ["*.txt"]));
        if (path is null)
            return;

        int imported = 0, skipped = 0;
        using var reader = new StreamReader(path, new System.Text.UTF8Encoding(false));
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            if (!TryParseDictLine(line, out var item))
                continue;

            if (_mainDictIndex.TryGetValue(item.Word, out var codes) && codes.Contains(item.Code))
            {
                skipped++;
                continue;
            }

            if (_allItems.Any(x => x.Word == item.Word && x.Code == item.Code))
            {
                skipped++;
                continue;
            }

            _allItems.Add(item);
            imported++;
        }

        PerformSearch(txtFilter.Text?.Trim() ?? string.Empty);
        await UiDialogs.ShowInfoAsync(this, $"导入完成\r\n成功：{imported}\r\n跳过：{skipped}");
    }

    private async void BtnExport_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentView.Count == 0)
        {
            await UiDialogs.ShowInfoAsync(this, "没有可导出的数据");
            return;
        }

        var path = await UiDialogs.PickSaveFileAsync(this, "导出词库", "wubi_export.txt", ("文本文件 (*.txt)", ["*.txt"]));
        if (path is null)
            return;

        using var writer = new StreamWriter(path, false, new System.Text.UTF8Encoding(false));
        foreach (var item in _currentView)
        {
            if (item.Weight > 0)
                writer.WriteLine($"{item.Code}\t{item.Word}\t{item.Weight}");
            else
                writer.WriteLine($"{item.Code}\t{item.Word}");
        }

        await UiDialogs.ShowInfoAsync(this, $"成功导出 {_currentView.Count} 条数据");
    }

    private async void BtnClear_Click(object? sender, RoutedEventArgs e)
    {
        if (await UiDialogs.ConfirmAsync(this, "确定清空所有自定义词条？", "警告"))
        {
            _allItems.Clear();
            PerformSearch(string.Empty);
        }
    }

    private async void OnGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (dgvList.SelectedItem is not DictItem selectedItem)
            return;

        var editForm = new AddDictWindow(_allItems, _mainDictIndex, selectedItem);
        var newItem = await editForm.ShowAsync(this);
        if (newItem != null)
        {
            selectedItem.Word = newItem.Word;
            selectedItem.Code = newItem.Code;
            selectedItem.Weight = newItem.Weight;
            PerformSearch(txtFilter.Text?.Trim() ?? string.Empty);
        }
    }

    private void LoadMainDictIndex()
    {
        string path = Path.Combine(YamlHelper.GetUserDataDir(), "wubi.dict.yaml");
        if (!File.Exists(path))
            return;

        using var reader = new StreamReader(path, new System.Text.UTF8Encoding(false));
        string? line;
        bool body = false;

        while ((line = reader.ReadLine()) != null)
        {
            if (!body)
            {
                if (line.Trim() == "...")
                    body = true;
                continue;
            }

            if (!TryParseDictLine(line, out var item))
                continue;

            if (!_mainDictIndex.TryGetValue(item.Word, out var set))
                _mainDictIndex[item.Word] = set = new HashSet<string>();
            set.Add(item.Code);
        }
    }

    private static bool TryParseDictLine(string line, out DictItem item)
    {
        item = null!;

        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            return false;

        var parts = line.Split(['\t'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return false;

        string? word = null;
        string? code = null;
        int weight = 0;

        foreach (var part in parts)
        {
            if (int.TryParse(part, out var w))
            {
                weight = w;
                continue;
            }

            if (IsCode(part))
            {
                code ??= part;
                continue;
            }

            word ??= part;
        }

        if (string.IsNullOrEmpty(word) || string.IsNullOrEmpty(code))
            return false;

        item = new DictItem
        {
            Word = word,
            Code = code,
            Weight = weight
        };
        return true;
    }

    private static bool IsCode(string text) =>
        !string.IsNullOrEmpty(text) && text.All(c => c is >= 'a' and <= 'z');

    private static class RimeDictParser
    {
        public static (string Header, List<DictItem> Items) LoadDict(string filePath)
        {
            var items = new List<DictItem>();
            var header = new System.Text.StringBuilder();

            using var reader = new StreamReader(filePath, new System.Text.UTF8Encoding(false));
            string? line;
            bool body = false;

            while ((line = reader.ReadLine()) != null)
            {
                if (!body)
                {
                    header.AppendLine(line);
                    if (line.Trim() == "...")
                        body = true;
                    continue;
                }

                if (TryParseDictLine(line, out var item))
                    items.Add(item);
            }

            return (header.ToString(), items);
        }

        public static void SaveDict(string filePath, string header, List<DictItem> items)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(header);

            if (!header.TrimEnd().EndsWith("..."))
                sb.AppendLine("...");

            foreach (var item in items)
            {
                if (item.Weight > 0)
                    sb.AppendLine($"{item.Word}\t{item.Code}\t{item.Weight}");
                else
                    sb.AppendLine($"{item.Word}\t{item.Code}");
            }

            File.WriteAllText(filePath, sb.ToString(), new System.Text.UTF8Encoding(false));
        }
    }
}
