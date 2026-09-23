using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using PiuScoresWatcher.App.Storage;
using PiuScoresWatcher.Core.Api;

namespace PiuScoresWatcher.App.Security;

/// <summary>
///     The personal token at rest: <c>token.bin</c> in the data folder, encrypted by Windows for the
///     account it was saved under (DPAPI, current-user scope). No key to manage, nothing in clear,
///     and another Windows account on the same PC cannot read it.
/// </summary>
public sealed class DpapiTokenStore(ILogger<DpapiTokenStore> log) : ITokenStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PiuScoresWatcher token v1");

    public string? Load()
    {
        if (!File.Exists(AppPaths.TokenFile))
            return null;
        try
        {
            var plain = ProtectedData.Unprotect(File.ReadAllBytes(AppPaths.TokenFile), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception failure) when (failure is CryptographicException or IOException)
        {
            log.LogWarning(failure, "The stored token could not be read; treating it as absent");
            return null;
        }
    }

    public void Save(string token)
    {
        Directory.CreateDirectory(AppPaths.Root);
        var sealedToken = ProtectedData.Protect(Encoding.UTF8.GetBytes(token.Trim()), Entropy, DataProtectionScope.CurrentUser);
        var staging = AppPaths.TokenFile + ".tmp";
        File.WriteAllBytes(staging, sealedToken);
        File.Move(staging, AppPaths.TokenFile, overwrite: true);
    }

    public void Clear()
    {
        if (File.Exists(AppPaths.TokenFile))
            File.Delete(AppPaths.TokenFile);
    }
}

/// <summary>
///     A dev seam over the store: <c>PIUSCORESWATCHER_TOKEN</c> in the environment stands in for a
///     stored token, so a replay can post to a local site before the settings window exists. Never
///     persisted; saving and clearing go to the real store.
/// </summary>
public sealed class EnvironmentOrStoredToken(ITokenStore store) : ITokenStore
{
    public const string Variable = "PIUSCORESWATCHER_TOKEN";

    public string? Load()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(Variable);
        return string.IsNullOrWhiteSpace(fromEnvironment) ? store.Load() : fromEnvironment.Trim();
    }

    public void Save(string token) => store.Save(token);

    public void Clear() => store.Clear();
}
