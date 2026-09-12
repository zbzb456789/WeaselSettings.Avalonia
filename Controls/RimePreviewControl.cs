using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WeaselSettings.Avalonia.Controls;

/// <summary>
/// Rime 候选框实时预览。移植自 WinForms 版 RimePreviewControl 的 GDI+ 绘制逻辑，
/// 使用 Avalonia DrawingContext 渲染，支持横排/竖排、19 色、阴影与自定义候选序号。
/// </summary>
public class RimePreviewControl : Control
{
    private RimeStyle? _style;
    private string[]? _customLabels;

    // 模拟预览数据（与 WinForms 版保持一致）
    private readonly string _preeditText = "aaaa";
    private readonly string _readingText = "^ 读音：gōng";
    private readonly (string Text, string Root)[] _cands =
    [
        ("工", "[ 工工工工・aaaa・(43) ]"),
        ("恭恭敬敬", "[ 廿廿艹艹・aaaa ]"),
        ("藏匿", "[ 艹厂匚艹・aaaa ]"),
        ("花花草草", "[ 艹艹艹艹・aaaa ]"),
        ("㠭", "[ 工工工工・aaaa・(22068) ]")
    ];

    public RimePreviewControl()
    {
        ClipToBounds = true;
    }

    public void BindStyle(RimeStyle style)
    {
        _style = style;
        _style.StyleChanged += (_, _) => InvalidateVisual();
        InvalidateVisual();
    }

