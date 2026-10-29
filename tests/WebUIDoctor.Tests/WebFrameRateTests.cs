using WebUIDoctor.Core;
using Xunit;

namespace WebUIDoctor.Tests;

public class WebFrameRateTests
{
    [Fact]
    public void Out_of_the_box_the_menus_run_at_twice_the_games_rate()
    {
        // The lag players feel in every menu comes from the game's 30; the default must lift it.
        Assert.Equal(60u, WebFrameRate.Pick(WebFrameRate.Default));
        Assert.True(WebFrameRate.Default > WebFrameRate.GameRate);
    }

    [Theory]
    [InlineData(30, 30u)]
    [InlineData(45, 45u)]
    [InlineData(120, 120u)]
    public void A_rate_in_the_range_is_used_as_set(int configured, uint expected) =>
        Assert.Equal(expected, WebFrameRate.Pick(configured));

    [Theory]
    [InlineData(0, 30u)]
    [InlineData(-5, 30u)]
    [InlineData(10, 30u)]
    [InlineData(500, 120u)]
    public void A_hand_edited_rate_outside_the_range_is_pulled_back(int configured, uint expected) =>
        Assert.Equal(expected, WebFrameRate.Pick(configured));
}
