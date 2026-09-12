using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WeaselSettings.Avalonia.Controls;

public sealed partial class FontOption : ObservableObject
{
    public FontOption(string fontName)
    {
        FontName = fontName;
    }

    public string FontName { get; }

    [ObservableProperty]
    public partial bool IsChecked { get; set; }
}

/// <summary>
/// 多选字体下拉框：点击后弹出字体列表，可勾选多个字体，对应 WinForms 的 MultiFontComboBox。
/// </summary>
public partial class MultiFontComboBox : UserControl
{
    private readonly List<FontOption> _fontOptions = new();

    public MultiFontComboBox()
    {
        InitializeComponent();

        DisplayBox.AddHandler(PointerPressedEvent, OnDisplayPointerPressed, RoutingStrategies.Tunnel);
        FontPopup.Closed += (_, _) => FontList.SelectedItem = null;
    }

    /// <summary>勾选变化时触发，Text 已是最新的逗号分隔字符串。</summary>
    public event EventHandler? FontsChanged;

    public string Text => string.Join(", ", _fontOptions.Where(f => f.IsChecked).Select(f => f.FontName));

    /// <summary>加载系统字体，并根据初始字符串（如“微软雅黑, Segoe UI”）勾选。</summary>
    public void InitializeFonts(string? initialFonts)
    {
        var selectedList = string.IsNullOrWhiteSpace(initialFonts)
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : initialFonts.Split([','], StringSplitOptions.RemoveEmptyEntries)
                          .Select(f => f.Trim())
                          .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _fontOptions.Clear();
        foreach (var family in FontManager.Current.SystemFonts.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
        {
            _fontOptions.Add(new FontOption(family.Name) { IsChecked = selectedList.Contains(family.Name) });
        }

        foreach (var option in _fontOptions)
        {
            option.PropertyChanged += OnFontOptionChanged;
        }

        FontList.ItemsSource = _fontOptions;
        DisplayBox.Text = initialFonts ?? string.Empty;
    }

    private void OnFontOptionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FontOption.IsChecked))
            return;

        DisplayBox.Text = Text;
        FontsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDisplayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        FontPopup.IsOpen = !FontPopup.IsOpen;
        e.Handled = true;
    }
}
