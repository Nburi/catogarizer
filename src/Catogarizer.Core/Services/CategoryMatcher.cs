namespace Catogarizer.Core.Services;

/// <summary>
/// How well a category name matches what's typed in the palette. Higher is better, 0 means no
/// match. Keeps typing short: "dw" finds "Deep Work", "wo" prefers "Work" over "Deep Work".
/// </summary>
public static class CategoryMatcher
{
    public static int Score(string name, string query)
    {
        var q = query.Trim();
        if (q.Length == 0) return 1;

        if (name.StartsWith(q, StringComparison.OrdinalIgnoreCase)) return 4;

        var words = name.Split([' ', '-', '_', '/', '&'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Any(w => w.StartsWith(q, StringComparison.OrdinalIgnoreCase))) return 3;

        var initials = string.Concat(words.Select(w => w[0]));
        if (initials.StartsWith(q, StringComparison.OrdinalIgnoreCase)) return 2;

        return name.Contains(q, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }
}
