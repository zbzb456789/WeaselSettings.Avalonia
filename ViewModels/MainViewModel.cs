using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WeaselSettings.Avalonia.Models;

namespace WeaselSettings.Avalonia.ViewModels;

/// <summary>
/// 主窗口 ViewModel：完整移植 WinForms Form1 的全部设置逻辑。
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private const string ZkeyValue =
        "[derive/^(...).$/$1z/, derive/^(..).(.*)$/$1z$2/, derive/^(.).(.*)$/$1z$2/]";

    private readonly YamlHelper _yamlHelper = new();
    private readonly string RimeUserDir = YamlHelper.GetUserDataDir();
    private readonly RimeStyle _style = new();
    private bool _loading = true;

    private List<string> _dictHeaderLines = new();
    private readonly List<DictionaryEntry> _allDictEntries = new();
    private List<DictionaryEntry> _selectedDictEntries = new();
    private List<DictionaryEntry> _filteredEntries = new();
    private string _currentDictPath = string.Empty;
    private CancellationTokenSource? _filterCts;

    public MainViewModel()
    {
        SelectLabelOptions = new[]
        {
            "[1, 2, 3, 4, 5, 6, 7, 8, 9, 10]",
            "[1., 2., 3., 4., 5., 6., 7., 8., 9., 10.]",
            "[⑴, ⑵, ⑶, ⑷, ⑸, ⑹, ⑺, ⑻, ⑼, ⑽]",
            "[①, ②, ③, ④, ⑤, ⑥, ⑦, ⑧, ⑨, ⑩]",
            "[㊀, ㊁, ㊂, ㊃, ㊄, ㊅, ㊆, ㊇, ㊈, ㊉]",
            "[❶, ❷, ❸, ❹, ❺, ❻, ❼, ❽, ❾, ❿]",
            "[Ⅰ, Ⅱ, Ⅲ, Ⅳ, Ⅴ, Ⅵ, Ⅶ, Ⅷ, Ⅸ, Ⅹ, Ⅺ, Ⅻ]",
            "[甲, 乙, 丙, 丁, 戊, 己, 庚, 辛, 壬, 癸]",
            "[子, 丑, 寅, 卯, 辰, 巳, 午, 未, 申, 酉, 戌, 亥]"
        };

        DictSourceNames = ["86五笔", "98五笔", "新世纪", "986五笔", "虎码", "自定义", "用户词库"];

        LoadSettings();
    }

    /// <summary>给预览控件和颜色选择器使用的样式对象。</summary>
    public RimeStyle Style => _style;

    /// <summary>出现错误时触发（对应 WinForms 的 MessageBox 错误提示）。</summary>
    public event EventHandler<string>? ErrorOccurred;

    /// <summary>提示信息（对应 WinForms 的 MessageBox 提示）。</summary>
    public event EventHandler<string>? InfoMessage;

    /// <summary>候选分割字变化时触发，用于刷新预览控件。</summary>
    public event EventHandler<string?>? SelectLabelsChanged;

    public string HelpText { get; } =
        "本程序是在Rime的基础上进行了二次开发，同时借用以下两位大佬仓库的部分内容：\n" +
        "    Rime官网：https://rime.im/\n" +
        "    1、空山明月：https://github.com/myshiqi/WubiMaster\n" +
        "    2、五笔小筑：http://98wb.ysepan.com\n" +
        "说明：\n" +
        "1、为了规范词库，导入系统词库不支持空格，特殊需求去用户词库导入，具体使用方法看词库教程。\n" +
        "2、生僻字开关对应屏蔽GBK开关，需要码表和拆分表对应才能正常使用\n" +
        "快捷键：\n" +
        "    Ctrl+`         打开属性设置\n" +
        "    Ctrl+shift+F   繁简切换\n" +
        "    shift+空格     半/全角切换\n" +
        "    Ctrl+。        中英文符号切换\n" +
        "    Ctrl+shift+D   单字/词组切换\n" +
        "    Ctrl+shift+U   GB2312/GBK字符集切换\n" +
        "    Ctrl+shift+H   拆分开关\n" +
        "    Ctrl+shift+B   编码开关\n" +
        "    Ctrl+shift+J   读音开关\n" +
        "    Ctrl+shift+E   Emoji开关\n" +
        "    Ctrl+shift+X   单字序号开关\n" +
        "    Ctrl+shift+T   英文单词翻译开关\n" +
        "    shift + v  计算器 shift+r  金额、数字大小写\n" +
        "    shift+u Unicode打字\n" +
        "    nong 农历    jieq   24节气 week   星期\n" +
        "    date    日期 time   时间\n" +
        "    Ctrl+1 五笔拼音混输 Ctrl+2 纯五笔 Ctrl+3 纯拼音\n" +
        "                                                本人QQ：270271227";

    // ============================================================
    // 输入风格
    // ============================================================

    [ObservableProperty]
    public partial string SelectedInputScheme { get; set; } = "wubi_pinyin";

    [ObservableProperty]
    public partial string SelectedWubiVersion { get; set; } = "86";

    [ObservableProperty]
    public partial bool IsCustomSchemaSelected { get; set; }

    public ObservableCollection<SchemaOption> Schemas { get; } = new();

    partial void OnSelectedInputSchemeChanged(string value)
    {
        if (_loading || string.IsNullOrEmpty(value))
            return;

        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "user.yaml"), "var/previously_selected_schema", value);
    }

    partial void OnSelectedWubiVersionChanged(string value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        IsCustomSchemaSelected = value == "custom";
        if (_loading)
            return;

        _ = ForceReplaceSchemaAsync(value);
    }

    // ============================================================
    // 默认状态（开关组）
    // ============================================================

    [ObservableProperty]
    public partial string ChineseEnglishMode { get; set; } = "中文";

    [ObservableProperty]
    public partial string SimplifiedTraditional { get; set; } = "简体";

    [ObservableProperty]
    public partial string HalfFullShape { get; set; } = "半角";

    [ObservableProperty]
    public partial string PhraseSingleChar { get; set; } = "词组";

    [ObservableProperty]
    public partial bool AsciiPunct { get; set; }

    [ObservableProperty]
    public partial bool ShowEmoji { get; set; }

    [ObservableProperty]
    public partial bool ShowSpelling { get; set; }

    [ObservableProperty]
    public partial bool ShowCode { get; set; }

    [ObservableProperty]
    public partial bool ShowPinyin { get; set; }

    [ObservableProperty]
    public partial bool ShowIndex { get; set; }

    [ObservableProperty]
    public partial bool EnableCompletion { get; set; }

    [ObservableProperty]
    public partial bool EnableEncoder { get; set; }

    [ObservableProperty]
    public partial bool Gb2312Only { get; set; }

    [ObservableProperty]
    public partial bool EnableUserDict { get; set; }

    [ObservableProperty]
    public partial bool UserDictEnabled { get; set; } = true;

    // ============================================================
    // Z 键与按键行为
    // ============================================================

    [ObservableProperty]
    public partial bool ZKeyWildcard { get; set; }

    [ObservableProperty]
    public partial bool ZKeyRepeat { get; set; }

    [ObservableProperty]
    public partial bool ZKeyPinyin { get; set; }

    [ObservableProperty]
    public partial bool ReturnEsc { get; set; }

    [ObservableProperty]
    public partial bool NullReturn { get; set; }

    [ObservableProperty]
    public partial bool NullReturnEnabled { get; set; }

    [ObservableProperty]
    public partial bool SpaceScreen { get; set; }

    [ObservableProperty]
    public partial bool AutoSelect { get; set; }

    [ObservableProperty]
    public partial bool MaxCodeLength { get; set; }

    [ObservableProperty]
    public partial bool MaxCodeEnabled { get; set; }

    [ObservableProperty]
    public partial bool EnglishWord { get; set; }

    [ObservableProperty]
    public partial bool EnglishTranslation { get; set; }

    [ObservableProperty]
    public partial bool EnglishTranslationEnabled { get; set; }

    // ============================================================
    // 外观
    // ============================================================

    [ObservableProperty]
    public partial bool Horizontal { get; set; }

    [ObservableProperty]
    public partial bool VerticalText { get; set; }

    [ObservableProperty]
    public partial bool VerticalTextLtr { get; set; }

    [ObservableProperty]
    public partial bool ClickToCapture { get; set; }

    [ObservableProperty]
    public partial bool InlinePreedit { get; set; }

    [ObservableProperty]
    public partial decimal PageSize { get; set; } = 5;

    [ObservableProperty]
    public partial decimal FontPoint { get; set; } = 14;

    [ObservableProperty]
    public partial decimal LabelFontPoint { get; set; } = 14;

    [ObservableProperty]
    public partial decimal CommentFontPoint { get; set; } = 14;

    [ObservableProperty]
    public partial string FontFace { get; set; } = "微软雅黑";

    [ObservableProperty]
    public partial string LabelFontFace { get; set; } = "微软雅黑";

    [ObservableProperty]
    public partial string CommentFontFace { get; set; } = "通用字根";

    public ObservableCollection<ColorSchemeItem> ColorSchemes { get; } = new();

    [ObservableProperty]
    public partial ColorSchemeItem? LightScheme { get; set; }

    [ObservableProperty]
    public partial ColorSchemeItem? DarkScheme { get; set; }

    public string[] SelectLabelOptions { get; }

    [ObservableProperty]
    public partial string? SelectedSelectLabels { get; set; }

    // ============================================================
    // 词库管理
    // ============================================================

    public string[] DictSourceNames { get; }

    [ObservableProperty]
    public partial int SelectedDictIndex { get; set; } = -1;

    [ObservableProperty]
    public partial string DictFilter { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DictionaryEntry? SelectedEntry { get; set; }

    [ObservableProperty]
    public partial string EditWord { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditWeight { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditStem { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool EditStemEnabled { get; set; }

    [ObservableProperty]
    public partial bool DictSaveEnabled { get; set; }

    /// <summary>
    /// 当前过滤/排序后的词条列表。整个列表整体替换，避免逐条触发集合变更导致大数据量卡顿。
    /// </summary>
    public IReadOnlyList<DictionaryEntry> FilteredEntries => _filteredEntries;

    [ObservableProperty]
    public partial string StatusText { get; set; } = "就绪";

    // ============================================================
    // 默认状态开关写入
    // ============================================================

    private static readonly string[] AllSwitchFiles =
        ["wubi_pinyin.custom.yaml", "wubi.custom.yaml", "pinyin.custom.yaml"];

    private static readonly string[] WubiSwitchFiles =
        ["wubi_pinyin.custom.yaml", "wubi.custom.yaml"];

    private void WriteSwitch(string[] files, int index, string value)
    {
        foreach (var file in files)
        {
            _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, file), $"patch/switches/@{index}/reset", value);
        }
    }

    partial void OnChineseEnglishModeChanged(string value)
    {
        if (_loading || string.IsNullOrEmpty(value)) return;
        WriteSwitch(AllSwitchFiles, 0, value == "英文" ? "1" : "0");
    }

    partial void OnSimplifiedTraditionalChanged(string value)
    {
        if (_loading || string.IsNullOrEmpty(value)) return;
        WriteSwitch(AllSwitchFiles, 1, value == "繁体" ? "1" : "0");
    }

    partial void OnHalfFullShapeChanged(string value)
    {
        if (_loading || string.IsNullOrEmpty(value)) return;
        WriteSwitch(AllSwitchFiles, 2, value == "全角" ? "1" : "0");
    }

    partial void OnPhraseSingleCharChanged(string value)
    {
        if (_loading || string.IsNullOrEmpty(value)) return;
        WriteSwitch(AllSwitchFiles, 4, value == "单字" ? "1" : "0");
    }

    partial void OnAsciiPunctChanged(bool value)
    {
        if (_loading) return;
        WriteSwitch(AllSwitchFiles, 3, value ? "1" : "0");
    }

    partial void OnGb2312OnlyChanged(bool value)
    {
        if (_loading) return;
        WriteSwitch(WubiSwitchFiles, 5, value ? "1" : "0");
    }

    partial void OnShowSpellingChanged(bool value)
    {
        if (_loading) return;
        WriteSwitch(WubiSwitchFiles, 6, value ? "1" : "0");
    }

    partial void OnShowCodeChanged(bool value)
    {
        if (_loading) return;
        WriteSwitch(WubiSwitchFiles, 7, value ? "1" : "0");
    }

    partial void OnShowPinyinChanged(bool value)
    {
        if (_loading) return;
        WriteSwitch(WubiSwitchFiles, 8, value ? "1" : "0");
    }

    partial void OnShowIndexChanged(bool value)
    {
        if (_loading) return;
        WriteSwitch(WubiSwitchFiles, 9, value ? "1" : "0");
    }

    partial void OnShowEmojiChanged(bool value)
    {
        if (_loading) return;
        WriteSwitch(WubiSwitchFiles, 10, value ? "1" : "0");
        WriteSwitch(["pinyin.custom.yaml"], 5, value ? "1" : "0");
    }

    partial void OnEnableCompletionChanged(bool value)
    {
        if (_loading) return;
        var valueText = value ? "true" : "false";
        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "wubi.custom.yaml"), "patch/translator/enable_completion", valueText);
        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "wubi_pinyin.custom.yaml"), "patch/translator/enable_completion", valueText);
        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "pinyin.custom.yaml"), "patch/ci_reverse_lookup/enable_completion", valueText);
    }

    partial void OnEnableEncoderChanged(bool value)
    {
        if (_loading) return;
        var valueText = value ? "true" : "false";
        foreach (var file in WubiSwitchFiles)
        {
            _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, file), "patch/translator/enable_encoder", valueText);
            _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, file), "patch/translator/encode_commit_history", valueText);
        }
    }

    partial void OnEnableUserDictChanged(bool value)
    {
        if (_loading) return;
        var valueText = value ? "true" : "false";
        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "wubi.custom.yaml"), "patch/translator/enable_user_dict", valueText);
        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "wubi_pinyin.custom.yaml"), "patch/translator/enable_user_dict", valueText);
    }

    // ============================================================
    // Z 键与按键行为写入
    // ============================================================

    partial void OnZKeyWildcardChanged(bool value)
    {
        if (_loading) return;
        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "wubi.custom.yaml"), "patch/speller/algebra",
            value ? ZkeyValue : "[]");
    }

    partial void OnZKeyRepeatChanged(bool value)
    {
        if (_loading) return;
        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "wubi.custom.yaml"), "patch/history/input",
            value ? "z" : "[]");
    }

    partial void OnZKeyPinyinChanged(bool value)
    {
        if (_loading) return;
        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "wubi.custom.yaml"), "patch/rvlk1/prefix",
            value ? "`" : "[]");
    }

    partial void OnReturnEscChanged(bool value)
    {
        NullReturnEnabled = value;
        if (_loading)
            return;

        foreach (var file in WubiSwitchFiles)
        {
            _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, file), "patch/key_binder/bindings/@next",
                value ? "{accept: Return, send: Escape, when: composing}" : "{}");
        }
    }

    partial void OnNullReturnChanged(bool value)
    {
        if (_loading) return;

        foreach (var file in WubiSwitchFiles)
        {
            _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, file), "patch/key_binder/bindings/@next",
                value ? "{accept: Return, send: Escape, when: has_menu}" : "{accept: Return, send: Escape, when: composing}");
        }
    }

    partial void OnSpaceScreenChanged(bool value)
    {
        if (_loading) return;

        foreach (var file in WubiSwitchFiles)
        {
            _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, file), "patch/key_binder/bindings/@next1",
                value ? "{accept: space, send: Shift+Return, when: composing}" : "{}");
        }
    }

    partial void OnAutoSelectChanged(bool value)
    {
        MaxCodeEnabled = value;
        if (_loading) return;

        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "wubi.custom.yaml"), "patch/speller/auto_select",
            value ? "true" : "false");
    }

    partial void OnMaxCodeLengthChanged(bool value)
    {
        if (_loading) return;
        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "wubi.custom.yaml"), "patch/speller/max_code_length",
            value ? "4" : "0");
    }

    partial void OnEnglishWordChanged(bool value)
    {
        EnglishTranslationEnabled = value;
        if (_loading) return;

        
        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "wubi.extended.dict.yaml"), "import_tables",
            value ? "[wubi, quick_symbols, english]" : "[wubi, quick_symbols]");
        Task.Run(() => _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "pinyin.dict.yaml"), "import_tables",
            value ? "[quick_symbols, english]" : "[quick_symbols]"));
    }

    partial void OnEnglishTranslationChanged(bool value)
    {
        if (_loading) return;
        WriteSwitch(WubiSwitchFiles, 11, value ? "1" : "0");
        WriteSwitch(["pinyin.custom.yaml"], 6, value ? "1" : "0");
    }

    partial void OnUserDictEnabledChanged(bool value)
    {
        if (_loading) return;

        var dictName = value ? "wubi.extended" : "wubi";
        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "wubi.custom.yaml"), "patch/translator/dictionary", dictName);
        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "wubi.custom.yaml"), "patch/fixed/dictionary", dictName);
        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "wubi.custom.yaml"), "patch/mkst/dictionary", dictName);
        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "wubi.custom.yaml"), "patch/sentence_input/dictionary", dictName);
        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "wubi.custom.yaml"), "patch/ci_reverse_lookup/dictionary", dictName);
        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "wubi_pinyin.custom.yaml"), "patch/translator/dictionary", dictName);
    }

    // ============================================================
    // 外观写入
    // ============================================================

    partial void OnHorizontalChanged(bool value)
    {
        if (value)
        {
            VerticalText = false;
            VerticalTextLtr = false;
        }
        else
        {
            VerticalTextLtr = false;
        }

        _style.Horizontal = value;
        _style.TriggerChange();
        if (_loading) return;

        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "weasel.custom.yaml"), "patch/style/horizontal", value ? "true" : "false");
    }

    partial void OnVerticalTextChanged(bool value)
    {
        if (value)
            Horizontal = false;

        _style.VerticalText = value;
        _style.TriggerChange();
        if (_loading) return;

        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "weasel.custom.yaml"), "patch/style/vertical_text", value ? "true" : "false");
        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "weasel.custom.yaml"), "patch/style/horizontal", "false");
    }

    partial void OnVerticalTextLtrChanged(bool value)
    {
        _style.VerticalTextLeftToRight = value;
        _style.TriggerChange();
        if (_loading) return;

        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "weasel.custom.yaml"), "patch/style/vertical_text_left_to_right", value ? "true" : "false");
    }

    partial void OnClickToCaptureChanged(bool value)
    {
        if (_loading) return;
        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "weasel.custom.yaml"), "patch/style/click_to_capture", value ? "true" : "false");
    }

    partial void OnInlinePreeditChanged(bool value)
    {
        if (_loading) return;
        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "weasel.custom.yaml"), "patch/style/inline_preedit", value ? "true" : "false");
    }

    partial void OnPageSizeChanged(decimal value)
    {
        if (_loading) return;
        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "default.custom.yaml"), "patch/menu/page_size", ((int)value).ToString());
    }

    partial void OnFontPointChanged(decimal value)
    {
        if (_loading) return;
        _style.FontPoint = (int)value;
        _style.TriggerChange();
        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "weasel.custom.yaml"), "patch/style/font_point", ((int)value).ToString());
    }

    partial void OnLabelFontPointChanged(decimal value)
    {
        if (_loading) return;
        _style.LabelFontPoint = (int)value;
        _style.TriggerChange();
        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "weasel.custom.yaml"), "patch/style/label_font_point", ((int)value).ToString());
    }

    partial void OnCommentFontPointChanged(decimal value)
    {
        if (_loading) return;
        _style.CommentFontPoint = (int)value;
        _style.TriggerChange();
        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "weasel.custom.yaml"), "patch/style/comment_font_point", ((int)value).ToString());
    }

    partial void OnLightSchemeChanged(ColorSchemeItem? value)
    {
        if (_loading || value is null)
            return;

        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "weasel.custom.yaml"), "patch/style/color_scheme", value.Id);
        _yamlHelper.ApplyColorSchemeToStyle(Path.Combine(RimeUserDir, "weasel.yaml"), value.Id, _style);
    }

    partial void OnDarkSchemeChanged(ColorSchemeItem? value)
    {
        if (_loading || value is null)
            return;

        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "weasel.custom.yaml"), "patch/style/color_scheme_dark", value.Id);
        _yamlHelper.ApplyColorSchemeToStyle(Path.Combine(RimeUserDir, "weasel.yaml"), value.Id, _style);
    }

    partial void OnSelectedSelectLabelsChanged(string? value)
    {
        SelectLabelsChanged?.Invoke(this, value);
        if (_loading || value is null)
            return;

        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "default.custom.yaml"), "patch/menu/alternative_select_labels", value);
    }

    /// <summary>字体下拉框勾选变化时调用。</summary>
    public void SetFontFace(string key, string value)
    {
        if (_loading) return;

        if (key == "font_face") _style.FontFace = value;
        else if (key == "label_font_face") _style.LabelFontFace = value;
        else if (key == "comment_font_face") _style.CommentFontFace = value;

        _style.TriggerChange();
        _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "weasel.custom.yaml"), $"patch/style/{key}", value);
    }

    /// <summary>颜色选择器变化时调用。仅当当前浅色皮肤为 custom 时写回 weasel.yaml。</summary>
    public void SetStyleColor(string key, Color color)
    {
        if (_loading) return;

        switch (key)
        {
            case "text_color": _style.text_color = color; break;
            case "back_color": _style.back_color = color; break;
            case "border_color": _style.border_color = color; break;
            case "shadow_color": _style.shadow_color = color; break;
            case "hilited_back_color": _style.hilited_back_color = color; break;
            case "hilited_text_color": _style.hilited_text_color = color; break;
            case "hilited_shadow_color": _style.hilited_shadow_color = color; break;
            case "comment_text_color": _style.comment_text_color = color; break;
            case "hilited_mark_color": _style.hilited_mark_color = color; break;
            case "hilited_candidate_back_color": _style.hilited_candidate_back_color = color; break;
            case "hilited_candidate_shadow_color": _style.hilited_candidate_shadow_color = color; break;
            case "hilited_label_color": _style.hilited_label_color = color; break;
            case "hilited_candidate_text_color": _style.hilited_candidate_text_color = color; break;
            case "hilited_candidate_border_color": _style.hilited_candidate_border_color = color; break;
            case "hilited_comment_text_color": _style.hilited_comment_text_color = color; break;
            case "candidate_back_color": _style.candidate_back_color = color; break;
            case "candidate_text_color": _style.candidate_text_color = color; break;
            case "candidate_shadow_color": _style.candidate_shadow_color = color; break;
            case "label_color": _style.label_color = color; break;
            case "candidate_border_color": _style.candidate_border_color = color; break;
            default: return;
        }

        _style.TriggerChange();

        if (LightScheme?.Id == "custom")
        {
            _yamlHelper.ModifyNode(Path.Combine(RimeUserDir, "weasel.yaml"),
                $"preset_color_schemes/custom/{key}", Controls.RimeColorPicker.ToRimeColor(color));
        }
    }

    // ============================================================
    // 加载
    // ============================================================

    private void LoadSettings()
    {
        _loading = true;
        StatusText = "正在读取配置...";

        try
        {
            // 输入方案
            string currentSchema1 = _yamlHelper.GetNodeValue(Path.Combine(RimeUserDir, "user.yaml"), "var/previously_selected_schema");
            string currentSchema2 = _yamlHelper.GetNodeValue(Path.Combine(RimeUserDir, "wubi.dict.yaml"), "version");

            string asciiMode1 = GetSwitch("wubi_pinyin.custom.yaml", 0);
            string asciiMode2 = GetSwitch("wubi.custom.yaml", 0);
            string zhTrad1 = GetSwitch("wubi_pinyin.custom.yaml", 1);
            string zhTrad2 = GetSwitch("wubi.custom.yaml", 1);
            string fullShape1 = GetSwitch("wubi_pinyin.custom.yaml", 2);
            string fullShape2 = GetSwitch("wubi.custom.yaml", 2);
            string asciiPunct1 = GetSwitch("wubi_pinyin.custom.yaml", 3);
            string asciiPunct2 = GetSwitch("wubi.custom.yaml", 3);
            string singleChar1 = GetSwitch("wubi_pinyin.custom.yaml", 4);
            string singleChar2 = GetSwitch("wubi.custom.yaml", 4);
            string gb2312_1 = GetSwitch("wubi_pinyin.custom.yaml", 5);
            string gb2312_2 = GetSwitch("wubi.custom.yaml", 5);
            string showSpelling1 = GetSwitch("wubi_pinyin.custom.yaml", 6);
            string showSpelling2 = GetSwitch("wubi.custom.yaml", 6);
            string showCode1 = GetSwitch("wubi_pinyin.custom.yaml", 7);
            string showCode2 = GetSwitch("wubi.custom.yaml", 7);
            string showPinyin1 = GetSwitch("wubi_pinyin.custom.yaml", 8);
            string showPinyin2 = GetSwitch("wubi.custom.yaml", 8);
            string showIndex1 = GetSwitch("wubi_pinyin.custom.yaml", 9);
            string showIndex2 = GetSwitch("wubi.custom.yaml", 9);
            string showEs1 = GetSwitch("wubi_pinyin.custom.yaml", 10);
            string showEs2 = GetSwitch("wubi.custom.yaml", 10);
            string englishtranslation1 = GetSwitch("wubi_pinyin.custom.yaml", 11);
            string englishtranslation2 = GetSwitch("wubi.custom.yaml", 11);
            string englishtranslation3 = GetSwitch("pinyin.custom.yaml", 6);

            string zkeyWildcard = GetNode("wubi.custom.yaml", "patch/speller/algebra");
            string zkeyRepetition = GetNode("wubi.custom.yaml", "patch/history/input");
            string zkeyPinyin = GetNode("wubi.custom.yaml", "patch/rvlk1/prefix");
            string returnEsc = GetNode("wubi.custom.yaml", "patch/key_binder/bindings/@next");
            string spaceScreen = GetNode("wubi.custom.yaml", "patch/key_binder/bindings/@next1");
            string autoSelect = GetNode("wubi.custom.yaml", "patch/speller/auto_select");
            string maxCodeLength = GetNode("wubi.custom.yaml", "patch/speller/max_code_length");
            string enableEncoder = GetNode("wubi.custom.yaml", "patch/translator/enable_encoder");
            string englishword1 = GetNode("wubi.extended.dict.yaml", "import_tables");
            string englishword2 = GetNode("pinyin.dict.yaml", "import_tables");

            string userDict1 = GetNode("wubi.custom.yaml", "patch/translator/dictionary");
            string userDict2 = GetNode("wubi.custom.yaml", "patch/fixed/dictionary");
            string userDict3 = GetNode("wubi.custom.yaml", "patch/mkst/dictionary");
            string userDict4 = GetNode("wubi.custom.yaml", "patch/sentence_input/dictionary");
            string userDict5 = GetNode("wubi.custom.yaml", "patch/ci_reverse_lookup/dictionary");
            string enableCompletion = GetNode("wubi.custom.yaml", "patch/translator/enable_completion");

            string enableUserDict = GetNode("wubi.custom.yaml", "patch/translator/enable_user_dict");
            string pageSize = GetNode("default.custom.yaml", "patch/menu/page_size");
            string inlinePreedit = GetNode("weasel.custom.yaml", "patch/style/inline_preedit");
            string fontPoint = GetNode("weasel.custom.yaml", "patch/style/font_point");
            string labelFontPoint = GetNode("weasel.custom.yaml", "patch/style/label_font_point");
            string commentFontPoint = GetNode("weasel.custom.yaml", "patch/style/comment_font_point");
            string horizontal = GetNode("weasel.custom.yaml", "patch/style/horizontal");
            string verticalText = GetNode("weasel.custom.yaml", "patch/style/vertical_text");
            string verticalTextLtr = GetNode("weasel.custom.yaml", "patch/style/vertical_text_left_to_right");
            string selectLabels = GetNode("default.custom.yaml", "patch/menu/alternative_select_labels");
            string clickToCapture = GetNode("weasel.custom.yaml", "patch/style/click_to_capture");

            string fontFace = GetNode("weasel.custom.yaml", "patch/style/font_face");
            string labelFontFace = GetNode("weasel.custom.yaml", "patch/style/label_font_face");
            string commentFontFace = GetNode("weasel.custom.yaml", "patch/style/comment_font_face");

            // ---- 输入方案 ----
            if (!string.IsNullOrEmpty(currentSchema1) && currentSchema1 is "wubi" or "wubi_pinyin" or "luna_pinyin_simp")
                SelectedInputScheme = currentSchema1;
            if (!string.IsNullOrEmpty(currentSchema2) && currentSchema2 is "86" or "98" or "06" or "986" or "tiger" or "custom")
                SelectedWubiVersion = currentSchema2;

            // ---- 默认状态 ----
            ChineseEnglishMode = (asciiMode1 == "1" || asciiMode2 == "1") ? "英文" : "中文";
            SimplifiedTraditional = (zhTrad1 == "1" || zhTrad2 == "1") ? "繁体" : "简体";
            HalfFullShape = (fullShape1 == "1" || fullShape2 == "1") ? "全角" : "半角";
            PhraseSingleChar = (singleChar1 == "1" || singleChar2 == "1") ? "单字" : "词组";
            AsciiPunct = asciiPunct1 == "1" || asciiPunct2 == "1";
            ShowSpelling = showSpelling1 == "1" || showSpelling2 == "1";
            ShowCode = showCode1 == "1" || showCode2 == "1";
            ShowPinyin = showPinyin1 == "1" || showPinyin2 == "1";
            ShowIndex = showIndex1 == "1" || showIndex2 == "1";
            Gb2312Only = gb2312_1 == "1" || gb2312_2 == "1";
            ShowEmoji = showEs1 == "1" || showEs2 == "1";
            EnglishTranslation = englishtranslation1 == "1" || englishtranslation2 == "1" || englishtranslation3 == "1";

            ZKeyWildcard = zkeyWildcard == ZkeyValue;
            ZKeyRepeat = zkeyRepetition == "z";
            ZKeyPinyin = zkeyPinyin != "[]";
            if (returnEsc == "{}")
            {
                ReturnEsc = false;
                NullReturn = false;
            }
            else if (returnEsc == "{accept: Return, send: Escape, when: has_menu}")
            {
                NullReturn = true;
                ReturnEsc = true;
            }
            else if (returnEsc == "{accept: Return, send: Escape, when: composing}")
            {
                ReturnEsc = true;
            }
            if (englishword1 == "[wubi, quick_symbols, english]" && englishword2 == "[quick_symbols, english]")
            {
                EnglishWord = true;
            }
            else
            {
                EnglishWord = false;
            }
            SpaceScreen = spaceScreen != "{}";
            MaxCodeLength = maxCodeLength == "4";
            AutoSelect = autoSelect?.ToLowerInvariant() == "true";
            EnableEncoder = enableEncoder?.ToLowerInvariant() == "true";
            EnableCompletion = enableCompletion?.ToLowerInvariant() == "true";
            EnableUserDict = enableUserDict?.ToLowerInvariant() == "true";
            UserDictEnabled = !(userDict1 == "wubi" || userDict2 == "wubi" || userDict3 == "wubi" || userDict4 == "wubi" || userDict5 == "wubi");

            // ---- 外观 ----
            Horizontal = horizontal?.ToLowerInvariant() == "true";
            VerticalText = verticalText?.ToLowerInvariant() == "true";
            VerticalTextLtr = verticalTextLtr?.ToLowerInvariant() == "true";
            InlinePreedit = inlinePreedit?.ToLowerInvariant() == "true";
            ClickToCapture = clickToCapture?.ToLowerInvariant() == "true";

            if (byte.TryParse(pageSize, out byte ps)) PageSize = ps;
            if (byte.TryParse(fontPoint, out byte fp)) FontPoint = fp;
            if (byte.TryParse(labelFontPoint, out byte lfp)) LabelFontPoint = lfp;
            if (byte.TryParse(commentFontPoint, out byte cfp)) CommentFontPoint = cfp;

            FontFace = string.IsNullOrEmpty(fontFace) ? FontFace : fontFace;
            LabelFontFace = string.IsNullOrEmpty(labelFontFace) ? LabelFontFace : labelFontFace;
            CommentFontFace = string.IsNullOrEmpty(commentFontFace) ? CommentFontFace : commentFontFace;

            SelectedSelectLabels = SelectLabelOptions.Contains(selectLabels) ? selectLabels : null;

            // ---- 方案列表 ----
            LoadSchemas();
            LoadColorSchemes();

            // 让预览和颜色控件反映当前实际皮肤
            if (LightScheme is not null)
            {
                _yamlHelper.ApplyColorSchemeToStyle(Path.Combine(RimeUserDir, "weasel.yaml"), LightScheme.Id, _style);
            }

            StatusText = "配置读取完成";
        }
        catch (Exception ex)
        {
            StatusText = "读取失败";
            ErrorOccurred?.Invoke(this, "初始化失败: " + ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    private string GetSwitch(string file, int index) =>
        _yamlHelper.GetNodeValue(Path.Combine(RimeUserDir, file), $"patch/switches/@{index}/reset");

    private string GetNode(string file, string key) =>
        _yamlHelper.GetNodeValue(Path.Combine(RimeUserDir, file), key);

    private void LoadSchemas()
    {
        var allSchemas = _yamlHelper.GetSchemaList(RimeUserDir);
        var activeIds = _yamlHelper.GetCurrentActiveSchemaIds(Path.Combine(RimeUserDir, "default.custom.yaml"));

        Schemas.Clear();
        foreach (var kvp in allSchemas)
        {
            var option = new SchemaOption(kvp.Key, kvp.Value, activeIds.Contains(kvp.Key));
            option.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SchemaOption.IsEnabled) && !_loading)
                {
                    try
                    {
                        _yamlHelper.UpdateSchemaListAtPath(
                            Path.Combine(RimeUserDir, "default.custom.yaml"),
                            option.Id, option.IsEnabled);
                    }
                    catch (Exception ex)
                    {
                        ErrorOccurred?.Invoke(this, $"更新配置文件失败: {ex.Message}");
                    }
                }
            };
            Schemas.Add(option);
        }
    }

    private void LoadColorSchemes()
    {
        ColorSchemes.Clear();

        string yamlPath = Path.Combine(RimeUserDir, "weasel.yaml");
        var schemes = _yamlHelper.GetColorSchemes(yamlPath);

        foreach (var kv in schemes)
        {
            ColorSchemes.Add(new ColorSchemeItem(kv.Key, kv.Value));
        }

        string customPath = Path.Combine(RimeUserDir, "weasel.custom.yaml");
        string currentSchemeId = _yamlHelper.GetNodeValue(customPath, "patch/style/color_scheme");
        string currentSchemeIdDark = _yamlHelper.GetNodeValue(customPath, "patch/style/color_scheme_dark");

        LightScheme = ColorSchemes.FirstOrDefault(s => s.Id == currentSchemeId) ?? ColorSchemes.FirstOrDefault();
        DarkScheme = ColorSchemes.FirstOrDefault(s => s.Id == currentSchemeIdDark) ?? ColorSchemes.FirstOrDefault();
    }

    public async Task ForceReplaceSchemaAsync(string versionDir)
    {
        if (string.IsNullOrEmpty(versionDir))
            return;

        try
        {
            await Task.Run(() =>
            {
                string sourceBase = Path.Combine(RimeUserDir, "tables", versionDir);
                if (!Directory.Exists(sourceBase))
                    throw new DirectoryNotFoundException($"源目录不存在: {sourceBase}");

                foreach (string file in Directory.GetFiles(sourceBase))
                {
                    File.Copy(file, Path.Combine(RimeUserDir, Path.GetFileName(file)), true);
                }
            });
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"文件替换失败: {ex.Message}");
        }
    }

    // ============================================================
    // 词库管理
    // ============================================================

    partial void OnSelectedDictIndexChanged(int value)
    {
        if (_loading || value < 0)
            return;

        string? path = value switch
        {
            0 => Path.Combine(RimeUserDir, "tables", "86", "wubi.dict.yaml"),
            1 => Path.Combine(RimeUserDir, "tables", "98", "wubi.dict.yaml"),
            2 => Path.Combine(RimeUserDir, "tables", "06", "wubi.dict.yaml"),
            3 => Path.Combine(RimeUserDir, "tables", "986", "wubi.dict.yaml"),
            4 => Path.Combine(RimeUserDir, "tables", "tiger", "wubi.dict.yaml"),
            5 => Path.Combine(RimeUserDir, "tables", "custom", "wubi.dict.yaml"),
            6 => Path.Combine(RimeUserDir, "wubi.extended.dict.yaml"),
            _ => null
        };

        if (path != null && File.Exists(path))
        {
            LoadDictionary(path);
        }
        else
        {
            ErrorOccurred?.Invoke(this, "对应的词库文件不存在！");
        }
    }

    partial void OnDictFilterChanged(string value)
    {
        // 输入防抖：连续输入时等 300ms 再过滤，避免每个按键都排序/刷新上万行
        _filterCts?.Cancel();
        var cts = new CancellationTokenSource();
        _filterCts = cts;
        _ = DebouncedFilterAsync(value, cts.Token);
    }

    private async Task DebouncedFilterAsync(string value, CancellationToken token)
    {
        try
        {
            await Task.Delay(300, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested)
            return;

        // 确保回到 UI 线程再刷新列表
        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyFilterAndSort();
        }
        else
        {
            await Dispatcher.UIThread.InvokeAsync(() => ApplyFilterAndSort());
        }
    }

    partial void OnSelectedEntryChanged(DictionaryEntry? value)
    {
        if (value is null)
        {
            EditWord = EditCode = EditWeight = EditStem = string.Empty;
            EditStemEnabled = false;
            return;
        }

        EditWord = value.Word;
        EditCode = value.Code;
        EditWeight = value.Weight.ToString();
        EditStemEnabled = value.Word.Length == 1;
        EditStem = EditStemEnabled ? value.Stem : string.Empty;
    }

    public void SetSelectedEntries(IEnumerable<DictionaryEntry> entries)
    {
        _selectedDictEntries = entries.ToList();
    }

    public void LoadDictionaryFromFile(string filePath)
    {
        LoadDictionary(filePath);
    }

    public void LoadDictionary(string filePath)
    {
        _currentDictPath = filePath;
        _dictHeaderLines.Clear();
        _allDictEntries.Clear();

        var lines = File.ReadAllLines(filePath, System.Text.Encoding.UTF8);
        bool isHeader = true;

        foreach (var line in lines)
        {
            if (isHeader)
            {
                _dictHeaderLines.Add(line);
                if (line.Trim() == "...")
                    isHeader = false;
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split('\t');
            if (parts.Length >= 2)
            {
                int w = 0;
                if (parts.Length >= 3)
                    _ = int.TryParse(parts[2], out w);

                _allDictEntries.Add(new DictionaryEntry
                {
                    Word = parts[0],
                    Code = parts[1],
                    Weight = w,
                    Stem = parts.Length >= 4 ? parts[3] : string.Empty
                });
            }
        }

        ApplyFilterAndSort();
        DictSaveEnabled = true;
    }

    private void ApplyFilterAndSort(DictionaryEntry? targetEntry = null)
    {
        string filter = DictFilter.Trim().ToLowerInvariant();

        IEnumerable<DictionaryEntry> query;
        if (string.IsNullOrEmpty(filter))
        {
            query = _allDictEntries;
        }
        else
        {
            query = _allDictEntries.Where(x =>
                x.Code.StartsWith(filter, StringComparison.OrdinalIgnoreCase) ||
                x.Word.Contains(filter, StringComparison.Ordinal));
        }

        var filtered = query
            .OrderBy(x => x.Code, StringComparer.Ordinal)
            .ThenByDescending(x => x.Weight)
            .ToList();

        _filteredEntries = filtered;
        OnPropertyChanged(nameof(FilteredEntries));

        SelectedEntry = targetEntry != null && filtered.Contains(targetEntry)
            ? targetEntry
            : null;
    }

    [RelayCommand]
    private void ApplyEdit()
    {
        if (SelectedEntry is null)
            return;

        SelectedEntry.Word = EditWord.Trim();
        SelectedEntry.Code = EditCode.Trim().ToLowerInvariant();
        if (int.TryParse(EditWeight.Trim(), out int w))
            SelectedEntry.Weight = w;
        SelectedEntry.Stem = EditStem.Trim();

        ApplyFilterAndSort(SelectedEntry);
        DictSaveEnabled = true;
    }

    [RelayCommand]
    private void WeightUp() => AdjustWeight(10);

    [RelayCommand]
    private void WeightDown() => AdjustWeight(-10);

    private void AdjustWeight(int offset)
    {
        if (_selectedDictEntries.Count == 0)
            return;

        var target = _selectedDictEntries[0];
        foreach (var entry in _selectedDictEntries)
        {
            entry.Weight = Math.Max(0, entry.Weight + offset);
        }

        ApplyFilterAndSort(target);
        DictSaveEnabled = true;
    }

    [RelayCommand]
    private void AddEntry()
    {
        var newEntry = new DictionaryEntry { Word = "新词", Code = "a", Weight = 10 };

        if (_selectedDictEntries.Count > 0)
        {
            var baseEntry = _selectedDictEntries[0];
            newEntry.Code = baseEntry.Code;
            newEntry.Weight = Math.Max(0, baseEntry.Weight);
        }

        _allDictEntries.Add(newEntry);
        ApplyFilterAndSort(newEntry);
        DictSaveEnabled = true;
    }

    public void DeleteSelected()
    {
        foreach (var item in _selectedDictEntries.ToList())
        {
            _allDictEntries.Remove(item);
        }

        ApplyFilterAndSort();
        DictSaveEnabled = true;
    }

    [RelayCommand]
    private void SaveDict()
    {
        if (string.IsNullOrEmpty(_currentDictPath))
            return;

        var utf8 = new System.Text.UTF8Encoding(false);
        using var sw = new StreamWriter(_currentDictPath, false, utf8);

        foreach (var h in _dictHeaderLines)
            sw.WriteLine(h);

        foreach (var en in _allDictEntries.OrderBy(x => x.Code, StringComparer.Ordinal).ThenByDescending(x => x.Weight))
        {
            string line = $"{en.Word}\t{en.Code}\t{en.Weight}";
            if (!string.IsNullOrEmpty(en.Stem))
                line += $"\t{en.Stem}";
            sw.WriteLine(line);
        }

        InfoMessage?.Invoke(this, "保存成功");
        DictSaveEnabled = false;

        // 与 WinForms 一致：保存后按当前五笔方案同步复制文件
        int index = SelectedDictIndex;
        if (index >= 0 && index <= 5)
        {
            var version = SelectedWubiVersion;
            bool matches = (index, version) switch
            {
                (0, "86") => true,
                (1, "98") => true,
                (2, "06") => true,
                (3, "986") => true,
                (4, "tiger") => true,
                (5, "custom") => true,
                _ => false
            };
            if (matches)
                _ = ForceReplaceSchemaAsync(version);
        }
    }

    // ============================================================
    // 保存 / 部署
    // ============================================================

    public async Task<bool> SaveSettingsAsync()
    {
        StatusText = "正在保存配置...";
        try
        {
            StatusText = "正在通知输入法引擎更新...";
            await Task.Run(() =>
            {
                YamlHelper.RunDeploy();
                Thread.Sleep(500);
            });
            StatusText = "设置已生效！";
            return true;
        }
        catch (Exception ex)
        {
            StatusText = "保存失败";
            ErrorOccurred?.Invoke(this, "保存时发生错误: " + ex.Message);
            return false;
        }
    }

    [RelayCommand]
    private async Task Redeploy()
    {
        StatusText = "正在重新部署...";
        try
        {
            await Task.Run(() =>
            {
                YamlHelper.RunDeploy();
                Thread.Sleep(500);
            });
            StatusText = "重新部署完成";
        }
        catch (Exception ex)
        {
            StatusText = "部署失败";
            ErrorOccurred?.Invoke(this, ex.Message);
        }
    }
}
