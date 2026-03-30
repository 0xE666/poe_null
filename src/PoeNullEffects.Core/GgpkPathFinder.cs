using Microsoft.Win32;

namespace PoeNullEffects.Core;

public static class GgpkPathFinder
{
    private static readonly string[] Poe1GgpkNames = ["Content.ggpk"];
    private static readonly string[] Poe2GgpkNames = ["Content.ggpk", "Content.ggpk2"];

    /// <summary>
    /// Attempts to find the PoE 1 install.
    /// Prefers Steam (loose Bundles2/) over standalone (Content.ggpk).
    /// Returns either the game directory (Steam/Epic) or Content.ggpk path (standalone).
    /// </summary>
    public static string? FindPoe1()
    {
        // First try Steam/Epic (loose bundles — preferred, actually works)
        var looseDir = FindLooseBundles(GetPoe1SearchPaths());
        if (looseDir != null) return looseDir;

        // Fall back to standalone GGPK
        return FindGgpk(GetPoe1SearchPaths(), Poe1GgpkNames);
    }

    /// <summary>
    /// Attempts to find the PoE 2 install.
    /// </summary>
    public static string? FindPoe2()
    {
        var looseDir = FindLooseBundles(GetPoe2SearchPaths());
        if (looseDir != null) return looseDir;

        return FindGgpk(GetPoe2SearchPaths(), Poe2GgpkNames);
    }

    private static string? FindLooseBundles(IEnumerable<string> searchPaths)
    {
        foreach (var dir in searchPaths)
        {
            if (!Directory.Exists(dir)) continue;

            var indexPath = Path.Combine(dir, "Bundles2", "_.index.bin");
            if (File.Exists(indexPath))
            {
                // Verify it's not all zeros (corrupted standalone)
                using var fs = File.OpenRead(indexPath);
                var header = new byte[16];
                fs.Read(header, 0, 16);
                if (header.Any(b => b != 0))
                    return dir; // Game directory with valid loose bundles
            }
        }
        return null;
    }

    private static string? FindGgpk(IEnumerable<string> searchPaths, string[] ggpkNames)
    {
        foreach (var dir in searchPaths)
        {
            if (!Directory.Exists(dir)) continue;

            foreach (var name in ggpkNames)
            {
                var path = Path.Combine(dir, name);
                if (File.Exists(path))
                    return path;
            }
        }

        return null;
    }

    private static List<string> GetPoe1SearchPaths()
    {
        var paths = new List<string>();

        // Steam
        foreach (var steamLib in GetSteamLibraryFolders())
            paths.Add(Path.Combine(steamLib, "steamapps", "common", "Path of Exile"));

        // Standalone (GGG installer)
        paths.Add(@"C:\Program Files (x86)\Grinding Gear Games\Path of Exile");
        paths.Add(@"C:\Program Files\Grinding Gear Games\Path of Exile");

        // Registry — standalone installer writes here
        var regPath = GetRegistryInstallPath(@"SOFTWARE\GrindingGearGames\Path of Exile");
        if (regPath != null) paths.Add(regPath);

        // Epic
        foreach (var epicPath in GetEpicInstallPaths("PathOfExile"))
            paths.Add(epicPath);

        return paths;
    }

    private static List<string> GetPoe2SearchPaths()
    {
        var paths = new List<string>();

        // Steam
        foreach (var steamLib in GetSteamLibraryFolders())
        {
            paths.Add(Path.Combine(steamLib, "steamapps", "common", "Path of Exile 2"));
            paths.Add(Path.Combine(steamLib, "steamapps", "common", "Path of Exile 2 Early Access"));
        }

        // Standalone
        paths.Add(@"C:\Program Files (x86)\Grinding Gear Games\Path of Exile 2");
        paths.Add(@"C:\Program Files\Grinding Gear Games\Path of Exile 2");

        // Registry
        var regPath = GetRegistryInstallPath(@"SOFTWARE\GrindingGearGames\Path of Exile 2");
        if (regPath != null) paths.Add(regPath);

        // Epic
        foreach (var epicPath in GetEpicInstallPaths("PathOfExile2"))
            paths.Add(epicPath);

        return paths;
    }

    private static List<string> GetSteamLibraryFolders()
    {
        var libraries = new List<string>();

        // Default Steam location
        var defaultSteam = @"C:\Program Files (x86)\Steam";

        // Try registry for actual Steam install path
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
                         ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");
            var steamPath = key?.GetValue("InstallPath") as string;
            if (!string.IsNullOrEmpty(steamPath))
                defaultSteam = steamPath;
        }
        catch { }

        libraries.Add(defaultSteam);

        // Parse libraryfolders.vdf for additional Steam library locations
        var vdfPath = Path.Combine(defaultSteam, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdfPath))
        {
            try
            {
                foreach (var line in File.ReadAllLines(vdfPath))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("\"path\""))
                    {
                        // Format: "path"		"D:\\SteamLibrary"
                        var parts = trimmed.Split('"');
                        if (parts.Length >= 4)
                        {
                            var libPath = parts[3].Replace(@"\\", @"\");
                            if (Directory.Exists(libPath))
                                libraries.Add(libPath);
                        }
                    }
                }
            }
            catch { }
        }

        // Common alternate drives
        for (var drive = 'D'; drive <= 'Z'; drive++)
        {
            libraries.Add($@"{drive}:\SteamLibrary");
            libraries.Add($@"{drive}:\Steam");
            libraries.Add($@"{drive}:\Program Files (x86)\Steam");
        }

        return libraries;
    }

    private static string? GetRegistryInstallPath(string subKey)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(subKey)
                         ?? Registry.CurrentUser.OpenSubKey(subKey);
            return key?.GetValue("InstallLocation") as string;
        }
        catch
        {
            return null;
        }
    }

    private static List<string> GetEpicInstallPaths(string gameName)
    {
        var paths = new List<string>();

        // Epic manifests location
        var epicManifests = @"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests";
        if (!Directory.Exists(epicManifests))
            return paths;

        try
        {
            foreach (var manifest in Directory.GetFiles(epicManifests, "*.item"))
            {
                var content = File.ReadAllText(manifest);
                if (content.Contains(gameName, StringComparison.OrdinalIgnoreCase))
                {
                    // Quick parse: find "InstallLocation": "path"
                    var idx = content.IndexOf("\"InstallLocation\"", StringComparison.Ordinal);
                    if (idx >= 0)
                    {
                        var colonIdx = content.IndexOf(':', idx + 17);
                        if (colonIdx >= 0)
                        {
                            var firstQuote = content.IndexOf('"', colonIdx);
                            var secondQuote = content.IndexOf('"', firstQuote + 1);
                            if (firstQuote >= 0 && secondQuote > firstQuote)
                            {
                                var installPath = content[(firstQuote + 1)..secondQuote]
                                    .Replace(@"\\", @"\").Replace("/", @"\");
                                paths.Add(installPath);
                            }
                        }
                    }
                }
            }
        }
        catch { }

        return paths;
    }
}
