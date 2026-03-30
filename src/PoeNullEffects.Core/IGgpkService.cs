using PoeNullEffects.Core.Models;

namespace PoeNullEffects.Core;

public interface IGgpkService : IDisposable
{
    bool IsOpen { get; }
    string FilePath { get; }

    void Open(string ggpkPath);
    void Close();

    List<GgpkFileEntry> GetAllFiles();
    List<GgpkFileEntry> GetParticleFiles();
    List<GgpkFileEntry> SearchFiles(string query, bool useRegex = false);

    byte[] ReadFile(string path);
    void WriteFile(string path, byte[] data);
    void SaveIndex();

    int CountEmitters(string particlePath);
}
