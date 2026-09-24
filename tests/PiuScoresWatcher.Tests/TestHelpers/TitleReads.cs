namespace PiuScoresWatcher.Tests.TestHelpers;

/// <summary>What a stubbed title reader's attempts read, in order — none at all for a title nothing could read.</summary>
internal static class TitleReads
{
    public static async IAsyncEnumerable<string> Of(params string[] reads)
    {
        foreach (var read in reads)
        {
            await Task.Yield();
            yield return read;
        }
    }
}
