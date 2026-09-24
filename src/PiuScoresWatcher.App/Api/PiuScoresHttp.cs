using System.Net.Http;
using System.Net.Http.Headers;
using PiuScoresWatcher.Core.Api;
using PiuScoresWatcher.Core.Startup;

namespace PiuScoresWatcher.App.Api;

/// <summary>The one <see cref="HttpClient" /> the watcher talks to PIU Scores with: the site's address, a name in the User-Agent, a sane timeout.</summary>
public static class PiuScoresHttp
{
    public static HttpClient Create(LaunchOptions options)
    {
        var http = new HttpClient
        {
            BaseAddress = options.EffectiveBaseUrl,
            Timeout = TimeSpan.FromSeconds(30)
        };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PiuScoresWatcher", AppVersion.Informational));
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(+https://github.com/DrMurloc/PiuScoresWatcher)"));
        return http;
    }

    public static IPlaysClient Client(LaunchOptions options, ITokenStore tokens)
    {
        return new PiuScoresClient(Create(options), tokens);
    }
}
