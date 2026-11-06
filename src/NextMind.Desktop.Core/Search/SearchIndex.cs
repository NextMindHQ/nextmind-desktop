using System.Globalization;
using System.Text;

namespace NextMind.Desktop.Core.Search;

public enum SearchEntryKind
{
    Command,
    Zone,
    Application,
    Widget,
}

public sealed record SearchEntry(
    string Id,
    string Title,
    string Subtitle,
    SearchEntryKind Kind,
    IReadOnlyList<string> Keywords);

public sealed class SearchIndex
{
    private readonly IReadOnlyList<IndexedEntry> _entries;

    public SearchIndex(IEnumerable<SearchEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Id) && !string.IsNullOrWhiteSpace(entry.Title))
            .Select(entry => new IndexedEntry(entry))
            .ToList();
    }

    public IReadOnlyList<SearchEntry> Query(string? query, int limit = 20)
    {
        var normalized = Normalize(query);
        if (normalized.Length == 0 || limit <= 0)
        {
            return [];
        }

        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return _entries
            .Select(entry => (Entry: entry, Score: entry.Score(normalized, tokens)))
            .Where(result => result.Score is not null)
            .OrderBy(result => result.Score)
            .ThenBy(result => result.Entry.Source.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(result => result.Entry.Source.Id, StringComparer.Ordinal)
            .Take(limit)
            .Select(result => result.Entry.Source)
            .ToList();
    }

    internal static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var previousSpace = true;
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var expanded = character switch
            {
                'ł' => "l",
                'ß' => "ss",
                'æ' => "ae",
                'ø' => "o",
                _ => null,
            };
            if (expanded is not null)
            {
                builder.Append(expanded);
                previousSpace = false;
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousSpace = false;
            }
            else if (!previousSpace)
            {
                builder.Append(' ');
                previousSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    private sealed class IndexedEntry
    {
        private readonly string _title;
        private readonly string[] _titleWords;
        private readonly string _subtitle;
        private readonly string[] _keywords;
        private readonly string _all;

        public IndexedEntry(SearchEntry source)
        {
            Source = source;
            _title = Normalize(source.Title);
            _titleWords = _title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            _subtitle = Normalize(source.Subtitle);
            _keywords = source.Keywords.Select(Normalize).Where(value => value.Length > 0).ToArray();
            _all = string.Join(' ', new[] { _title, _subtitle }.Concat(_keywords));
        }

        public SearchEntry Source { get; }

        public int? Score(string query, IReadOnlyList<string> tokens)
        {
            if (tokens.Any(token => !_all.Contains(token, StringComparison.Ordinal)))
            {
                return null;
            }

            if (_title == query)
            {
                return 0;
            }

            if (_title.StartsWith(query, StringComparison.Ordinal))
            {
                return 10;
            }

            var score = 0;
            foreach (var token in tokens)
            {
                if (_titleWords.Any(word => word == token))
                {
                    score += 20;
                }
                else if (_titleWords.Any(word => word.StartsWith(token, StringComparison.Ordinal)))
                {
                    score += 25;
                }
                else if (_title.Contains(token, StringComparison.Ordinal))
                {
                    score += 30;
                }
                else if (_keywords.Any(keyword => keyword.Contains(token, StringComparison.Ordinal)))
                {
                    score += 40;
                }
                else
                {
                    score += 50;
                }
            }

            return score;
        }
    }
}
