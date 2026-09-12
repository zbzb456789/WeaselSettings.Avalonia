using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using WeaselSettings.Avalonia.Models;
using WeaselSettings.Avalonia.Services;

namespace WeaselSettings.Avalonia.Views;

/// <summary>
/// 用户短语词库管理窗口，对应 WinForms 的 Phrase（短语管理工具）。
/// </summary>
public partial class PhraseWindow : Window
{
    private readonly DispatcherTimer _searchTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly List<DictItem> _allItems = new();
    private List<DictItem> _currentList = new();
    private readonly string _dictFilePath;
    private readonly Dictionary<string, HashSet<string>> _mainDictIndex = new();

    public PhraseWindow()
    {
        InitializeComponent();

        string userDir = YamlHelper.GetUserDataDir();
        _dictFilePath = Path.Combine(userDir, "phrase.txt");

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

        LoadMainDictIndex();
        LoadData();
    }

    private void LoadData()
    {
        try
        {
            if (!File.Exists(_dictFilePath))
                File.WriteAllText(_dictFilePath, string.Empty, new System.Text.UTF8Encoding(false));

            _allItems.Clear();
            _allItems.AddRange(RimeDictParser.LoadDict(_dictFilePath));
            PerformSearch(string.Empty);
        }
        catch (Exception ex)
        {
            _ = UiDialogs.ShowErrorAsync(this, "读取短语文件失败：" + ex.Message);
        }
    }

    private void PerformSearch(string keyword)
    {
        if (string.IsNullOrEmpty(keyword))
        {
            _currentList = _allItems.ToList();
        }
        else
        {
            string lowerKey = keyword.ToLowerInvariant();
            _currentList = _allItems
                .Where(item => item.Code.Contains(lowerKey, StringComparison.OrdinalIgnoreCase) ||
                               item.Word.Contains(lowerKey, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        dgvList.ItemsSource = null;
        dgvList.ItemsSource = _currentList;
        UpdatePageInfo();
    }

    private void UpdatePageInfo()
    {
        lblPageInfo.Text = $"共 {_currentList.Count} 条数据";
    }

    private void BtnOk_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            RimeDictParser.SaveDict(_dictFilePath, _allItems);
            _ = UiDialogs.ShowInfoAsync(this, "保存成功！需要重新部署才能生效。");
            Close();
        }
        catch (Exception ex)
        {
            _ = UiDialogs.ShowErrorAsync(this, "保存失败：" + ex.Message);
        }
    }

    private async void BtnAdd_Click(object? sender, RoutedEventArgs e)
    {
        var addForm = new AddPhraseWindow(_allItems);
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
        if (selected.Count == 0)
            return;

        foreach (var item in selected)
        {
            _allItems.Remove(item);
        }
        PerformSearch(txtFilter.Text?.Trim() ?? string.Empty);
    }

    private async void BtnImport_Click(object? sender, RoutedEventArgs e)
    {
        var path = await UiDialogs.PickOpenFileAsync(this, "选择词库文本", ("文本文件", ["*.txt", "*"]));
        if (path is null)
            return;

        try
        {
            int imported = 0, skipped = 0;
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs, System.Text.Encoding.UTF8);

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                    continue;

                if (!TryParsePhraseLine(line, out var item))
                    continue;

                if (_mainDictIndex.TryGetValue(item.Word, out var codes) && codes.Contains(item.Code))
                {
                    skipped++;
                    continue;
                }

                if (_allItems.Any(x => x.Code == item.Code && x.Word == item.Word))
                {
                    skipped++;
                    continue;
                }

                _allItems.Add(item);
                imported++;
            }

            PerformSearch(txtFilter.Text?.Trim() ?? string.Empty);
            await UiDialogs.ShowInfoAsync(this, $"导入完成\r\n成功：{imported}\r\n跳过重码：{skipped}", "导入结果");
        }
        catch (Exception ex)
        {
            await UiDialogs.ShowErrorAsync(this, ex.Message);
        }
    }

    private async void BtnExport_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentList.Count == 0)
        {
            await UiDialogs.ShowInfoAsync(this, "当前没有可导出的数据。");
            return;
        }

        var path = await UiDialogs.PickSaveFileAsync(this, "导出短语", "phrase_export.txt", ("文本文件 (UTF8无BOM.txt)", ["*.txt"]));
        if (path is null)
            return;

        try
        {
            using var writer = new StreamWriter(path, false, new System.Text.UTF8Encoding(false));
            foreach (var item in _currentList)
                writer.WriteLine($"{item.Code}\t{item.Word}");
        }
        catch (Exception ex)
        {
            await UiDialogs.ShowErrorAsync(this, "导出失败：" + ex.Message);
            return;
        }

        await UiDialogs.ShowInfoAsync(this, $"成功导出 {_currentList.Count} 条数据。");
    }

    private async void BtnClear_Click(object? sender, RoutedEventArgs e)
    {
        if (await UiDialogs.ConfirmAsync(this, "确定要清空所有自定义短语吗？", "警告"))
        {
            _allItems.Clear();
            PerformSearch(string.Empty);
        }
    }

    private async void OnGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (dgvList.SelectedItem is not DictItem targetItem)
            return;

        var editForm = new AddPhraseWindow(_allItems, targetItem);
        var newItem = await editForm.ShowAsync(this);
        if (newItem != null)
        {
            targetItem.Code = newItem.Code;
            targetItem.Word = newItem.Word;
            PerformSearch(txtFilter.Text?.Trim() ?? string.Empty);
        }
    }

    private void LoadMainDictIndex()
    {
        string mainDictPath = Path.Combine(YamlHelper.GetUserDataDir(), "wubi.dict.yaml");
        if (!File.Exists(mainDictPath))
            return;

        using var reader = new StreamReader(mainDictPath, new System.Text.UTF8Encoding(false));
        bool isBody = false;
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            if (!isBody)
            {
                if (line.Trim() == "...")
                    isBody = true;
                continue;
            }

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;

            var parts = line.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                continue;

            string word = parts[0];
            string code = parts[1];

            if (parts[0].Length > 0 && parts[0][0] <= 0x7F)
            {
                code = parts[0];
                word = parts[1];
            }

            if (!_mainDictIndex.TryGetValue(word, out var codes))
                _mainDictIndex[word] = codes = new HashSet<string>();
            codes.Add(code);
        }
    }

    private static bool TryParsePhraseLine(string line, out DictItem item)
    {
        item = null!;
        var parts = line.Split(['\t'], 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return false;

        item = new DictItem
        {
            Code = parts[0].Trim(),
            Word = parts[1].Trim()
        };
        return true;
    }

    private static class RimeDictParser
    {
        public static List<DictItem> LoadDict(string filePath)
        {
            var items = new List<DictItem>();
            if (!File.Exists(filePath))
                return items;

            using var reader = new StreamReader(filePath, System.Text.Encoding.UTF8);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                    continue;

                var parts = line.Split(['\t'], 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    items.Add(new DictItem
                    {
                        Code = parts[0].Trim(),
                        Word = parts[1].Trim()
                    });
                }
            }
            return items;
        }

        public static void SaveDict(string filePath, List<DictItem> items)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var item in items)
                sb.AppendLine($"{item.Code}\t{item.Word}");
            File.WriteAllText(filePath, sb.ToString(), new System.Text.UTF8Encoding(false));
        }
    }
}
