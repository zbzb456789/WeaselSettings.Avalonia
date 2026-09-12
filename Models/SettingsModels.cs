using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WeaselSettings.Avalonia.Models;

public sealed class ColorSchemeItem
{
    public ColorSchemeItem(string id, string name)
    {
        Id = id;
        Name = name;
    }

    public string Id { get; }
    public string Name { get; }

    public override string ToString() => Name;
}

public partial class SchemaOption : ObservableObject
{
    public SchemaOption(string id, string name, bool isEnabled)
    {
        Id = id;
        Name = name;
        IsEnabled = isEnabled;
    }

    public string Id { get; }
    public string Name { get; }

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }
}

public partial class ColorSetting : ObservableObject
{
    private string _value;

    public ColorSetting(string key, string displayName, string value)
    {
        Key = key;
        DisplayName = displayName;
        _value = value;
    }

    public string Key { get; }
    public string DisplayName { get; }

    public string Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            _value = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PreviewBrush));
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public IBrush PreviewBrush => new SolidColorBrush(RimeColor.Parse(Value));

    public event EventHandler? ValueChanged;
}

/// <summary>
/// Rime/Weasel colours are stored as 0xAABBGGRR (or 0xBBGGRR).
/// Keeping this conversion in one place avoids silently swapping red and blue
/// when a user edits a custom colour scheme.
/// </summary>
public static class RimeColor
{
    public static Color Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Colors.Transparent;

        var hex = value.Trim();
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex[2..];

        if (!uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var numeric))
            return Colors.Transparent;

        if (hex.Length <= 6)
        {
            return Color.FromArgb(
                255,
                (byte)(numeric & 0xff),
                (byte)((numeric >> 8) & 0xff),
                (byte)((numeric >> 16) & 0xff));
        }

        return Color.FromArgb(
            (byte)((numeric >> 24) & 0xff),
            (byte)(numeric & 0xff),
            (byte)((numeric >> 8) & 0xff),
            (byte)((numeric >> 16) & 0xff));
    }

    public static string ToRime(Color color) =>
        $"0x{color.A:X2}{color.B:X2}{color.G:X2}{color.R:X2}";

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var hex = value.Trim();
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex[2..];

        return (hex.Length == 6 || hex.Length == 8) &&
               uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out _);
    }

    public static bool TryParse(string? value, out Color color)
    {
        color = Colors.Transparent;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var hex = value.Trim();
        if (hex.StartsWith('#') && hex.Length == 7)
        {
            return Color.TryParse(hex, out color);
        }

        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex[2..];

        if (hex.Length is 6 or 8 &&
            uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var numeric))
        {
            if (hex.Length == 6)
            {
                color = Color.FromArgb(
                    255,
                    (byte)(numeric & 0xff),
                    (byte)((numeric >> 8) & 0xff),
                    (byte)((numeric >> 16) & 0xff));
            }
            else
            {
                color = Color.FromArgb(
                    (byte)((numeric >> 24) & 0xff),
                    (byte)(numeric & 0xff),
                    (byte)((numeric >> 8) & 0xff),
                    (byte)((numeric >> 16) & 0xff));
            }
            return true;
        }

        return Color.TryParse(hex, out color);
    }
}
