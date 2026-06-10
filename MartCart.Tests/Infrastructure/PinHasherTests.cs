using MartCart.Infrastructure.Security;

namespace MartCart.Tests.Infrastructure;

public class PinHasherTests
{
    [Fact]
    public void Hash_Then_Verify_Succeeds()
    {
        var (salt, hash) = PinHasher.Hash("123456");
        Assert.True(PinHasher.Verify("123456", salt, hash));
    }

    [Fact]
    public void Wrong_Pin_Fails()
    {
        var (salt, hash) = PinHasher.Hash("123456");
        Assert.False(PinHasher.Verify("000000", salt, hash));
    }

    [Fact]
    public void Each_Hash_Uses_Fresh_Salt()
    {
        var (s1, h1) = PinHasher.Hash("1234");
        var (s2, h2) = PinHasher.Hash("1234");
        Assert.NotEqual(s1, s2);
        Assert.NotEqual(h1, h2);
    }
}
