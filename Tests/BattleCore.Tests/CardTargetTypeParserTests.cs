using CardMoba.Protocol.Enums;
using FluentAssertions;
using Xunit;

namespace CardMoba.Tests
{
    public class CardTargetTypeParserTests
    {
        [Theory]
        [InlineData("Enemy", CardTargetType.CurrentEnemy)]
        [InlineData("Opponent", CardTargetType.CurrentEnemy)]
        [InlineData("AllEnemies", CardTargetType.AllEnemies)]
        [InlineData("AllOpponents", CardTargetType.AllEnemies)]
        [InlineData("Self", CardTargetType.Self)]
        [InlineData("All", CardTargetType.All)]
        public void TryParse_ShouldSupport_RuntimeAndJsonAliases(string raw, CardTargetType expected)
        {
            CardTargetTypeParser.TryParse(raw, out var actual).Should().BeTrue();
            actual.Should().Be(expected);
        }
    }
}
