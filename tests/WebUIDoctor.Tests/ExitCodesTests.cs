using WebUIDoctor.Core;
using Xunit;

namespace WebUIDoctor.Tests;

public class ExitCodesTests
{
    [Theory]
    [InlineData(0xC0000005u, "access violation")]
    [InlineData(0xE0000008u, "out-of-memory")]
    [InlineData(0x887A0005u, "GPU device lost")]
    [InlineData(ExitCodes.KilledByDoctor, "killed by WebUIDoctor")]
    public void Explains_known_crash_codes_in_words(uint code, string meaning)
    {
        Assert.Contains(meaning, ExitCodes.Describe(code));
    }

    [Fact]
    public void Always_shows_the_raw_hex_so_unknown_codes_can_be_looked_up()
    {
        var text = ExitCodes.Describe(0x12345678);

        Assert.Contains("0x12345678", text);
        Assert.Contains("unrecognised", text);
    }
}
