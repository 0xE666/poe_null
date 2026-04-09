namespace PoeNullEffects.Core;

public class FilterList
{
    public IReadOnlyList<string> Entries { get; }
    public IReadOnlyList<string> Exclusions { get; }

    private FilterList(IReadOnlyList<string> entries, IReadOnlyList<string> exclusions)
    {
        Entries = entries;
        Exclusions = exclusions;
    }

    public static FilterList Load(string filePath)
    {
        if (!File.Exists(filePath))
            return new FilterList([], []);

        var lines = File.ReadAllLines(filePath)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'));

        var entries = new List<string>();
        var exclusions = new List<string>();

        foreach (var line in lines)
        {
            if (line.StartsWith('!'))
                exclusions.Add(line[1..]);
            else
                entries.Add(line);
        }

        return new FilterList(entries, exclusions);
    }

    /// <summary>
    /// Returns true if path matches any positive entry and no exclusion entry.
    /// Exclusions (! prefixed) override positive matches.
    /// </summary>
    public bool Matches(string path)
    {
        if (Exclusions.Any(ex => path.Contains(ex, StringComparison.OrdinalIgnoreCase)))
            return false;

        return Entries.Any(entry =>
            path.Contains(entry, StringComparison.OrdinalIgnoreCase));
    }
}
