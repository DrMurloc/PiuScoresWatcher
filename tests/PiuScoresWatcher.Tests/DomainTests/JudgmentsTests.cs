using PiuScoresWatcher.Core.Domain;
using PiuScoresWatcher.Core.Exceptions;

namespace PiuScoresWatcher.Tests.DomainTests;

public sealed class JudgmentsTests
{
    [Fact]
    public void TheNotesAreTheFiveCountsTogether()
    {
        Assert.Equal(1000, Judgments.From(911, 59, 13, 3, 14).Notes);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0, 1)]
    [InlineData(0, 0, 0, 0, 0)]
    public void ANegativeCountOrNoNotesIsRefused(int p, int g, int gd, int b, int m)
    {
        Assert.Throws<InvalidJudgmentsException>(() => Judgments.From(p, g, gd, b, m));
    }
}
