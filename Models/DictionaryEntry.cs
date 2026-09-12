using CommunityToolkit.Mvvm.ComponentModel;

namespace WeaselSettings.Avalonia.Models;

public partial class DictionaryEntry : ObservableObject
{
    [ObservableProperty]
    public partial string Word { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Code { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int Weight { get; set; }

    [ObservableProperty]
    public partial string Stem { get; set; } = string.Empty;

    public DictionaryEntry Clone() => new()
    {
        Word = Word,
        Code = Code,
        Weight = Weight,
        Stem = Stem
    };
}
