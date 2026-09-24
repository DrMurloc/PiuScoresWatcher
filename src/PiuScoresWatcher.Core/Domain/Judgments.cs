using PiuScoresWatcher.Core.Exceptions;

namespace PiuScoresWatcher.Core.Domain;

/// <summary>The five counts a result screen shows. Every count is zero or more and at least one note was judged.</summary>
public readonly record struct Judgments
{
    public int Perfects { get; }
    public int Greats { get; }
    public int Goods { get; }
    public int Bads { get; }
    public int Misses { get; }

    private Judgments(int perfects, int greats, int goods, int bads, int misses)
    {
        Perfects = perfects;
        Greats = greats;
        Goods = goods;
        Bads = bads;
        Misses = misses;
    }

    public int Notes => Perfects + Greats + Goods + Bads + Misses;

    public static Judgments From(int perfects, int greats, int goods, int bads, int misses)
    {
        if (perfects < 0 || greats < 0 || goods < 0 || bads < 0 || misses < 0)
            throw new InvalidJudgmentsException("A judgment count cannot be negative.");
        var judgments = new Judgments(perfects, greats, goods, bads, misses);
        if (judgments.Notes == 0)
            throw new InvalidJudgmentsException("A play has at least one judged note.");
        return judgments;
    }

    public override string ToString()
    {
        return $"{Perfects}/{Greats}/{Goods}/{Bads}/{Misses}";
    }
}
