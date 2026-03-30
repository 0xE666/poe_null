namespace PoeNullEffects.Core;

public class FilterList
{
    public IReadOnlyList<string> Entries { get; }

    private FilterList(IReadOnlyList<string> entries)
    {
        Entries = entries;
    }

    public static FilterList Load(string filePath)
    {
        if (!File.Exists(filePath))
            return new FilterList([]);

        var entries = File.ReadAllLines(filePath)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToList();

        return new FilterList(entries);
    }

    public bool Matches(string path)
    {
        return Entries.Any(entry =>
            path.Contains(entry, StringComparison.OrdinalIgnoreCase));
    }
}
