using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using WeaselSettings.Avalonia.Models;

namespace WeaselSettings.Avalonia.Views;

/// <summary>
/// 新增/编辑五笔词条窗口，对应 WinForms 的 AddWordForm（词库管理工具）。
/// 支持根据拆分表自动推导五笔编码。
/// </summary>
public partial class AddDictWindow : Window
{
    private readonly List<DictItem> _extItems;
    private readonly Dictionary<string, HashSet<string>> _mainDict;
    private readonly Dictionary<char, string> _wubiCharCodes = new();
    private bool _isDictValid;
    private readonly string _editingOriginalWord = string.Empty;

    public AddDictWindow(
        List<DictItem> extItems,
        Dictionary<string, HashSet<string>> mainDict,
        DictItem? targetItem = null)
    {
        InitializeComponent();
        _extItems = extItems;
        _mainDict = mainDict;

        LoadWubiDict();

        txtWord.TextChanged += (_, _) => OnWordChanged();
        txtCode.TextChanged += (_, _) =>
        {
            CheckCanSave();
            FilterCode();
        };
        txtWeight.TextChanged += (_, _) => OnWeightChanged();
        btnOk.Click += OnOkClick;
        btnCancel.Click += (_, _) => Close();

        if (targetItem != null)
        {
            Title = "编辑词条";
            _editingOriginalWord = targetItem.Word;
            txtWord.Text = targetItem.Word;
            txtCode.Text = targetItem.Code;
            txtWeight.Text = targetItem.Weight.ToString();
        }
    }

    public DictItem? NewItem { get; private set; }

    public async Task<DictItem?> ShowAsync(Window owner)
    {
        await ShowDialog(owner);
        return NewItem;
    }

    private void LoadWubiDict()
    {
        try
        {
            string dictPath = Path.Combine(YamlHelper.GetUserDataDir(), "wb_spelling.dict.yaml");
            if (!File.Exists(dictPath))
            {
                _isDictValid = false;
                return;
            }

            bool isDataSection = false;
            using var sr = new StreamReader(dictPath, System.Text.Encoding.UTF8);
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                if (!isDataSection)
                {
                    if (line.Trim() == "...")
                        isDataSection = true;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                    continue;

                int tabIndex = line.IndexOf('\t');
                if (tabIndex <= 0)
                    continue;

                string word = line[..tabIndex].Trim();
                if (word.Length != 1 || _wubiCharCodes.ContainsKey(word[0]))
                    continue;

                string data = line[(tabIndex + 1)..].Trim();
                if (data.StartsWith('[') && data.EndsWith(']'))
                {
                    int firstComma = data.IndexOf(',');
                    if (firstComma != -1)
                    {
                        int secondComma = data.IndexOf(',', firstComma + 1);
                        if (secondComma != -1)
                        {
                            int thirdComma = data.IndexOf(',', secondComma + 1);
                            if (thirdComma != -1)
                            {
                                string code = data[(secondComma + 1)..thirdComma].Trim();
                                if (!string.IsNullOrEmpty(code))
                                    _wubiCharCodes[word[0]] = code;
                            }
                        }
                    }
                }
            }

            _isDictValid = _wubiCharCodes.Count > 0;
        }
        catch
        {
            _isDictValid = false;
        }
    }

    private void OnWordChanged()
    {
        string word = txtWord.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(word))
        {
            lblDupItems.Text = "请输入新词条";
            lblDupItems.Foreground = Brushes.Gray;
            txtCode.IsEnabled = false;
            txtWeight.IsEnabled = false;
            txtCode.Text = string.Empty;
            txtWeight.Text = "10";
            CheckCanSave();
            return;
        }

        bool isSelf = word == _editingOriginalWord;
        var extCodes = _extItems
            .Where(i => i.Word == word && !isSelf)
            .Select(i => i.Code)
            .Distinct()
            .ToList();

        _mainDict.TryGetValue(word, out var mainCodes);

