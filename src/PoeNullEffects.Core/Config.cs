using PoeNullEffects.Core.Models;

namespace PoeNullEffects.Core;

public class Config
{
    private readonly string _filePath;

    public string GgpkPath { get; set; } = "";
    public NullMode NullParticlesMethod { get; set; } = NullMode.AllExcept;
    public int KeepEmitters { get; set; } = 0;
    public int MakeGoodValue { get; set; } = 2;

    private Config(string filePath)
    {
        _filePath = filePath;
    }

    public static Config Load(string filePath)
    {
        var config = new Config(filePath);

        if (!File.Exists(filePath))
            return config;

        foreach (var line in File.ReadAllLines(filePath))
        {
            var eqIndex = line.IndexOf('=');
            if (eqIndex < 0) continue;

            var key = line[..eqIndex].Trim();
            var value = line[(eqIndex + 1)..].Trim();

            switch (key)
            {
                case "ggpkPath":
                    config.GgpkPath = value;
                    break;
                case "nullParticlesMethod":
                    if (int.TryParse(value, out var method) && Enum.IsDefined(typeof(NullMode), method))
                        config.NullParticlesMethod = (NullMode)method;
                    break;
                case "keepEmitters":
                    if (int.TryParse(value, out var keep))
                        config.KeepEmitters = keep;
                    break;
                case "makeGoodValue":
                    if (int.TryParse(value, out var mgv))
                        config.MakeGoodValue = mgv;
                    break;
            }
        }

        return config;
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var lines = new[]
        {
            $"ggpkPath={GgpkPath}",
            $"nullParticlesMethod={(int)NullParticlesMethod}",
            $"keepEmitters={KeepEmitters}",
            $"makeGoodValue={MakeGoodValue}"
        };

        File.WriteAllLines(_filePath, lines);
    }
}
