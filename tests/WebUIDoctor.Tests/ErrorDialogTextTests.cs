using WebUIDoctor.Core;
using Xunit;

namespace WebUIDoctor.Tests;

public class ErrorDialogTextTests
{
    [Theory]
    [InlineData("Interface Display Error")]
    [InlineData("INTERFACE DISPLAY ERROR: automatic recovery has failed; progress auto-saved")]
    [InlineData("Automatic recovery has failed. Please restart the game.")]
    [InlineData("界面显示异常")]
    public void Recognises_the_recovery_failure_dialog(string text) => Assert.True(ErrorDialogText.IsRecoveryFailureDialog(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Recovery 12")]
    [InlineData("Display settings")]
    [InlineData("Interface")]
    public void Leaves_other_ui_text_alone(string? text) => Assert.False(ErrorDialogText.IsRecoveryFailureDialog(text));
}