        if (extCodes.Count > 0 || (mainCodes != null && mainCodes.Count > 0))
        {
            var allCodes = new HashSet<string>(extCodes, StringComparer.OrdinalIgnoreCase);
            if (mainCodes != null)
            {
                foreach (var c in mainCodes)
                    allCodes.Add(c);
            }

            lblDupItems.Text = $"⚠ 词条已存在\r\n编码: {string.Join(", ", allCodes)}";
            lblDupItems.Foreground = Brushes.Red;
            txtCode.IsEnabled = false;
            txtWeight.IsEnabled = false;
            txtCode.Text = string.Empty;
            txtWeight.Text = "10";
        }
        else
        {
            lblDupItems.Text = "新词条，请输入编码";
            lblDupItems.Foreground = Brushes.Green;
            txtCode.IsEnabled = true;
            txtWeight.IsEnabled = true;
            txtWeight.Text = "10";

            if (_isDictValid)
                txtCode.Text = GenerateWubiCode(word);
        }

        CheckCanSave();
    }

    private void FilterCode()
    {
        string original = txtCode.Text ?? string.Empty;
        string filtered = new string(original
            .ToLowerInvariant()
            .Where(c => c >= 'a' && c <= 'z')
            .Take(5)
            .ToArray());

        if (original != filtered)
        {
            int caret = txtCode.CaretIndex - (original.Length - filtered.Length);
            txtCode.Text = filtered;
            txtCode.CaretIndex = Math.Max(caret, 0);
        }
    }

    private void OnWeightChanged()
    {
        string text = txtWeight.Text ?? string.Empty;
        if (string.IsNullOrEmpty(text))
            return;

        if (int.TryParse(text, out int value))
        {
            if (value < 0)
            {
                txtWeight.Text = "0";
                txtWeight.CaretIndex = txtWeight.Text?.Length ?? 0;
            }
        }
        else
        {
            txtWeight.Text = "0";
        }
    }

    private void CheckCanSave()
    {
        btnOk.IsEnabled = !string.IsNullOrWhiteSpace(txtWord.Text) &&
                          !string.IsNullOrWhiteSpace(txtCode.Text) &&
                          txtCode.IsEnabled;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        NewItem = new DictItem
        {
            Word = txtWord.Text?.Trim() ?? string.Empty,
            Code = (txtCode.Text ?? string.Empty).Trim().ToLowerInvariant(),
            Weight = txtWeight.Text?.Trim() == string.Empty ? 0 : int.Parse(txtWeight.Text!.Trim()),
            IsNew = true
        };
        Close();
    }

    private string GenerateWubiCode(string word)
    {
        if (!_isDictValid || string.IsNullOrEmpty(word))
            return string.Empty;

        if (word.Length == 1)
            return _wubiCharCodes.TryGetValue(word[0], out var code) ? code : string.Empty;

        foreach (char c in word)
        {
            if (!_wubiCharCodes.ContainsKey(c))
                return string.Empty;
        }

        try
        {
            return word.Length switch
            {
                2 => GetCodePrefix(_wubiCharCodes[word[0]], 2) + GetCodePrefix(_wubiCharCodes[word[1]], 2),
                3 => GetCodePrefix(_wubiCharCodes[word[0]], 1) + GetCodePrefix(_wubiCharCodes[word[1]], 1) +
                     GetCodePrefix(_wubiCharCodes[word[2]], 2),
                _ => GetCodePrefix(_wubiCharCodes[word[0]], 1) + GetCodePrefix(_wubiCharCodes[word[1]], 1) +
                     GetCodePrefix(_wubiCharCodes[word[2]], 1) + GetCodePrefix(_wubiCharCodes[word[^1]], 1)
            };
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetCodePrefix(string code, int length)
    {
        if (string.IsNullOrEmpty(code))
            return string.Empty;
        return code.Length >= length ? code[..length] : code;
    }
}
