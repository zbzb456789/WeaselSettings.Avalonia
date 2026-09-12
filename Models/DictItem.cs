namespace WeaselSettings.Avalonia.Models;

/// <summary>
/// 内存中的单行词条，兼容短语库与五笔用户词库两种场景。
/// </summary>
public class DictItem
{
    public string Code { get; set; } = string.Empty;

    public string Word { get; set; } = string.Empty;

    public int Weight { get; set; }

    public bool IsNew { get; set; }
}
