using System;
using Avalonia.Media;
using System.Collections.Generic;

namespace WeaselSettings
{
    public class RimeStyle
    {
        public string ColorScheme = "default";   // 对应 color_scheme

        // --- 字体设置 ---
        public string FontFace = "微软雅黑";//微软雅黑
        public int FontPoint = 14;

        public string LabelFontFace = "微软雅黑";//微软雅黑
        public int LabelFontPoint = 14;

        public string CommentFontFace = "通用字根"; // 对应 comment_font_face通用字根
        public int CommentFontPoint = 14;

        // --- 行为与方向布尔值 ---
        public bool Horizontal = false;                  // 对应 horizontal (候选词横排/竖排)
        public bool VerticalText = false;                // 对应 vertical_text (文字本身竖写，如古籍排版)
        public bool VerticalTextLeftToRight = false;     // 对应 vertical_text_left_to_right
        public bool VerticalTextWithWrap = false;        // 对应 vertical_text_with_wrap
        public bool VerticalAutoReverse = false;         // 对应 vertical_auto_reverse

        // --- 布局尺寸参数 (Layout) ---
        public string AlignType = "center";              // 对应 align_type (center, top, bottom...)

        public int MarginX = 12;             // margin_x
        public int MarginY = 12;             // margin_y

        public int Spacing = 18;             // spacing: 编码行与候选区间隔
        public int CandidateSpacing = 15;    // candidate_spacing: 候选词间隔
        public int HiliteSpacing = 10;       // hilite_spacing: 特定高亮间隔 (部分皮肤引擎使用)

        public int HilitePaddingX = 5;       // hilite_padding_x
        public int HilitePaddingY = 5;       // hilite_padding_y
        // 注：YAML 若只有 hilite_padding，需在解析时同时赋值给 X 和 Y

        public int ShadowOffsetX = 0;
        public int ShadowOffsetY = 0;
        public int ShadowRadius = 8;         // 对应 shadow_radius (阴影模糊半径)

        public int CornerRadius = 8;         // corner_radius (也对应 round_corner)
        public int BorderWidth = 2;          // border_width (也对应 border)

        public int MinWidth = 80;            // min_width
        public int MaxWidth = 2800;          // max_width
        public int MinHeight = 0;            // min_height
        public int MaxHeight = 2800;         // max_height

        // --- 颜色参数 ---
        public Color text_color = ParseRimeColor("0xFF00FFFF");
        public Color back_color = ParseRimeColor("0xFF1A1A1A");
        public Color border_color = ParseRimeColor("0xFF5000FF");
        public Color shadow_color = ParseRimeColor("0x404B00FF");

        // 编码区
        public Color hilited_back_color = ParseRimeColor("0xFF2A2A2A");
        public Color hilited_text_color = ParseRimeColor("0xFF5000FF");
        public Color hilited_shadow_color = ParseRimeColor("0x304B00FF");

        // 高亮候选词
        public Color comment_text_color = ParseRimeColor("0xFF00FFFF");
        public Color hilited_mark_color = ParseRimeColor("0xFF5000FF");
        public Color hilited_candidate_back_color = ParseRimeColor("0xFF2A2A2A");
        public Color hilited_candidate_shadow_color = ParseRimeColor("0xFF80FF00");
        public Color hilited_label_color = ParseRimeColor("0xFF5000FF");
        public Color hilited_candidate_text_color = ParseRimeColor("0xFF5000FF");
        public Color hilited_candidate_border_color = ParseRimeColor("0xFF5000FF");
        public Color hilited_comment_text_color = ParseRimeColor("0xFF66FFFF");

        // 非高亮候选词
        public Color candidate_back_color = ParseRimeColor("0xFF1A1A1A");
        public Color candidate_text_color = ParseRimeColor("0xFFFFFFFF");
        public Color candidate_shadow_color = ParseRimeColor("0x204B00FF");
        public Color label_color = ParseRimeColor("0xFFF5FF00");
        public Color candidate_border_color = ParseRimeColor("0xFFF5FF00");


        public event EventHandler? StyleChanged;
        public void TriggerChange() => StyleChanged?.Invoke(this, EventArgs.Empty);

        public static Color ParseRimeColor(string? hexStr)
        {
            // 如果传入 null 或空字符串，直接返回透明
            if (string.IsNullOrWhiteSpace(hexStr))
                return Colors.Transparent;

            string text = hexStr.Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                text = text[2..];

            // 解析十六进制
            if (!uint.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out uint hex))
                return Colors.Transparent;

            byte a, r, g, b;

            // 关键逻辑：区分 0x000000 (6位) 和 0x00000000 (8位)
            if (text.Length <= 6)
            {
                a = 255; // 6位色默认为不透明
                b = (byte)((hex >> 16) & 0xFF);
                g = (byte)((hex >> 8) & 0xFF);
                r = (byte)(hex & 0xFF);
            }
            else
            {
                // 8位色（包含 Alpha），尊重原始值，0x00000000 此时 a 就是 0
                a = (byte)((hex >> 24) & 0xFF);
                b = (byte)((hex >> 16) & 0xFF);
                g = (byte)((hex >> 8) & 0xFF);
                r = (byte)(hex & 0xFF);
            }

            return Color.FromArgb(a, r, g, b);
        }

        public static string ToRimeColor(Color color)
        {
            // Rime 使用 0xAARRGGBB
            return $"0x{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        }

    }
}