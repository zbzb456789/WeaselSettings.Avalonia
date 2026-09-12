using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using WeaselSettings.Avalonia.Models;

namespace WeaselSettings.Avalonia.Views.Dialogs;

public partial class ColorPickerWindow : Window
{
    private bool _syncing;

    public ColorPickerWindow()
    {
        InitializeComponent();

        RedSlider.PropertyChanged += (_, e) => { if (e.Property.Name == nameof(Slider.Value)) OnChannelChanged(); };
        GreenSlider.PropertyChanged += (_, e) => { if (e.Property.Name == nameof(Slider.Value)) OnChannelChanged(); };
        BlueSlider.PropertyChanged += (_, e) => { if (e.Property.Name == nameof(Slider.Value)) OnChannelChanged(); };
        AlphaSlider.PropertyChanged += (_, e) => { if (e.Property.Name == nameof(Slider.Value)) OnChannelChanged(); };

        HexBox.TextChanged += (_, _) => OnHexChanged();
        OkButton.Click += OnOkClick;
        CancelButton.Click += (_, _) => Close();
    }

    public Color? SelectedColor { get; private set; }

    public static Task<Color?> PickAsync(Window owner, Color initial)
    {
        var picker = new ColorPickerWindow
        {
            SelectedColor = initial
        };
        return picker.ShowAsync(owner);
    }

    private async Task<Color?> ShowAsync(Window owner)
    {
        SetColor(SelectedColor ?? Colors.Black);
        await ShowDialog(owner);
        return SelectedColor;
    }

    private Color CurrentColor => Color.FromArgb(
        (byte)AlphaSlider.Value,
        (byte)RedSlider.Value,
        (byte)GreenSlider.Value,
        (byte)BlueSlider.Value);

    private void SetColor(Color color)
    {
        _syncing = true;
        RedSlider.Value = color.R;
        GreenSlider.Value = color.G;
        BlueSlider.Value = color.B;
        AlphaSlider.Value = color.A;
        _syncing = false;
        UpdatePreview();
        UpdateHex();
    }

    private void OnChannelChanged()
    {
        if (_syncing) return;
        UpdatePreview();
        UpdateHex();
    }

    private void OnHexChanged()
    {
        if (_syncing) return;

        var text = HexBox.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        if (RimeColor.TryParse(text, out var color))
        {
            SetColor(color);
        }
    }

    private void UpdatePreview()
    {
        PreviewPanel.DisplayColor = CurrentColor;
    }

    private void UpdateHex()
    {
        var color = CurrentColor;
        _syncing = true;
        HexBox.Text = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        HexBox.CaretIndex = HexBox.Text?.Length ?? 0;
        _syncing = false;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        SelectedColor = CurrentColor;
        Close();
    }
}
