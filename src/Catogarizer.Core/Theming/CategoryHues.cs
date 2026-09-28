namespace Catogarizer.Core.Theming;

public static class CategoryHues
{
    /// <summary>The hues offered for categories - spread around the wheel, each readable in every theme.</summary>
    public static IReadOnlyList<double> Palette { get; } = [260, 150, 330, 70, 200, 25, 290, 110, 175, 50];

    /// <summary>The palette hue farthest from every hue already in use (first palette entry on a tie).</summary>
    public static double Next(IEnumerable<double> used)
    {
        var usedList = used.ToList();
        if (usedList.Count == 0) return Palette[0];
        return Palette
            .Select((hue, index) => (hue, index, distance: usedList.Min(u => AngularDistance(hue, u))))
            .OrderByDescending(x => x.distance)
            .ThenBy(x => x.index)
            .First().hue;
    }

    public static double AngularDistance(double a, double b)
    {
        var d = Math.Abs(((a - b) % 360 + 360) % 360);
        return Math.Min(d, 360 - d);
    }
}
