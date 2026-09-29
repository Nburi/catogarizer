using System.Globalization;
using System.Text;

namespace Catogarizer.Core.Services;

/// <summary>
/// How well a category name matches what's typed in the palette. Higher is better, 0 means no
/// match. Keeps typing short: "dw" finds "Deep Work", "wo" prefers "Work" over "Deep Work", and
/// accents don't matter ("pruf" finds "Prüfung", "grosser" finds "Grösser").
/// </summary>
public static class CategoryMatcher
{
    public static int Score(string name, string query)
    {
        var q = Fold(query.Trim());
        if (q.Length == 0) return 1;
        var n = Fold(name);

        if (n.StartsWith(q, StringComparison.Ordinal)) return 4;

        var words = n.Split([' ', '-', '_', '/', '&'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Any(w => w.StartsWith(q, StringComparison.Ordinal))) return 3;

        var initials = string.Concat(words.Select(w => w[0]));
        if (initials.StartsWith(q, StringComparison.Ordinal)) return 2;

        return n.Contains(q, StringComparison.Ordinal) ? 1 : 0;
    }

    /// <summary>Lowercase, accents removed, "ß" as "ss" - so typing on any keyboard layout finds the name.</summary>
    private static string Fold(string value)
    {
        var decomposed = value.ToLowerInvariant().Replace("ß", "ss").Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) builder.Append(c);
        return builder.ToString();
    }
}
