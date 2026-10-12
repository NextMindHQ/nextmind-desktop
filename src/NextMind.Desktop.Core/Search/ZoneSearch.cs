using System.Globalization;
using System.Text;

namespace NextMind.Desktop.Core.Search;

/// <summary>
/// In-zone search: pure text matching over the items' names. It only answers "which items match?"; it never reorders, edits or touches
/// anything. Matching ignores case and diacritics (Polish letters included), every word of the query must occur, order is the zone's own.
/// </summary>
public static class ZoneSearch
{
    /// <summary>Lower-cases and strips combining marks; ł/đ/ø have no decomposition and are mapped explicitly.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            sb.Append(c switch
            {
                'ł' or 'Ł' => 'l',
                'đ' or 'Đ' => 'd',
                'ø' or 'Ø' => 'o',
                _ => char.ToLowerInvariant(c),
            });
        }

        return sb.ToString();
    }

    /// <summary>
    /// The searchable text of one item: the name shown on the tile plus the target's file or folder name (with extension), so a shortcut
    /// shown as "Visual Studio Code" is also found by ".lnk" and a file shown without its extension by "notes.txt". The parent path is not searched.
    /// </summary>
    public static string BuildKey(string? displayName, string? path)
    {
        var fileName = string.IsNullOrEmpty(path) ? string.Empty : System.IO.Path.GetFileName(path.TrimEnd('\\', '/'));
        return Normalize(displayName) + "\n" + Normalize(fileName);
    }

    /// <summary>The query's words, normalised. Empty when nothing usable was typed.</summary>
    public static IReadOnlyList<string> Words(string? query)
        => Normalize(query).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    public static bool Matches(string key, IReadOnlyList<string> words)
    {
        foreach (var word in words)
        {
            if (!key.Contains(word, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Indexes of the matching items in their original order. An empty query matches everything.</summary>
    public static IReadOnlyList<int> Filter(IReadOnlyList<string> keys, string? query)
    {
        var words = Words(query);
        var hits = new List<int>(keys.Count);
        for (var i = 0; i < keys.Count; i++)
        {
            if (words.Count == 0 || Matches(keys[i], words))
            {
                hits.Add(i);
            }
        }

        return hits;
    }
}
