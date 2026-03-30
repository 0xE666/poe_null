using NSubstitute;
using PoeNullEffects.Core;
using PoeNullEffects.Core.Models;

namespace PoeNullEffects.Tests;

public class ParticleNullerTests
{
    private static readonly byte[] NullPayload = [0xFF, 0xFE, 0x30, 0x00, 0x0D, 0x00, 0x0A, 0x00];

    private readonly IGgpkService _ggpk;
    private readonly ParticleNuller _nuller;

    public ParticleNullerTests()
    {
        _ggpk = Substitute.For<IGgpkService>();
        _nuller = new ParticleNuller(_ggpk);
    }

    [Fact]
    public void NullAll_WritesNullPayloadToEveryParticle()
    {
        var particles = new List<GgpkFileEntry>
        {
            new() { Path = "Metadata/Particles/fire.pet", Size = 100 },
            new() { Path = "Metadata/Particles/ice.pet", Size = 200 }
        };
        _ggpk.GetParticleFiles().Returns(particles);

        var result = _nuller.Execute(NullMode.All, filterList: null, keepEmitters: false);

        _ggpk.Received(1).WriteFile("Metadata/Particles/fire.pet",
            Arg.Is<byte[]>(b => b.SequenceEqual(NullPayload)));
        _ggpk.Received(1).WriteFile("Metadata/Particles/ice.pet",
            Arg.Is<byte[]>(b => b.SequenceEqual(NullPayload)));
        Assert.Equal(2, result);
    }

    [Fact]
    public void NullAllExcept_SkipsMatchingPaths()
    {
        var particles = new List<GgpkFileEntry>
        {
            new() { Path = "Metadata/Particles/fire.pet", Size = 100 },
            new() { Path = "Metadata/Particles/lightning_orb/glow.pet", Size = 200 }
        };
        _ggpk.GetParticleFiles().Returns(particles);

        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "lightning_orb\n");
        var filterList = FilterList.Load(tempFile);
        File.Delete(tempFile);

        var result = _nuller.Execute(NullMode.AllExcept, filterList, keepEmitters: false);

        _ggpk.Received(1).WriteFile("Metadata/Particles/fire.pet", Arg.Any<byte[]>());
        _ggpk.DidNotReceive().WriteFile("Metadata/Particles/lightning_orb/glow.pet", Arg.Any<byte[]>());
        Assert.Equal(1, result);
    }

    [Fact]
    public void NullOnly_OnlyNullsMatchingPaths()
    {
        var particles = new List<GgpkFileEntry>
        {
            new() { Path = "Metadata/Particles/blood_splatter.pet", Size = 100 },
            new() { Path = "Metadata/Particles/lightning.pet", Size = 200 }
        };
        _ggpk.GetParticleFiles().Returns(particles);

        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "blood\n");
        var filterList = FilterList.Load(tempFile);
        File.Delete(tempFile);

        var result = _nuller.Execute(NullMode.Only, filterList, keepEmitters: false);

        _ggpk.Received(1).WriteFile("Metadata/Particles/blood_splatter.pet", Arg.Any<byte[]>());
        _ggpk.DidNotReceive().WriteFile("Metadata/Particles/lightning.pet", Arg.Any<byte[]>());
        Assert.Equal(1, result);
    }
}
