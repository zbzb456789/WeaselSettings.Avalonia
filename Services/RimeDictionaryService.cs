using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WeaselSettings.Avalonia.Models;

namespace WeaselSettings.Avalonia.Services;

/// <summary>
/// Reads and writes the two dictionary formats used by the original WinForms
/// application.  This is deliberately independent from the UI so dictionary
/// changes are never lost while a filtered DataGrid is being displayed.
/// </summary>
public static class RimeDictionaryService
{
    public static (List<string> Header, List<DictionaryEntry> Entries) LoadYamlDictionary(string filePath)
    {
        var header = new List<string>();
        var entries = new List<DictionaryEntry>();

        if (!File.Exists(filePath))
            return (header, entries);

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, new UTF8Encoding(false));
        var isBody = false;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (!isBody)
            {
                header.Add(line);
                if (line.Trim() == "...")
                    isBody = true;
                continue;
            }

            if (TryParseDictionaryLine(line, out var entry))
                entries.Add(entry);
        }

        return (header, entries);
    }

    public static void SaveYamlDictionary(string filePath, IEnumerable<string> header, IEnumerable<DictionaryEntry> entries)
    {
        EnsureParentDirectory(filePath);
        var headerLines = header.ToList();
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));

        foreach (var line in headerLines)
            writer.WriteLine(line);

        if (!headerLines.Any(line => line.Trim() == "..."))
            writer.WriteLine("...");

        foreach (var entry in entries.OrderBy(item => item.Code, StringComparer.Ordinal)
                                     .ThenByDescending(item => item.Weight))
        {
            var line = $"{entry.Word}\t{entry.Code}\t{Math.Max(0, entry.Weight)}";
            if (!string.IsNullOrWhiteSpace(entry.Stem))
                line += $"\t{entry.Stem}";
            writer.WriteLine(line);
        }
    }

    public static List<DictionaryEntry> LoadPhrases(string filePath)
    {
        var entries = new List<DictionaryEntry>();
        if (!File.Exists(filePath))
            return entries;

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, new UTF8Encoding(false));
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (TryParsePhraseLine(line, out var entry))
                entries.Add(entry);
        }

        return entries;
    }

    public static void SavePhrases(string filePath, IEnumerable<DictionaryEntry> entries)
    {
        EnsureParentDirectory(filePath);
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        foreach (var entry in entries)
            writer.WriteLine($"{entry.Code}\t{entry.Word}");
    }

    public static bool TryParseDictionaryLine(string? line, out DictionaryEntry entry)
    {
        entry = new DictionaryEntry();
        if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
            return false;

        var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            return false;

        string? word = null;
        string? code = null;
        string? stem = null;
        var weight = 0;

        foreach (var part in parts)
        {
            if (int.TryParse(part, out var parsedWeight))
            {
                weight = parsedWeight;
                continue;
            }

            if (IsCode(part))
            {
                if (code is null)
                    code = part.ToLowerInvariant();
                else
                    stem ??= part;
                continue;
            }

            word ??= part;
        }

        if (string.IsNullOrWhiteSpace(word) || string.IsNullOrWhiteSpace(code))
            return false;

        entry = new DictionaryEntry
        {
            Word = word,
            Code = code,
            Weight = Math.Max(0, weight),
            Stem = stem ?? string.Empty
        };
        return true;
    }

    public static bool TryParsePhraseLine(string? line, out DictionaryEntry entry)
    {
        entry = new DictionaryEntry();
        if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
            return false;

        var parts = line.Split('\t', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            return false;

        entry = new DictionaryEntry
        {
            Code = parts[0].ToLowerInvariant(),
            Word = parts[1]
        };
        return true;
    }

    public static Dictionary<string, HashSet<string>> BuildWordCodeIndex(IEnumerable<DictionaryEntry> entries)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (!result.TryGetValue(entry.Word, out var codes))
                result[entry.Word] = codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            codes.Add(entry.Code);
        }

        return result;
    }

    public static Dictionary<char, string> LoadCharacterCodes(string filePath)
    {
        var result = new Dictionary<char, string>();
        if (!File.Exists(filePath))
            return result;

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, new UTF8Encoding(false));
        var isBody = false;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (!isBody)
            {
                isBody = line.Trim() == "...";
                continue;
            }

            var tabIndex = line.IndexOf('\t');
            if (tabIndex <= 0)
                continue;

            var word = line[..tabIndex].Trim();
            if (word.Length != 1 || result.ContainsKey(word[0]))
                continue;

            var payload = line[(tabIndex + 1)..].Trim();
            if (!payload.StartsWith('[') || !payload.EndsWith(']'))
                continue;

            var values = payload[1..^1].Split(',', StringSplitOptions.TrimEntries);
            if (values.Length >= 3 && IsCode(values[2]))
                result[word[0]] = values[2].ToLowerInvariant();
        }

        return result;
    }

    public static string GenerateWubiCode(string word, IReadOnlyDictionary<char, string> characterCodes)
    {
        if (string.IsNullOrWhiteSpace(word) || characterCodes.Count == 0 ||
            word.Any(character => !characterCodes.ContainsKey(character)))
            return string.Empty;

        string Prefix(char character, int length)
        {
            var code = characterCodes[character];
            return code.Length <= length ? code : code[..length];
        }

        return word.Length switch
        {
            1 => characterCodes[word[0]],
            2 => Prefix(word[0], 2) + Prefix(word[1], 2),
            3 => Prefix(word[0], 1) + Prefix(word[1], 1) + Prefix(word[2], 2),
            _ => Prefix(word[0], 1) + Prefix(word[1], 1) + Prefix(word[2], 1) + Prefix(word[^1], 1)
        };
    }

    public static bool IsCode(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.All(character =>
            (character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z'));

    private static void EnsureParentDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
    }
}