    public void SetSelectLabels(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue) || rawValue.Trim() == "null")
        {
            _customLabels = null;
        }
        else
        {
            try
            {
                string clean = rawValue.Trim();
                if (clean.StartsWith('[') && clean.EndsWith(']'))
                    clean = clean[1..^1];

                _customLabels = clean.Split([','], StringSplitOptions.RemoveEmptyEntries)
                                     .Select(s => s.Trim())
                                     .ToArray();
            }
            catch
            {
                _customLabels = null;
            }
        }

        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_style is null)
            return new Size(100, 100);

        var (winW, winH) = _style.VerticalText
            ? MeasureVerticalWindow()
            : MeasureHorizontalWindow();

        // 对应 WinForms 的 UpdateScrollRange：画布比窗口略大，留出滚动空间
        return new Size(winW + 35, winH + 20);
    }

    public override void Render(DrawingContext context)
    {
        if (_style is null)
            return;

        if (_style.VerticalText)
            DrawVerticalLayout(context);
        else
            DrawHorizontalLayout(context);
    }

    // ============================================================
    // 横排布局（Horizontal / Vertical List）
    // ============================================================
    private void DrawHorizontalLayout(DrawingContext context)
    {
        var style = _style!;
        var fMain = CreateTypeface(style.FontFace);
        var fLabel = CreateTypeface(style.LabelFontFace);
        var fComment = CreateTypeface(style.CommentFontFace);

        var sPreedit = MeasureText(_preeditText, fMain, style.FontPoint);
        var sReading = MeasureText(_readingText, fMain, style.FontPoint);

        double preeditBoxW = sPreedit.Width + 8;
        double preeditBoxH = sPreedit.Height + 4;
        double preeditRowW = preeditBoxW + sReading.Width + 10;

        var measures = new (Size L, Size T, Size C, double W, double H)[_cands.Length];
        double candTotalW = 0, candTotalH = 0, maxItemH = 0;

        for (int i = 0; i < _cands.Length; i++)
        {
            string labelText = GetLabelText(i);
            var sl = MeasureText(labelText, fLabel, style.LabelFontPoint);
            var st = MeasureText(_cands[i].Text, fMain, style.FontPoint);
            var sc = MeasureText(_cands[i].Root, fComment, style.CommentFontPoint);

            double w = sl.Width + st.Width + sc.Width + style.HilitePaddingX * 2 + 10;
            double h = Math.Max(st.Height, Math.Max(sl.Height, sc.Height)) + style.HilitePaddingY * 2;
            measures[i] = (sl, st, sc, w, h);

            if (style.Horizontal)
            {
                candTotalW += w + style.CandidateSpacing - 10;
                maxItemH = Math.Max(maxItemH, h);
            }
            else
            {
                candTotalW = Math.Max(candTotalW, w);
                candTotalH += h + style.CandidateSpacing - 10;
            }
        }

        double contentW = Math.Max(preeditRowW, candTotalW);
        double contentH = preeditBoxH + (style.Spacing - 12) +
                          (style.Horizontal ? maxItemH : candTotalH);

        int winW = (int)Math.Clamp(contentW + (style.MarginX - 5) * 2, style.MinWidth, style.MaxWidth);
        int winH = (int)Math.Clamp(contentH + (style.MarginY - 5) * 2, style.MinHeight, style.MaxHeight);

        var rectWin = new Rect(20, 20, winW, winH);
        DrawWindowBase(context, rectWin);

        double curX = rectWin.X + (style.MarginX - 5);
        double curY = rectWin.Y + (style.MarginY - 5);

        // ---- 编码区 ----
        var preeditRect = new Rect(curX, curY, preeditBoxW, preeditBoxH);
        DrawItemShadow(context, preeditRect, style.hilited_shadow_color, style.ShadowRadius);
        DrawRoundedRect(context, preeditRect, 8, new SolidColorBrush(style.hilited_back_color), null);
        context.DrawText(MakeText(_preeditText, fMain, style.FontPoint, style.hilited_text_color),
            new Point(curX + 4, curY + 2));
        context.DrawText(MakeText(_readingText, fMain, style.FontPoint, style.text_color),
            new Point(curX + preeditBoxW + 5, curY + 2));

        curY += preeditBoxH + (style.Spacing - 12);

        // ---- 候选区 ----
        for (int i = 0; i < _cands.Length; i++)
        {
            var m = measures[i];
            bool isHi = i == 0;

            var itemRect = new Rect(
                curX,
                curY,
                style.Horizontal ? m.W : rectWin.Width - (style.MarginX - 5) * 2,
                m.H);

            if (isHi)
            {
                DrawItemShadow(context, itemRect, style.hilited_candidate_shadow_color);
                DrawRoundedRect(context, itemRect, 8, new SolidColorBrush(style.hilited_candidate_back_color), null);
                if (style.hilited_candidate_border_color.A > 0)
                    DrawRoundedRect(context, itemRect, 8, null, new Pen(new SolidColorBrush(style.hilited_candidate_border_color), 1));
                if (style.hilited_mark_color.A > 0)
                    context.FillRectangle(new SolidColorBrush(style.hilited_mark_color),
                        new Rect(itemRect.X + 2, itemRect.Y + 4, 3, itemRect.Height - 8));
            }
            else
            {
                DrawItemShadow(context, itemRect, style.candidate_shadow_color);
                if (style.candidate_back_color.A > 0)
                    DrawRoundedRect(context, itemRect, 8, new SolidColorBrush(style.candidate_back_color), null);
                if (style.candidate_border_color.A > 0)
                    DrawRoundedRect(context, itemRect, 8, null, new Pen(new SolidColorBrush(style.candidate_border_color), 1));
            }

            double tx = itemRect.X + style.HilitePaddingX;
            double ty = itemRect.Y + style.HilitePaddingY;
            string labelText = GetLabelText(i);

            context.DrawText(MakeText(labelText, fLabel, style.LabelFontPoint, isHi ? style.hilited_label_color : style.label_color),
                new Point(tx, ty));
            context.DrawText(MakeText(_cands[i].Text, fMain, style.FontPoint, isHi ? style.hilited_candidate_text_color : style.candidate_text_color),
                new Point(tx + m.L.Width, ty));
            context.DrawText(MakeText(_cands[i].Root, fComment, style.CommentFontPoint, isHi ? style.hilited_comment_text_color : style.comment_text_color),
                new Point(tx + m.L.Width + m.T.Width + 5, ty + 2));

            if (style.Horizontal)
                curX += m.W + style.CandidateSpacing - 10;
            else
                curY += m.H + style.CandidateSpacing - 10;
        }
    }

    // ============================================================
    // 竖排布局（文字竖向显示）
    // ============================================================
    private void DrawVerticalLayout(DrawingContext context)
    {
        var style = _style!;
        var fMain = CreateTypeface(style.FontFace);
        var fLabel = CreateTypeface(style.LabelFontFace);
        var fComment = CreateTypeface(style.CommentFontFace);

        // 测量编码区（按字符竖排累计）
        var sPre = MeasureVerticalText(_preeditText, fMain, style.FontPoint);
        var sRead = MeasureVerticalText(_readingText, fComment, style.CommentFontPoint);

        double preW = Math.Max(sPre.Width, sRead.Width) + style.HilitePaddingX * 2;
        double preH = sPre.Height + sRead.Height + 10;

        var ms = new (Size L, Size T, Size C, double W, double H)[_cands.Length];
        double candTotalW = 0;
        double maxCandH = 0;

        for (int i = 0; i < _cands.Length; i++)
        {
            string labelText = GetLabelText(i);
            var sl = MeasureVerticalText(labelText, fLabel, style.LabelFontPoint);
            var st = MeasureVerticalText(_cands[i].Text, fMain, style.FontPoint);
            var sc = MeasureVerticalText(_cands[i].Root, fComment, style.CommentFontPoint);

            double w = Math.Max(st.Width, sc.Width) + style.HilitePaddingX * 2;
            double h = sl.Height + st.Height + sc.Height + style.HilitePaddingY * 2;

            ms[i] = (sl, st, sc, w, h);
            candTotalW += w + style.CandidateSpacing - 10;
            maxCandH = Math.Max(maxCandH, h);
        }

        int winW = (int)Math.Clamp(
            preW + (style.Spacing - 12) + candTotalW + (style.MarginX - 5) * 2,
            style.MinWidth, style.MaxWidth);
        int winH = (int)Math.Clamp(
            Math.Max(maxCandH, 200) + (style.MarginY - 5) * 2,
            style.MinHeight, style.MaxHeight);

        var rectWin = new Rect(20, 20, winW, winH);
        DrawWindowBase(context, rectWin);

        bool ltr = style.VerticalTextLeftToRight;
        bool isRev = style.VerticalAutoReverse;

        double curX = ltr
            ? rectWin.X + (style.MarginX - 5)
            : rectWin.Right - (style.MarginX - 5) - preW;
        double contentTop = rectWin.Y + (style.MarginY - 5);
        double contentBottom = rectWin.Bottom - (style.MarginY - 5);

        // 编码区
        var preRect = new Rect(curX, contentTop, preW, preH);
        DrawItemShadow(context, preRect, style.hilited_shadow_color, style.ShadowRadius);
        DrawRoundedRect(context, preRect, 8, new SolidColorBrush(style.hilited_back_color), null);
        DrawVerticalText(context, _preeditText, fMain, style.FontPoint, style.hilited_text_color,
            curX + style.HilitePaddingX, contentTop);
        DrawVerticalText(context, _readingText, fComment, style.CommentFontPoint, style.text_color,
            curX + style.HilitePaddingX, contentTop + sPre.Height + 5);

        curX += ltr ? (preW + (style.Spacing - 12)) : -(style.Spacing - 12);

        for (int i = 0; i < _cands.Length; i++)
        {
            var m = ms[i];
            bool isHi = i == 0;

            if (!ltr) curX -= m.W;

            double itemY = isRev ? contentBottom - m.H : contentTop;
            var r = new Rect(curX, itemY, m.W, m.H);

            if (isHi)
            {
                DrawItemShadow(context, r, style.hilited_candidate_shadow_color);
                DrawRoundedRect(context, r, 6, new SolidColorBrush(style.hilited_candidate_back_color), null);
                if (style.hilited_candidate_border_color.A > 0)
                    DrawRoundedRect(context, r, 6, null, new Pen(new SolidColorBrush(style.hilited_candidate_border_color), 1));
                if (style.hilited_mark_color.A > 0)
                {
                    if (isRev)
                        context.FillRectangle(new SolidColorBrush(style.hilited_mark_color),
                            new Rect(r.X + 4, r.Bottom - 5, r.Width - 8, 3));
                    else
                        context.FillRectangle(new SolidColorBrush(style.hilited_mark_color),
                            new Rect(r.X + 4, r.Y + 2, r.Width - 8, 3));
                }
            }
            else
            {
                DrawItemShadow(context, r, style.candidate_shadow_color);
                if (style.candidate_back_color.A > 0)
                    DrawRoundedRect(context, r, 6, new SolidColorBrush(style.candidate_back_color), null);
                if (style.candidate_border_color.A > 0)
                    DrawRoundedRect(context, r, 6, null, new Pen(new SolidColorBrush(style.candidate_border_color), 1));
            }

            double textY = itemY + style.HilitePaddingY;
            var cLabel = isHi ? style.hilited_label_color : style.label_color;
            var cText = isHi ? style.hilited_candidate_text_color : style.candidate_text_color;
            var cCmt = isHi ? style.hilited_comment_text_color : style.comment_text_color;
            string labelText = GetLabelText(i);

            if (isRev)
            {
                DrawVerticalText(context, _cands[i].Root, fComment, style.CommentFontPoint, cCmt,
                    curX + (m.W - m.C.Width) / 2, textY);
                DrawVerticalText(context, _cands[i].Text, fMain, style.FontPoint, cText,
                    curX + (m.W - m.T.Width) / 2, textY + m.C.Height);
                DrawVerticalText(context, labelText, fLabel, style.LabelFontPoint, cLabel,
                    curX + (m.W - m.L.Width) / 2, textY + m.C.Height + m.T.Height);
            }
            else
            {
                DrawVerticalText(context, labelText, fLabel, style.LabelFontPoint, cLabel,
                    curX + (m.W - m.L.Width) / 2, textY);
                DrawVerticalText(context, _cands[i].Text, fMain, style.FontPoint, cText,
                    curX + (m.W - m.T.Width) / 2, textY + m.L.Height);
                DrawVerticalText(context, _cands[i].Root, fComment, style.CommentFontPoint, cCmt,
                    curX + (m.W - m.C.Width) / 2, textY + m.L.Height + m.T.Height);
            }

            curX += ltr ? m.W + style.CandidateSpacing - 10 : -(style.CandidateSpacing - 10);
        }
    }

    // ============================================================
    // 公共辅助
    // ============================================================
    private (int W, int H) MeasureHorizontalWindow()
    {
        var style = _style!;
        var fMain = CreateTypeface(style.FontFace);
        var fComment = CreateTypeface(style.CommentFontFace);

        var sPreedit = MeasureText(_preeditText, fMain, style.FontPoint);
        var sReading = MeasureText(_readingText, fMain, style.FontPoint);
        double preeditBoxW = sPreedit.Width + 8;
        double preeditBoxH = sPreedit.Height + 4;
        double preeditRowW = preeditBoxW + sReading.Width + 10;

        double candTotalW = 0, candTotalH = 0, maxItemH = 0;
        for (int i = 0; i < _cands.Length; i++)
        {
            var sl = MeasureText(GetLabelText(i), CreateTypeface(style.LabelFontFace), style.LabelFontPoint);
            var st = MeasureText(_cands[i].Text, fMain, style.FontPoint);
            var sc = MeasureText(_cands[i].Root, fComment, style.CommentFontPoint);
            double w = sl.Width + st.Width + sc.Width + style.HilitePaddingX * 2 + 10;
            double h = Math.Max(st.Height, Math.Max(sl.Height, sc.Height)) + style.HilitePaddingY * 2;

            if (style.Horizontal)
            {
                candTotalW += w + style.CandidateSpacing - 10;
                maxItemH = Math.Max(maxItemH, h);
            }
            else
            {
                candTotalW = Math.Max(candTotalW, w);
                candTotalH += h + style.CandidateSpacing - 10;
            }
        }

        double contentW = Math.Max(preeditRowW, candTotalW);
        double contentH = preeditBoxH + (style.Spacing - 12) + (style.Horizontal ? maxItemH : candTotalH);
        int winW = (int)Math.Clamp(contentW + (style.MarginX - 5) * 2, style.MinWidth, style.MaxWidth);
        int winH = (int)Math.Clamp(contentH + (style.MarginY - 5) * 2, style.MinHeight, style.MaxHeight);
        return (winW, winH);
    }

    private (int W, int H) MeasureVerticalWindow()
    {
        var style = _style!;
        var fMain = CreateTypeface(style.FontFace);
        var fComment = CreateTypeface(style.CommentFontFace);

        var sPre = MeasureVerticalText(_preeditText, fMain, style.FontPoint);
        var sRead = MeasureVerticalText(_readingText, fComment, style.CommentFontPoint);
        double preW = Math.Max(sPre.Width, sRead.Width) + style.HilitePaddingX * 2;
        double preH = sPre.Height + sRead.Height + 10;

        double candTotalW = 0, maxCandH = 0;
        for (int i = 0; i < _cands.Length; i++)
        {
            var sl = MeasureVerticalText(GetLabelText(i), CreateTypeface(style.LabelFontFace), style.LabelFontPoint);
            var st = MeasureVerticalText(_cands[i].Text, fMain, style.FontPoint);
            var sc = MeasureVerticalText(_cands[i].Root, fComment, style.CommentFontPoint);
            double w = Math.Max(st.Width, sc.Width) + style.HilitePaddingX * 2;
            double h = sl.Height + st.Height + sc.Height + style.HilitePaddingY * 2;
            candTotalW += w + style.CandidateSpacing - 10;
            maxCandH = Math.Max(maxCandH, h);
        }

        int winW = (int)Math.Clamp(preW + (style.Spacing - 12) + candTotalW + (style.MarginX - 5) * 2, style.MinWidth, style.MaxWidth);
        int winH = (int)Math.Clamp(Math.Max(maxCandH, 200) + (style.MarginY - 5) * 2, style.MinHeight, style.MaxHeight);
        return (winW, winH);
    }

    private void DrawWindowBase(DrawingContext context, Rect rect)
    {
        var style = _style!;

        if (style.shadow_color.A > 0 && style.ShadowRadius > 0)
        {
            for (int i = style.ShadowRadius; i > 0; i--)
            {
                var sRect = rect.Inflate(i * 2.0);
                int alpha = (int)Math.Clamp(style.shadow_color.A * (0.0 / i), 8, 255);
                using (context.PushOpacity(alpha / 255.0))
                {
                    DrawRoundedRect(context, sRect, style.CornerRadius + i,
                        new SolidColorBrush(style.shadow_color), null);
                }
            }
        }

        DrawRoundedRect(context, rect, style.CornerRadius, new SolidColorBrush(style.back_color), null);

        if (style.BorderWidth > 0)
        {
            DrawRoundedRect(context, rect, style.CornerRadius, null,
                new Pen(new SolidColorBrush(style.border_color), style.BorderWidth));
        }
    }

    private void DrawItemShadow(DrawingContext context, Rect rect, Color color, double radius = 6)
    {
        if (color.A == 0 || radius <= 0 || _style is null)
            return;

        for (int i = (int)radius; i > 0; i--)
        {
            var r = rect.Inflate(i * 1.8);
            // 注意：color.A 可能小于 4（用户把 Alpha 拉到 0~3），
            // 此时 Math.Clamp(值, 4, color.A) 会因 min > max 抛异常，必须把下限也一起收紧
            int alpha = Math.Clamp((int)(color.A * (1f / (i + 18))), Math.Min(4, (int)color.A), color.A);
            using (context.PushOpacity(alpha / 255.0))
            {
                DrawRoundedRect(context, r, _style.CornerRadius + i, new SolidColorBrush(color), null);
            }
        }
    }

    private static void DrawRoundedRect(DrawingContext context, Rect rect, double radius, IBrush? brush, IPen? pen)
    {
        double d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
        if (d <= 0)
        {
            context.DrawRectangle(brush, pen, rect);
            return;
        }

        context.DrawRectangle(brush, pen, new RoundedRect(rect, d / 2, d / 2));
    }

    private static Typeface CreateTypeface(string fontName)
    {
        try
        {
            return new Typeface(new FontFamily(fontName));
        }
        catch
        {
            return Typeface.Default;
        }
    }

    private static FormattedText MakeText(string text, Typeface typeface, double size, Color color)
    {
        return new FormattedText(
            string.IsNullOrEmpty(text) ? " " : text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            size,
            new SolidColorBrush(color));
    }

    private static Size MeasureText(string text, Typeface typeface, double size)
    {
        var ft = MakeText(text, typeface, size, Colors.Black);
        return new Size(ft.Width, ft.Height);
    }

    private static Size MeasureVerticalText(string text, Typeface typeface, double size)
    {
        double width = 0, height = 0;
        foreach (var ch in string.IsNullOrEmpty(text) ? " " : text)
        {
            var ft = MakeText(ch.ToString(), typeface, size, Colors.Black);
            width = Math.Max(width, ft.Width);
            height += ft.Height;
        }
        return new Size(width, height);
    }

    private static void DrawVerticalText(DrawingContext context, string text, Typeface typeface, double size,
        Color color, double x, double y)
    {
        double curY = y;
        foreach (var ch in string.IsNullOrEmpty(text) ? " " : text)
        {
            var ft = MakeText(ch.ToString(), typeface, size, color);
            context.DrawText(ft, new Point(x, curY));
            curY += ft.Height;
        }
    }

    private string GetLabelText(int index)
    {
        if (_customLabels != null && index < _customLabels.Length)
            return _customLabels[index];

        return $"{index + 1}.";
    }
}
