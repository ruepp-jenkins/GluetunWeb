using System.Text;
using GluetunWeb.Api.Data;

namespace GluetunWeb.Api.Gluetun;

/// <summary>
/// Builds the Gluetun control-server <c>config.toml</c> (bind target /gluetun/auth/config.toml).
/// A single role grants the chosen authentication method over the standard control-server routes.
/// See https://github.com/qdm12/gluetun-wiki/blob/main/setup/advanced/control-server.md
/// </summary>
public static class GluetunAuthConfig
{
    /// <summary>Common control-server routes covered by the generated role.</summary>
    public static readonly string[] Routes =
    {
        "GET /v1/version",
        "GET /v1/vpn/status",
        "PUT /v1/vpn/status",
        "GET /v1/vpn/settings",
        "GET /v1/openvpn/status",
        "PUT /v1/openvpn/status",
        "GET /v1/openvpn/settings",
        // Current Gluetun serves the forwarded port at /v1/portforward; the /v1/openvpn/… path is
        // kept for older images. Both are granted so the read works either way.
        "GET /v1/portforward",
        "GET /v1/openvpn/portforwarded",
        "GET /v1/publicip/ip",
        "GET /v1/dns/status",
        "PUT /v1/dns/status",
    };

    /// <summary>
    /// Returns the config.toml contents for the given auth mode.
    ///
    /// A file is written even for <see cref="ControlServerAuth.None"/>: current Gluetun denies every
    /// control-server request unless a role explicitly covers it, so omitting the file (which used to
    /// mean "open") now means "nothing works" — including the dashboard's own status and public-IP
    /// reads. "None" therefore emits a role with <c>auth = "none"</c> limited to the routes below,
    /// rather than leaving the server unusable.
    /// </summary>
    public static string Build(ControlServerAuth auth, string? user, string? password, string? apiKey)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[[roles]]");
        sb.AppendLine("name = \"gluetunweb\"");
        sb.Append("routes = [");
        sb.Append(string.Join(", ", Routes.Select(r => $"\"{r}\"")));
        sb.AppendLine("]");

        switch (auth)
        {
            case ControlServerAuth.None:
                sb.AppendLine("auth = \"none\"");
                break;
            case ControlServerAuth.Basic:
                sb.AppendLine("auth = \"basic\"");
                sb.AppendLine($"username = \"{Escape(user)}\"");
                sb.AppendLine($"password = \"{Escape(password)}\"");
                break;
            case ControlServerAuth.ApiKey:
                sb.AppendLine("auth = \"apikey\"");
                sb.AppendLine($"apikey = \"{Escape(apiKey)}\"");
                break;
        }

        return sb.ToString();
    }

    private static string Escape(string? value)
        => (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
}
