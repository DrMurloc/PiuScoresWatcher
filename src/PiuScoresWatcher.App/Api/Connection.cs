using PiuScoresWatcher.App.Status;
using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Capture;
using PiuScoresWatcher.Core.Startup;

namespace PiuScoresWatcher.App.Api;

/// <summary>
///     Connecting a token, checking the stored one, and letting it go. A pasted token is checked against
///     PIU Scores before it is kept: only one the site accepts is stored.
/// </summary>
public sealed class Connection(LaunchOptions options, ITokenStore tokens, IPlaysClient site, WatcherStatus status, INotifier notifier)
{
    /// <summary>
    ///     At start-up, and when settings open without a name: who the stored token belongs to. A token
    ///     the site refuses says so the way a refused post does; an unreachable site leaves it stored.
    /// </summary>
    public async Task CheckStoredAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tokens.Load()))
        {
            status.Disconnected();
            return;
        }

        status.TokenStored();
        switch (await site.WhoAmIAsync(cancellationToken))
        {
            case IdentityCheck.Connected connected:
                status.Connected(connected.Player);
                break;
            case IdentityCheck.Unauthorized:
                notifier.Notify(new WatcherNotice.TokenRejected());
                break;
        }
    }

    public async Task<IdentityCheck> ConnectAsync(string token, CancellationToken cancellationToken)
    {
        var candidate = token.Trim();
        var check = await PiuScoresHttp.Client(options, new FixedToken(candidate)).WhoAmIAsync(cancellationToken);
        if (check is IdentityCheck.Connected connected)
        {
            tokens.Save(candidate);
            status.Connected(connected.Player);
        }

        return check;
    }

    public void Disconnect()
    {
        tokens.Clear();
        status.Disconnected();
    }

    private sealed class FixedToken(string token) : ITokenStore
    {
        public string? Load() => token;

        public void Save(string value)
        {
        }

        public void Clear()
        {
        }
    }
}
