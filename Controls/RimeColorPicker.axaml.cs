using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using WeaselSettings.Avalonia.Views.Dialogs;

namespace WeaselSettings.Avalonia.Controls;

/// <summary>
/// Rime 颜色选择控件：色块预览 + Alpha 滑块 + “…” 打开颜色对话框。
/// 与 WinForms 的 RimeColorPicker 行为一致。
/// </summary>
public partial class RimeColorPicker : UserControl
{
    private Color _currentColor = Colors.Black;
    private bool _syncing;

    public RimeColorPicker()
    {
        InitializeComponent();

        AlphaSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(Slider.Value) && !_syncing)
            {
                var alpha = (byte)AlphaSlider.Value;
                if (alpha != _currentColor.A)
                    CurrentColor = Color.FromArgb(alpha, _currentColor.R, _currentColor.G, _currentColor.B);
            }
        };

        PickButton.Click += OnPickColor;

        ApplyColor();
    }

    public event EventHandler? CurrentColorChanged;

    public Color CurrentColor
    {
        get => _currentColor;
        set
        {
            if (_currentColor == value)
                return;

            _currentColor = value;
            ApplyColor();
            CurrentColorChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Rime 颜色格式 0xAABBGGRR</summary>
    public string CurrentRimeColor => ToRimeColor(_currentColor);

    /// <summary>外部同步（如皮肤切换）时更新颜色但不触发保存事件。</summary>
    public void SetColorSilently(Color color)
    {
        _currentColor = color;
        ApplyColor();
    }

    public static string ToRimeColor(Color color) =>
        $"0x{color.A:X2}{color.B:X2}{color.G:X2}{color.R:X2}";

    private async void OnPickColor(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this) as Window;
        if (topLevel is null)
            return;

        var picked = await ColorPickerWindow.PickAsync(topLevel, Color.FromArgb(255, _currentColor.R, _currentColor.G, _currentColor.B));
        if (picked is { } color)
        {
            CurrentColor = Color.FromArgb(_currentColor.A, color.R, color.G, color.B);
        }
    }

    private void ApplyColor()
    {
        PreviewPanel.DisplayColor = _currentColor;

        if (_currentColor.A != (byte)AlphaSlider.Value)
        {
            _syncing = true;
            AlphaSlider.Value = _currentColor.A;
            _syncing = false;
        }
    }
}
