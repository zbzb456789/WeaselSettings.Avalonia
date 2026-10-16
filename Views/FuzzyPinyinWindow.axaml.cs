using Avalonia.Controls;
using Avalonia.Interactivity;
using WeaselSettings.Avalonia.Services;

namespace WeaselSettings.Avalonia.Views;

/// <summary>
/// 模糊音设置窗口，对应 WinForms 的 FuzzyPinyinForm。
/// </summary>
public partial class FuzzyPinyinWindow : Window
{
    private static readonly string[] Value1 = ["derive/^z([^h])/zh$1/", "derive/^zh/z/"];
    private static readonly string[] Value2 = ["derive/^c([^h])/ch$1/", "derive/^ch/c/"];
    private static readonly string[] Value3 = ["derive/^s([^h])/sh$1/", "derive/^sh/s/"];
    private static readonly string[] Value4 = ["derive/^f/h/", "derive/^h/f/"];
    private static readonly string[] Value5 = ["derive/^n/l/", "derive/^l/n/"];
    private static readonly string[] Value6 = ["derive/^r/l/", "derive/^l/r/"];
    private static readonly string[] Value7 = ["derive/^([^iuv]*)an$/$1ang/", "derive/^([^iuv]*)ang$/$1an/"];
    private static readonly string[] Value8 = ["derive/en$/eng/", "derive/eng$/en/"];
    private static readonly string[] Value9 = ["derive/in$/ing/", "derive/ing$/in/"];
    private static readonly string[] Value10 = ["derive/ian$/iang/", "derive/iang$/ian/"];
    private static readonly string[] Value11 = ["derive/uan$/uang/", "derive/uang$/uan/"];
    private static readonly string[] Value12 = ["abbrev/^([a-z]).+$/$1/", "abbrev/^([zcs]h).+$/$1/"];

    private readonly YamlHelper _yamlHelper = new();
    private readonly string _fullPath;
    private readonly Dictionary<CheckBox, string[]> _ruleMap = new();

    public FuzzyPinyinWindow()
    {
        InitializeComponent();
        _fullPath = Path.Combine(YamlHelper.GetUserDataDir(), "pinyin.custom.yaml");

        _ruleMap = new Dictionary<CheckBox, string[]>
        {
            { chkZ_ZH, Value1 },
            { chkC_CH, Value2 },
            { chkS_SH, Value3 },
            { chkF_H, Value4 },
            { chkL_N, Value5 },
            { chkR_L, Value6 },
            { chkAn_Ang, Value7 },
            { chkEn_Eng, Value8 },
            { chkIn_Ing, Value9 },
            { chkIan_Iang, Value10 },
            { chkUan_Uang, Value11 },
            { chkSimplePinyin, Value12 }
        };

        btnOk.Click += BtnOk_Click;
        btnReset.Click += (_, _) =>
        {
            foreach (var chk in _ruleMap.Keys)
                chk.IsChecked = false;
        };
        btnCancel.Click += (_, _) => Close();

        Opened += (_, _) => LoadRules();
    }

    private void LoadRules()
    {
        if (!File.Exists(_fullPath))
            return;

        string currentAlgebra = _yamlHelper.GetNodeValue(_fullPath, "patch/speller/algebra", true);
        foreach (var kvp in _ruleMap)
        {
            kvp.Key.IsChecked = currentAlgebra.Contains(kvp.Value[0], StringComparison.Ordinal);
        }
    }

    private void BtnOk_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var activeRules = new List<string>();
            foreach (var kvp in _ruleMap)
            {
                if (kvp.Key.IsChecked == true)
                    activeRules.AddRange(kvp.Value);
            }

            string yamlListValue = "[" + string.Join(", ", activeRules) + "]";
            _yamlHelper.ModifyNode(_fullPath, "patch/speller/algebra", yamlListValue);
            Close();
        }
        catch (Exception ex)
        {
            _ = UiDialogs.ShowErrorAsync(this, $"保存失败: {ex.Message}");
        }
    }
}
