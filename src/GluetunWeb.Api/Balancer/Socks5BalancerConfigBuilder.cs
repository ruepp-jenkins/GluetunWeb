using System.Text.Json;
using System.Text.Json.Nodes;

namespace GluetunWeb.Api.Balancer;

public record BalancerUpstreamInput(string Name, string Host, int Port, string? AuthUser, string? AuthPwd);

public record BalancerConfigInput
{
    public string UpstreamSelectRule { get; init; } = "loop";
    public int RetryTimes { get; init; } = 3;
    public int ConnectTimeout { get; init; } = 2000;
    public string TestRemoteHost { get; init; } = "www.google.com";
    public int TestRemotePort { get; init; } = 443;
    public int TcpCheckPeriod { get; init; } = 30000;
    public int ConnectCheckPeriod { get; init; } = 300000;
    public int AdditionCheckPeriod { get; init; } = 10000;
    public int ThreadNum { get; init; } = 10;
    public int ServerChangeTime { get; init; } = 5000;
    public IReadOnlyList<BalancerUpstreamInput> Upstreams { get; init; } = Array.Empty<BalancerUpstreamInput>();
}

/// <summary>
/// Builds the Socks5BalancerAsio config.json (injected at /app/config.json). Container-internal
/// ports are fixed; the host publishes them on auto-assigned ports.
/// See https://github.com/Socks5Balancer/Socks5BalancerAsio
/// </summary>
public static class Socks5BalancerConfigBuilder
{
    /// <summary>
    /// Config lives on a per-balancer named volume mounted here (not /app, which would shadow the
    /// binary) so it survives container recreation. The binary is given this path as an argument.
    /// </summary>
    public const string ConfigVolumePath = "/conf";
    public const string ConfigPath = ConfigVolumePath + "/config.json";
    public const int ListenPort = 15000;      // balanced SOCKS5
    public const int MultiListenPort = 15001; // secondary listen (keeps multiListen a non-empty array)
    public const int StateServerPort = 15010; // JSON state/control API
    public const int WebPort = 80;            // embedded web UI (stateBootstrap.html)

    public static string Build(BalancerConfigInput i)
    {
        var upstreams = new JsonArray();
        foreach (var u in i.Upstreams)
        {
            upstreams.Add(new JsonObject
            {
                ["name"] = u.Name,
                ["host"] = u.Host,
                ["port"] = u.Port,
                ["authUser"] = u.AuthUser ?? string.Empty,
                ["authPwd"] = u.AuthPwd ?? string.Empty,
            });
        }

        var config = new JsonObject
        {
            ["listenHost"] = "0.0.0.0",
            ["listenPort"] = ListenPort,
            ["retryTimes"] = i.RetryTimes,
            ["connectTimeout"] = i.ConnectTimeout,
            ["testRemoteHost"] = i.TestRemoteHost,
            ["testRemotePort"] = i.TestRemotePort,
            ["tcpCheckPeriod"] = i.TcpCheckPeriod,
            ["tcpCheckStart"] = 1000,
            ["connectCheckPeriod"] = i.ConnectCheckPeriod,
            ["connectCheckStart"] = 1000,
            ["additionCheckPeriod"] = i.AdditionCheckPeriod,
            ["upstreamSelectRule"] = i.UpstreamSelectRule,
            ["sleepTime"] = 1800000,
            ["threadNum"] = i.ThreadNum,
            ["serverChangeTime"] = i.ServerChangeTime,
            ["stateServerHost"] = "0.0.0.0",
            ["stateServerPort"] = StateServerPort,
            ["disableConnectTest"] = false,
            ["disableConnectionTracker"] = false,
            ["traditionTcpRelay"] = false,
            ["disableSocks4"] = true,
            ["upstream"] = upstreams,
            ["EmbedWebServerConfig"] = new JsonObject
            {
                ["enable"] = true,
                ["host"] = "0.0.0.0",
                ["port"] = WebPort,
                ["root_path"] = "./html/",
                ["index_file_of_root"] = "stateBootstrap.html",
                ["backendHost"] = string.Empty,
                ["backendPort"] = 0,
                ["allowFileExtList"] = "htm html js json jpg jpeg png bmp gif ico svg css",
            },
            // Must be a NON-EMPTY array: the state server serializes an empty array as "" (a boost
            // property_tree quirk), which breaks the web UI's config.multiListen.map(...).
            ["multiListen"] = new JsonArray(
                new JsonObject { ["host"] = "0.0.0.0", ["port"] = MultiListenPort }),
        };

        return config.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
