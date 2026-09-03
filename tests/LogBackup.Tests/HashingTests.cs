using LogBackup.Core.Hashing;

namespace LogBackup.Tests;

public class HashingTests
{
    [Fact]
    public async Task ComputesKnownSha256Value()
    {
        var engine = new HashEngine("SHA-256");
        using var stream = new MemoryStream("hello world"u8.ToArray());

        var hash = await engine.ComputeHashAsync(stream);

        Assert.Equal("b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9", hash);
    }

    [Fact]
    public async Task DifferentContentProducesDifferentHash()
    {
        var engine = new HashEngine("SHA-256");
        var h1 = await engine.ComputeHashAsync(new MemoryStream("a"u8.ToArray()));
        var h2 = await engine.ComputeHashAsync(new MemoryStream("b"u8.ToArray()));

        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void RejectsMd5AndSha1()
    {
        Assert.Throws<NotSupportedException>(() => new HashEngine("MD5"));
        Assert.Throws<NotSupportedException>(() => new HashEngine("SHA-1"));
    }

    [Fact]
    public void SupportsSha512()
    {
        var engine = new HashEngine("SHA-512");
        Assert.Equal("SHA-512", engine.Algorithm);
    }
}
