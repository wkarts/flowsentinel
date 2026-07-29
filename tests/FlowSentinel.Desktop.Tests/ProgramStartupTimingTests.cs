namespace FlowSentinel.Desktop.Tests;

public sealed class ProgramStartupTimingTests
{
    [Fact]
    public void PrimeiraAtualizacaoDoSplashNaoDeveSubtrairTimeSpanMinValue()
    {
        var refresh = Program.ShouldRefreshSplashProgress(TimeSpan.Zero, null);

        Assert.True(refresh);
    }

    [Fact]
    public void AtualizacaoDoSplashDeveRespeitarIntervaloDeDuzentosECinquentaMilissegundos()
    {
        Assert.False(Program.ShouldRefreshSplashProgress(
            TimeSpan.FromMilliseconds(249),
            TimeSpan.Zero));
        Assert.True(Program.ShouldRefreshSplashProgress(
            TimeSpan.FromMilliseconds(250),
            TimeSpan.Zero));
    }

    [Fact]
    public void AtualizacaoInicialDeveAceitarDuracaoMaximaSemOverflow()
    {
        var refresh = Program.ShouldRefreshSplashProgress(TimeSpan.MaxValue, null);

        Assert.True(refresh);
    }
}
