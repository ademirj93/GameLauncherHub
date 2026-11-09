using System.Globalization;
using GameLauncherHub.Domain.Enums;
using GameLauncherHub.UI.Converters;
using Xunit;

namespace GameLauncherHub.UnitTests.Converters;

public class StateConvertersTests
{
    [Theory]
    [InlineData(LauncherState.INSTALLED, "▶ Executar")]
    [InlineData(LauncherState.RUNNING, "● Em Execução")]
    [InlineData(LauncherState.DOWNLOADING, "⏳ Baixando...")]
    [InlineData(LauncherState.INSTALLING, "⚙ Instalando...")]
    [InlineData(LauncherState.NOT_INSTALLED, "↓ Instalar")]
    [InlineData(LauncherState.UNSUPPORTED, "ℹ Incompatível")]
    [InlineData(LauncherState.ERROR, "⚠ Tentar Novamente")]
    public void StateToActionTextConverter_ShouldReturnAppropriateLabel(LauncherState state, string expectedText)
    {
        var converter = StateToActionTextConverter.Instance;
        var result = converter.Convert(state, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal(expectedText, result);
    }

    [Theory]
    [InlineData(LauncherState.INSTALLED, "INSTALADO")]
    [InlineData(LauncherState.RUNNING, "EM EXECUÇÃO")]
    [InlineData(LauncherState.DOWNLOADING, "BAIXANDO")]
    [InlineData(LauncherState.INSTALLING, "INSTALANDO")]
    [InlineData(LauncherState.NOT_INSTALLED, "DISPONÍVEL")]
    [InlineData(LauncherState.UNSUPPORTED, "APENAS LINUX")]
    public void StateToBadgeTextConverter_ShouldReturnAppropriateBadge(LauncherState state, string expectedBadge)
    {
        var converter = StateToBadgeTextConverter.Instance;
        var result = converter.Convert(state, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal(expectedBadge, result);
    }
}

