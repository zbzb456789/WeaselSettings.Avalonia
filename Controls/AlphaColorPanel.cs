using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace WeaselSettings.Avalonia.Controls;

/// <summary>
/// 带透明棋盘格的纯色预览块，对应 WinForms 的 AlphaColorPanel。
/// </summary>
public class AlphaColorPanel : Control
{
    public static readonly StyledProperty<Color> DisplayColorProperty =
        AvaloniaProperty.Register<AlphaColorPanel, Color>(nameof(DisplayColor), Colors.Transparent);

    public Color DisplayColor
    {
        get => GetValue(DisplayColorProperty);
        set => SetValue(DisplayColorProperty, value);
    }

    static AlphaColorPanel()
    {
        AffectsRender<AlphaColorPanel>(DisplayColorProperty);
    }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(Bounds.Size);
        DrawCheckerboard(context, rect);
        context.FillRectangle(new SolidColorBrush(DisplayColor), rect);

        var pen = new Pen(Brushes.Gray, 1);
        context.DrawRectangle(pen, rect.Deflate(0.5));
    }

    public static void DrawCheckerboard(DrawingContext context, Rect rect)
    {
        const double size = 8;
        var light = new SolidColorBrush(Colors.LightGray);
        var white = Brushes.White;

        for (double y = rect.Y; y < rect.Bottom; y += size)
        {
            for (double x = rect.X; x < rect.Right; x += size)
            {
                var cell = new Rect(x, y, size, size);
                var odd = ((int)(x / size) + (int)(y / size)) % 2 == 1;
                context.FillRectangle(odd ? light : white, cell);
            }
        }
    }
}
