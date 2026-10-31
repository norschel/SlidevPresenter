using SlideDevPresenter.Infrastructure.Services;

namespace SlideDevPresenter.Tests.Services;

public sealed class SlidevOutputParserTests
{
    [Theory]
    // Slidev 52+ ready line
    [InlineData("  public slide show   > http://localhost:64848/", "http://localhost:64848/")]
    // Slidev 52+ ready line with picocolors ANSI codes (dim label, cyan URL, bold port)
    [InlineData("\u001b[2m  public slide show \u001b[22m  > \u001b[36mhttp://localhost:\u001b[1m64848\u001b[22m/\u001b[39m", "http://localhost:64848/")]
    // Slidev with a base path
    [InlineData("  public slide show   > http://localhost:3030/talk/", "http://localhost:3030/talk/")]
    // Legacy Vite-style ready line
    [InlineData("  ➜  Local:   http://localhost:3030/", "http://localhost:3030/")]
    // Legacy Vite-style ready line with ANSI codes (green arrow, bold label, cyan URL, bold port)
    [InlineData("  \u001b[32m➜\u001b[39m  \u001b[1mLocal\u001b[22m:   \u001b[36mhttp://localhost:\u001b[1m3030\u001b[22m/\u001b[39m", "http://localhost:3030/")]
    // Older Vite output without trailing slash
    [InlineData("  > Local: http://localhost:3030", "http://localhost:3030/")]
    [InlineData("  public slide show   > http://127.0.0.1:3030/", "http://127.0.0.1:3030/")]
    [InlineData("  ➜  Local:   http://127.0.0.1:3030/", "http://127.0.0.1:3030/")]
    public void TryParseReadyUrl_ReadyLine_ReturnsLocalUrl(string line, string expected)
    {
        var parsed = SlidevOutputParser.TryParseReadyUrl(line, out var url);

        Assert.True(parsed);
        Assert.Equal(expected, url!.AbsoluteUri);
    }

    [Theory]
    // Must not mistake the presenter/overview URL for the participant URL.
    [InlineData("  presenter mode      > http://localhost:64848/presenter/")]
    [InlineData("  slides overview     > http://localhost:64848/overview/")]
    [InlineData("  private slide show  > http://localhost:64848/?password=secret")]
    // Must not pick up LAN addresses.
    [InlineData("  remote control      > http://192.168.1.10:64848/entry/")]
    [InlineData("  ➜  Network: http://192.168.1.10:3030/")]
    [InlineData("  ➜  Network: use --host to expose")]
    [InlineData("  public slide show   > http://192.168.1.10:3030/")]
    [InlineData("  shortcuts           > restart | open | edit | quit")]
    [InlineData("  Local:   ")]
    [InlineData("  Slidev  v53.0.0")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParseReadyUrl_IrrelevantLine_ReturnsFalse(string? line)
    {
        var parsed = SlidevOutputParser.TryParseReadyUrl(line, out var url);

        Assert.False(parsed);
        Assert.Null(url);
    }

    [Fact]
    public void TryParseReadyUrl_PortDiffersFromRequested_ReturnsPrintedPort()
    {
        SlidevOutputParser.TryParseReadyUrl("  public slide show   > http://localhost:3031/", out var url);

        Assert.Equal(3031, url!.Port);
    }

    [Fact]
    public void StripAnsi_ColoredText_RemovesEscapeSequences()
    {
        var result = SlidevOutputParser.StripAnsi("\u001b[2m  theme \u001b[22m\u001b[32mdefault\u001b[39m");

        Assert.Equal("  theme default", result);
    }
}
