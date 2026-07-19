using System.Diagnostics;
using System.Net;
using System.Text;
using GluetunWeb.Api.Models;

namespace GluetunWeb.Api.Services;

/// <summary>
/// Fetches a URL through a SOCKS5 proxy so the UI can answer "does this actually work?" without the
/// user wiring up a client. Reports the outcome plus the raw response, since a proxy that returns
/// *something* unexpected is more informative than a bare pass/fail.
/// </summary>
public class ProxyTester(ILogger<ProxyTester> logger)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);
    /// <summary>Body is truncated — this is a connectivity probe, not a downloader.</summary>
    private const int MaxBodyChars = 4000;

    public async Task<ProxyTestResponse> TestAsync(
        string proxyHost, int proxyPort, string? user, string? password, string url,
        CancellationToken ct = default)
    {
        var target = NormalizeUrl(url);
        if (target is null)
            return ProxyTestResponse.Failed(url, "Enter a valid http:// or https:// URL.");

        var via = $"socks5://{proxyHost}:{proxyPort}";

        // Preflight: a plain TCP connect distinguishes "the proxy is not listening" from "the proxy
        // is fine but the tunnel behind it goes nowhere". Without this both surface as one 20s
        // timeout, and the report has to guess which happened.
        var reachable = await CanConnectAsync(proxyHost, proxyPort, ct);
        if (!reachable)
        {
            return new ProxyTestResponse(
                Ok: false, Url: target.ToString(), Via: via, StatusCode: null, ReasonPhrase: null,
                ElapsedMs: 0,
                Error: "Could not open a TCP connection to the proxy itself — it is not listening. "
                       + "Check the connection is deployed and running.",
                Headers: null, Body: null);
        }

        var proxyUri = new Uri(via);
        var handler = new HttpClientHandler
        {
            Proxy = new WebProxy(proxyUri)
            {
                Credentials = string.IsNullOrEmpty(user) ? null : new NetworkCredential(user, password),
            },
            UseProxy = true,
            AllowAutoRedirect = true,
        };

        using var http = new HttpClient(handler) { Timeout = Timeout };
        // Some sites serve very different content to an unknown agent; identify ourselves plainly.
        http.DefaultRequestHeaders.UserAgent.ParseAdd("GluetunWeb-ConnectivityTest/1.0");

        var sw = Stopwatch.StartNew();
        try
        {
            using var resp = await http.GetAsync(target, ct);
            sw.Stop();

            var body = await ReadTruncatedAsync(resp, ct);
            var headers = FormatHeaders(resp);

            return new ProxyTestResponse(
                Ok: resp.IsSuccessStatusCode,
                Url: target.ToString(),
                Via: $"socks5://{proxyHost}:{proxyPort}",
                StatusCode: (int)resp.StatusCode,
                ReasonPhrase: resp.ReasonPhrase,
                ElapsedMs: (int)sw.ElapsedMilliseconds,
                Error: resp.IsSuccessStatusCode
                    ? null
                    : $"Proxy reachable, but the site answered {(int)resp.StatusCode} {resp.ReasonPhrase}.",
                Headers: headers,
                Body: body);
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogDebug(ex, "Proxy test through {Proxy} failed", proxyUri);
            return new ProxyTestResponse(
                Ok: false,
                Url: target.ToString(),
                Via: $"socks5://{proxyHost}:{proxyPort}",
                StatusCode: null,
                ReasonPhrase: null,
                ElapsedMs: (int)sw.ElapsedMilliseconds,
                Error: Describe(ex),
                Headers: null,
                Body: null);
        }
    }

    /// <summary>TCP-connects to the proxy port with a short timeout. True = something is listening.</summary>
    private static async Task<bool> CanConnectAsync(string host, int port, CancellationToken ct)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(4));
            await client.ConnectAsync(host, port, cts.Token);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Accepts "google.com" as well as a full URL, defaulting to https.</summary>
    private static Uri? NormalizeUrl(string url)
    {
        var trimmed = (url ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return null;
        if (!trimmed.Contains("://", StringComparison.Ordinal))
            trimmed = "https://" + trimmed;

        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri
            : null;
    }

    private static async Task<string> ReadTruncatedAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        var body = await resp.Content.ReadAsStringAsync(ct);
        return body.Length <= MaxBodyChars
            ? body
            : body[..MaxBodyChars] + $"\n\n… truncated ({body.Length:N0} chars total)";
    }

    private static string FormatHeaders(HttpResponseMessage resp)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"HTTP/{resp.Version} {(int)resp.StatusCode} {resp.ReasonPhrase}");
        foreach (var h in resp.Headers)
            sb.AppendLine($"{h.Key}: {string.Join(", ", h.Value)}");
        foreach (var h in resp.Content.Headers)
            sb.AppendLine($"{h.Key}: {string.Join(", ", h.Value)}");
        return sb.ToString();
    }

    /// <summary>
    /// Turns transport exceptions into something actionable — "connection refused" through a proxy
    /// usually means the proxy is down, not the site.
    /// </summary>
    private static string Describe(Exception ex) => ex switch
    {
        // The proxy port answered (preflight passed), so the stall is past it: the request went into
        // the tunnel and nothing came back. Naming the likely causes beats asserting one.
        TaskCanceledException => $"The proxy is listening, but no response came back within "
                                 + $"{Timeout.TotalSeconds:N0}s. Usually the VPN tunnel is not actually up — "
                                 + "check the connection shows an exit IP — or the site is unreachable from that exit.",
        HttpRequestException { InnerException: System.Net.Sockets.SocketException se } =>
            $"Could not reach the proxy ({se.SocketErrorCode}). Is it deployed and running?",
        HttpRequestException h => $"Request failed: {h.Message}",
        _ => ex.Message,
    };
}
