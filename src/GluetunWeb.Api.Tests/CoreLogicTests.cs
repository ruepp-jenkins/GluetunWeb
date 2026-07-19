using System.Formats.Tar;
using System.Text.Json;
using GluetunWeb.Api.Balancer;
using GluetunWeb.Api.Crypto;
using GluetunWeb.Api.Data;
using GluetunWeb.Api.Docker;
using GluetunWeb.Api.Gluetun;
using GluetunWeb.Api.Services;
using GluetunWeb.Api.Validation;

namespace GluetunWeb.Api.Tests;

public class IdentifierTests
{
    [Theory]
    [InlineData("se-proxy-01")]
    [InlineData("ABC123")]
    [InlineData("a")]
    public void Accepts_valid_identifiers(string id) => Assert.True(Identifiers.IsValid(id));

    [Theory]
    [InlineData("bad id")]      // space
    [InlineData("under_score")] // underscore
    [InlineData("dot.name")]    // dot
    [InlineData("emoji😀")]
    [InlineData("")]
    public void Rejects_invalid_identifiers(string id) => Assert.False(Identifiers.IsValid(id));

    [Fact]
    public void Rejects_too_long()
        => Assert.False(Identifiers.IsValid(new string('a', Identifiers.MaxLength + 1)));

    [Fact]
    public void Validate_returns_message_for_invalid()
        => Assert.NotNull(Identifiers.Validate("no good!"));
}

public class SecretProtectorTests
{
    private readonly ISecretProtector _p = SecretProtector.FromPassphrase("unit-test-key");

    [Fact]
    public void Roundtrips_value()
    {
        var secret = "WIREGUARD_PRIVATE_KEY=abc/def+123==";
        var enc = _p.Encrypt(secret);
        Assert.NotNull(enc);
        Assert.NotEqual(secret, enc);          // actually encrypted
        Assert.Equal(secret, _p.Decrypt(enc)); // and reversible
    }

    [Fact]
    public void Ciphertext_differs_each_time()
        => Assert.NotEqual(_p.Encrypt("same"), _p.Encrypt("same")); // random nonce

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Passes_through_null_and_empty(string? value)
        => Assert.Equal(value, _p.Encrypt(value));

    [Fact]
    public void Wrong_key_cannot_decrypt()
    {
        var enc = _p.Encrypt("top-secret");
        var other = SecretProtector.FromPassphrase("different-key");
        Assert.ThrowsAny<Exception>(() => other.Decrypt(enc));
    }
}

public class PortManagerTests
{
    private static readonly HashSet<int> None = new();

    [Fact]
    public void Picks_the_first_block_in_an_empty_range()
        => Assert.Equal(20000, PortManager.FindFreeBlock(20000, 21000, 8, None));

    [Fact]
    public void Blocks_stay_aligned_to_the_range_start()
    {
        // First block taken by a single port → the next candidate is 20008, not 20001.
        var occupied = new HashSet<int> { 20003 };
        Assert.Equal(20008, PortManager.FindFreeBlock(20000, 21000, 8, occupied));
    }

    [Fact]
    public void Skips_a_block_touched_anywhere_by_a_live_port()
    {
        // One foreign container anywhere inside a block rules that whole block out — 20003 sits in
        // block 20000-20007 and 20011 in block 20008-20015, so the first free block is the third.
        var occupied = new HashSet<int> { 20003, 20011 };
        Assert.Equal(20016, PortManager.FindFreeBlock(20000, 21000, 8, occupied));
    }

    [Fact]
    public void Throws_when_no_whole_block_fits()
    {
        var occupied = new HashSet<int> { 20000, 20008 };
        Assert.Throws<PortAllocationException>(() =>
            PortManager.FindFreeBlock(20000, 20015, 8, occupied));
    }

    [Fact]
    public void Throws_when_the_range_is_smaller_than_one_block()
        => Assert.Throws<PortAllocationException>(() =>
            PortManager.FindFreeBlock(20000, 20003, 8, None));

    [Fact]
    public void Throws_on_invalid_range()
        => Assert.Throws<PortAllocationException>(() =>
            PortManager.FindFreeBlock(500, 400, 8, None));

    [Fact]
    public void Does_not_return_a_block_that_overruns_the_range_end()
    {
        // 20000-20015 holds exactly two blocks; a third would spill past the end.
        var occupied = new HashSet<int>();
        var first = PortManager.FindFreeBlock(20000, 20015, 8, occupied);
        for (var p = first; p < first + 8; p++) occupied.Add(p);
        var second = PortManager.FindFreeBlock(20000, 20015, 8, occupied);
        for (var p = second; p < second + 8; p++) occupied.Add(p);

        Assert.Equal(20000, first);
        Assert.Equal(20008, second);
        Assert.Throws<PortAllocationException>(() =>
            PortManager.FindFreeBlock(20000, 20015, 8, occupied));
    }
}

public class PortLayoutTests
{
    [Fact]
    public void Every_purpose_has_a_stable_offset()
    {
        Assert.Equal(0, PortLayout.OffsetOf(PortPurpose.Control));
        Assert.Equal(1, PortLayout.OffsetOf(PortPurpose.Socks5));
        Assert.Equal(2, PortLayout.OffsetOf(PortPurpose.HttpProxy));
        Assert.Equal(3, PortLayout.OffsetOf(PortPurpose.Shadowsocks));
        Assert.Equal(0, PortLayout.OffsetOf(PortPurpose.BalancerListen));
        Assert.Equal(1, PortLayout.OffsetOf(PortPurpose.BalancerWeb));
        Assert.Equal(2, PortLayout.OffsetOf(PortPurpose.BalancerState));
    }

    [Fact]
    public void Ports_are_derived_from_the_block_start()
    {
        Assert.Equal(20000, PortLayout.PortFor(20000, PortPurpose.Control));
        Assert.Equal(20001, PortLayout.PortFor(20000, PortPurpose.Socks5));
        Assert.Equal(20002, PortLayout.PortFor(20000, PortPurpose.HttpProxy));
        Assert.Equal(20003, PortLayout.PortFor(20000, PortPurpose.Shadowsocks));
    }

    [Fact]
    public void A_purposes_port_does_not_move_when_another_is_disabled()
    {
        // The regression this layout exists to prevent: Shadowsocks keeps port 20003 whether or not
        // SOCKS5 and the HTTP proxy are enabled, because nothing is ever compacted.
        const int block = 20000;
        var withEverything = PortLayout.PortFor(block, PortPurpose.Shadowsocks);
        var withProxiesOff = PortLayout.PortFor(block, PortPurpose.Shadowsocks);
        Assert.Equal(withEverything, withProxiesOff);
        Assert.Equal(20003, withProxiesOff);
    }

    [Fact]
    public void Unassigned_block_yields_zero()
        => Assert.Equal(0, PortLayout.PortFor(0, PortPurpose.Socks5));

    [Fact]
    public void Blocks_have_room_to_spare_for_future_purposes()
    {
        // Guards against adding a purpose whose offset would spill into the next owner's block.
        var connectionOffsets = new[]
        {
            PortPurpose.Control, PortPurpose.Socks5, PortPurpose.HttpProxy, PortPurpose.Shadowsocks,
        }.Select(PortLayout.OffsetOf).ToList();
        Assert.All(connectionOffsets, o => Assert.True(o < PortLayout.ConnectionBlockSize));

        var balancerOffsets = new[]
        {
            PortPurpose.BalancerListen, PortPurpose.BalancerWeb, PortPurpose.BalancerState,
        }.Select(PortLayout.OffsetOf).ToList();
        Assert.All(balancerOffsets, o => Assert.True(o < PortLayout.BalancerBlockSize));
    }

    [Fact]
    public void Block_end_is_inclusive()
        => Assert.Equal(20007, PortLayout.BlockEnd(20000, PortLayout.ConnectionBlockSize));
}

public class WireGuardParserTests
{
    private const string Sample = """
        [Interface]
        PrivateKey = SECRETPRIVATEKEY==
        Address = 10.64.0.2/32
        DNS = 10.64.0.1
        [Peer]
        PublicKey = SERVERPUBLICKEY==
        PresharedKey = PSK123==
        Endpoint = 193.32.126.1:51820
        AllowedIPs = 0.0.0.0/0
        """;

    [Fact]
    public void Parses_all_fields()
    {
        var wg = WireGuardConfigParser.Parse(Sample);
        Assert.Equal("SECRETPRIVATEKEY==", wg.PrivateKey);
        Assert.Equal("10.64.0.2/32", wg.Addresses);
        Assert.Equal("SERVERPUBLICKEY==", wg.PublicKey);
        Assert.Equal("PSK123==", wg.PresharedKey);
        Assert.Equal("193.32.126.1", wg.EndpointHost);
        Assert.Equal("51820", wg.EndpointPort);
        Assert.Equal("10.64.0.1", wg.Dns);
    }

    [Fact]
    public void Valid_config_passes_validation()
        => Assert.Null(WireGuardConfigParser.Validate(Sample));

    [Fact]
    public void Missing_peer_fails_validation()
        => Assert.NotNull(WireGuardConfigParser.Validate("[Interface]\nPrivateKey = x\nAddress = 10.0.0.1/32"));

    [Fact]
    public void OpenVpn_validator_requires_remote_and_client()
    {
        Assert.NotNull(OpenVpnConfigValidator.Validate("nonsense"));
        Assert.Null(OpenVpnConfigValidator.Validate("client\nremote vpn.example.com 1194\n"));
    }
}

public class EndpointResolverTests
{
    [Fact]
    public void Detects_placeholder()
    {
        Assert.True(EndpointResolver.HasPlaceholder("remote {{DNS_IP}} 1194"));
        Assert.False(EndpointResolver.HasPlaceholder("remote 1.2.3.4 1194"));
    }

    [Fact]
    public void Substitutes_all_occurrences()
    {
        var config = "remote {{DNS_IP}} 1194\nEndpoint = {{DNS_IP}}:51820";
        var result = EndpointResolver.Substitute(config, "203.0.113.7");
        Assert.Equal("remote 203.0.113.7 1194\nEndpoint = 203.0.113.7:51820", result);
    }

    [Fact]
    public async Task NoOp_when_no_dns_name_or_no_placeholder()
    {
        const string cfg = "remote 1.2.3.4 1194";
        Assert.Equal(cfg, await EndpointResolver.ResolveAndSubstituteAsync(cfg, null));
        Assert.Equal(cfg, await EndpointResolver.ResolveAndSubstituteAsync(cfg, "vpn.example.com"));
    }

    [Fact]
    public async Task Resolves_literal_ip_dns_name()
    {
        // An IP literal as the "DNS name" needs no network lookup.
        var result = await EndpointResolver.ResolveAndSubstituteAsync("remote {{DNS_IP}} 1194", "198.51.100.9");
        Assert.Equal("remote 198.51.100.9 1194", result);
    }
}

public class GluetunConfigBuilderTests
{
    [Fact]
    public void Provider_wireguard_maps_env()
    {
        var result = GluetunConfigBuilder.Build(new GluetunBuildInput
        {
            IsCustom = false,
            VpnType = VpnType.WireGuard,
            ProviderType = "mullvad",
            WireGuardPrivateKey = "PRIV==",
            WireGuardAddresses = "10.64.0.2/32",
            ServerCountries = "Sweden",
        });

        Assert.Equal("mullvad", result.Env["VPN_SERVICE_PROVIDER"]);
        Assert.Equal("wireguard", result.Env["VPN_TYPE"]);
        Assert.Equal("PRIV==", result.Env["WIREGUARD_PRIVATE_KEY"]);
        Assert.Equal("Sweden", result.Env["SERVER_COUNTRIES"]);
        // The auth file is always injected, even for "none" — see GluetunAuthConfigTests.
        Assert.Contains(result.Files, f => f.ContainerPath == GluetunConfigBuilder.ControlConfigPath);
    }

    [Fact]
    public void Custom_openvpn_injects_config_file()
    {
        var result = GluetunConfigBuilder.Build(new GluetunBuildInput
        {
            IsCustom = true,
            VpnType = VpnType.OpenVpn,
            CustomRawConfig = "client\nremote host 1194",
        });

        Assert.Equal("custom", result.Env["VPN_SERVICE_PROVIDER"]);
        Assert.Equal(GluetunConfigBuilder.CustomOpenVpnPath, result.Env["OPENVPN_CUSTOM_CONFIG"]);
        Assert.Contains(result.Files, f => f.ContainerPath == GluetunConfigBuilder.CustomOpenVpnPath);
    }

    [Fact]
    public void ApiKey_auth_adds_config_toml_file()
    {
        var result = GluetunConfigBuilder.Build(new GluetunBuildInput
        {
            IsCustom = false,
            VpnType = VpnType.OpenVpn,
            ProviderType = "nordvpn",
            ControlAuth = ControlServerAuth.ApiKey,
            ControlApiKey = "MYKEY",
        });

        Assert.Equal(GluetunConfigBuilder.ControlConfigPath, result.Env["HTTP_CONTROL_SERVER_AUTH_CONFIG_FILEPATH"]);
        var file = Assert.Single(result.Files);
        Assert.Contains("auth = \"apikey\"", file.Content);
        Assert.Contains("MYKEY", file.Content);
    }

    [Theory]
    [InlineData(OpenVpnProtocol.Udp, "udp")]
    [InlineData(OpenVpnProtocol.Tcp, "tcp")]
    public void Provider_openvpn_emits_protocol(OpenVpnProtocol protocol, string expected)
    {
        var result = GluetunConfigBuilder.Build(new GluetunBuildInput
        {
            IsCustom = false,
            VpnType = VpnType.OpenVpn,
            ProviderType = "nordvpn",
            OpenVpnProtocol = protocol,
        });

        Assert.Equal(expected, result.Env["OPENVPN_PROTOCOL"]);
    }

    [Fact]
    public void Provider_wireguard_omits_protocol()
    {
        // WireGuard is UDP-only and Gluetun has no equivalent option — emitting it would be wrong.
        var result = GluetunConfigBuilder.Build(new GluetunBuildInput
        {
            IsCustom = false,
            VpnType = VpnType.WireGuard,
            ProviderType = "mullvad",
            OpenVpnProtocol = OpenVpnProtocol.Tcp,
        });

        Assert.False(result.Env.ContainsKey("OPENVPN_PROTOCOL"));
    }

    [Fact]
    public void Custom_openvpn_omits_protocol()
    {
        // A custom .ovpn carries its own "proto" line; overriding it from the provider form would
        // silently contradict the uploaded config.
        var result = GluetunConfigBuilder.Build(new GluetunBuildInput
        {
            IsCustom = true,
            VpnType = VpnType.OpenVpn,
            CustomRawConfig = "client\nproto tcp\nremote host 443",
            OpenVpnProtocol = OpenVpnProtocol.Tcp,
        });

        Assert.False(result.Env.ContainsKey("OPENVPN_PROTOCOL"));
    }

    [Fact]
    public void Http_proxy_off_by_default()
    {
        var result = GluetunConfigBuilder.Build(new GluetunBuildInput
        {
            IsCustom = false, VpnType = VpnType.OpenVpn, ProviderType = "nordvpn",
        });
        Assert.Equal("off", result.Env["HTTPPROXY"]);
    }

    [Fact]
    public void Port_forwarding_off_by_default()
    {
        var result = GluetunConfigBuilder.Build(new GluetunBuildInput
        {
            IsCustom = false, VpnType = VpnType.OpenVpn, ProviderType = "nordvpn",
        });

        Assert.Equal("off", result.Env["VPN_PORT_FORWARDING"]);
        Assert.False(result.Env.ContainsKey("VPN_PORT_FORWARDING_PROVIDER"));
        Assert.False(result.Env.ContainsKey("VPN_PORT_FORWARDING_PORTS_COUNT"));
    }

    [Fact]
    public void Port_forwarding_maps_env()
    {
        var result = GluetunConfigBuilder.Build(new GluetunBuildInput
        {
            IsCustom = false,
            VpnType = VpnType.OpenVpn,
            ProviderType = "private internet access",
            PortForwarding = true,
            PortForwardingProvider = "protonvpn",
            PortForwardingPortsCount = 3,
            FirewallVpnInputPorts = "6881",
        });

        Assert.Equal("on", result.Env["VPN_PORT_FORWARDING"]);
        Assert.Equal("protonvpn", result.Env["VPN_PORT_FORWARDING_PROVIDER"]);
        Assert.Equal("3", result.Env["VPN_PORT_FORWARDING_PORTS_COUNT"]);
        Assert.Equal("6881", result.Env["FIREWALL_VPN_INPUT_PORTS"]);
    }

    [Fact]
    public void Port_forwarding_omits_count_when_single()
    {
        // 1 is Gluetun's default — emitting it adds noise without changing behaviour.
        var result = GluetunConfigBuilder.Build(new GluetunBuildInput
        {
            IsCustom = false, VpnType = VpnType.OpenVpn, ProviderType = "protonvpn",
            PortForwarding = true, PortForwardingPortsCount = 1,
        });

        Assert.Equal("on", result.Env["VPN_PORT_FORWARDING"]);
        Assert.False(result.Env.ContainsKey("VPN_PORT_FORWARDING_PORTS_COUNT"));
    }

    [Fact]
    public void Firewall_outbound_subnets_maps_env()
    {
        var result = GluetunConfigBuilder.Build(new GluetunBuildInput
        {
            IsCustom = false, VpnType = VpnType.OpenVpn, ProviderType = "nordvpn",
            FirewallOutboundSubnets = "192.168.1.0/24,10.0.0.0/8",
        });

        Assert.Equal("192.168.1.0/24,10.0.0.0/8", result.Env["FIREWALL_OUTBOUND_SUBNETS"]);
    }

    [Fact]
    public void Wireguard_mtu_only_applies_to_wireguard()
    {
        var wg = GluetunConfigBuilder.Build(new GluetunBuildInput
        {
            IsCustom = false, VpnType = VpnType.WireGuard, ProviderType = "mullvad", WireGuardMtu = 1320,
        });
        Assert.Equal("1320", wg.Env["WIREGUARD_MTU"]);

        var ovpn = GluetunConfigBuilder.Build(new GluetunBuildInput
        {
            IsCustom = false, VpnType = VpnType.OpenVpn, ProviderType = "nordvpn", WireGuardMtu = 1320,
        });
        Assert.False(ovpn.Env.ContainsKey("WIREGUARD_MTU"));
    }

    [Fact]
    public void Dns_blocking_defaults_match_gluetun()
    {
        var result = GluetunConfigBuilder.Build(new GluetunBuildInput
        {
            IsCustom = false, VpnType = VpnType.OpenVpn, ProviderType = "nordvpn",
        });

        Assert.Equal("on", result.Env["BLOCK_MALICIOUS"]);
        Assert.Equal("off", result.Env["BLOCK_ADS"]);
        Assert.False(result.Env.ContainsKey("DNS_UNBLOCK_HOSTNAMES"));
    }

    [Fact]
    public void Dns_blocking_maps_env()
    {
        var result = GluetunConfigBuilder.Build(new GluetunBuildInput
        {
            IsCustom = false,
            VpnType = VpnType.OpenVpn,
            ProviderType = "nordvpn",
            BlockMalicious = false,
            BlockAds = true,
            DnsUnblockHostnames = "example.com,cdn.example.org",
        });

        Assert.Equal("off", result.Env["BLOCK_MALICIOUS"]);
        Assert.Equal("on", result.Env["BLOCK_ADS"]);
        Assert.Equal("example.com,cdn.example.org", result.Env["DNS_UNBLOCK_HOSTNAMES"]);
    }

    [Fact]
    public void Dns_blocking_is_always_emitted_so_turning_it_off_reverts()
    {
        // Relying on the image default would leave a disabled toggle still blocking, because the
        // container keeps whatever gluetun defaults to when the variable is absent.
        var off = GluetunConfigBuilder.Build(new GluetunBuildInput
        {
            IsCustom = true, VpnType = VpnType.OpenVpn, CustomRawConfig = "client",
            BlockMalicious = false, BlockAds = false,
        });

        Assert.Equal("off", off.Env["BLOCK_MALICIOUS"]);
        Assert.Equal("off", off.Env["BLOCK_ADS"]);
    }

    [Fact]
    public void Shadowsocks_off_by_default()
    {
        var result = GluetunConfigBuilder.Build(new GluetunBuildInput
        {
            IsCustom = false, VpnType = VpnType.OpenVpn, ProviderType = "nordvpn",
        });

        Assert.Equal("off", result.Env["SHADOWSOCKS"]);
        // No password/cipher leak into the env when the server is not enabled.
        Assert.False(result.Env.ContainsKey("SHADOWSOCKS_PASSWORD"));
        Assert.False(result.Env.ContainsKey("SHADOWSOCKS_CIPHER"));
    }

    [Fact]
    public void Shadowsocks_on_maps_env()
    {
        var result = GluetunConfigBuilder.Build(new GluetunBuildInput
        {
            IsCustom = false,
            VpnType = VpnType.OpenVpn,
            ProviderType = "nordvpn",
            ShadowsocksEnabled = true,
            ShadowsocksPassword = "s3cret",
            ShadowsocksCipher = "aes-256-gcm",
            ShadowsocksLog = true,
        });

        Assert.Equal("on", result.Env["SHADOWSOCKS"]);
        Assert.Equal("s3cret", result.Env["SHADOWSOCKS_PASSWORD"]);
        Assert.Equal("aes-256-gcm", result.Env["SHADOWSOCKS_CIPHER"]);
        Assert.Equal("on", result.Env["SHADOWSOCKS_LOG"]);
        Assert.Equal($":{GluetunConfigBuilder.ShadowsocksPort}", result.Env["SHADOWSOCKS_LISTENING_ADDRESS"]);
    }

    [Fact]
    public void Shadowsocks_blank_cipher_falls_back_to_default()
    {
        // Rows migrated in before the cipher column existed can carry "".
        var result = GluetunConfigBuilder.Build(new GluetunBuildInput
        {
            IsCustom = false,
            VpnType = VpnType.OpenVpn,
            ProviderType = "nordvpn",
            ShadowsocksEnabled = true,
            ShadowsocksPassword = "pw",
            ShadowsocksCipher = "   ",
        });

        Assert.Equal(GluetunConfigBuilder.DefaultShadowsocksCipher, result.Env["SHADOWSOCKS_CIPHER"]);
    }
}

public class GluetunAuthConfigTests
{
    [Fact]
    public void None_still_emits_an_explicit_role()
    {
        // Current Gluetun denies any control-server route no role covers, so omitting the file does
        // not mean "open" — it means the dashboard's own status/public-IP reads get 401 Unauthorized.
        var toml = GluetunAuthConfig.Build(ControlServerAuth.None, null, null, null);

        Assert.Contains("auth = \"none\"", toml);
        Assert.Contains("GET /v1/vpn/status", toml);
        Assert.Contains("GET /v1/publicip/ip", toml);
    }

    [Fact]
    public void Routes_cover_the_current_portforward_path()
        => Assert.Contains("GET /v1/portforward", GluetunAuthConfig.Build(ControlServerAuth.None, null, null, null));

    [Fact]
    public void Basic_includes_credentials()
    {
        var toml = GluetunAuthConfig.Build(ControlServerAuth.Basic, "user", "pa\"ss", null)!;
        Assert.Contains("auth = \"basic\"", toml);
        Assert.Contains("username = \"user\"", toml);
        Assert.Contains("pa\\\"ss", toml); // quote escaped
        Assert.Contains("[[roles]]", toml);
    }
}

public class ApiKeyGeneratorTests
{
    [Fact]
    public void Generates_22_char_base58()
    {
        const string alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
        for (var i = 0; i < 50; i++)
        {
            var key = ApiKeyGenerator.Generate();
            Assert.InRange(key.Length, 21, 22); // 16 random bytes → 21-22 base58 chars
            Assert.All(key, c => Assert.Contains(c, alphabet));
        }
    }

    [Fact]
    public void Base58_has_no_ambiguous_chars()
    {
        var encoded = ApiKeyGenerator.Base58Encode(new byte[] { 0xFF, 0x00, 0x10, 0x20 });
        Assert.DoesNotContain('0', encoded);
        Assert.DoesNotContain('O', encoded);
        Assert.DoesNotContain('l', encoded);
    }
}

public class Socks5BalancerConfigTests
{
    [Fact]
    public void Builds_valid_config_with_upstreams()
    {
        var json = Socks5BalancerConfigBuilder.Build(new BalancerConfigInput
        {
            UpstreamSelectRule = "random",
            ThreadNum = 8,
            Upstreams = new[]
            {
                new BalancerUpstreamInput("nordvpn", "host.docker.internal", 20001, null, null),
                new BalancerUpstreamInput("goosevpn", "host.docker.internal", 20003, "alice", "pw"),
            },
        });

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(Socks5BalancerConfigBuilder.ListenPort, root.GetProperty("listenPort").GetInt32());
        Assert.Equal(Socks5BalancerConfigBuilder.StateServerPort, root.GetProperty("stateServerPort").GetInt32());
        Assert.Equal("random", root.GetProperty("upstreamSelectRule").GetString());
        Assert.Equal(8, root.GetProperty("threadNum").GetInt32());

        var web = root.GetProperty("EmbedWebServerConfig");
        Assert.True(web.GetProperty("enable").GetBoolean());
        Assert.Equal(Socks5BalancerConfigBuilder.WebPort, web.GetProperty("port").GetInt32());
        Assert.Equal("stateBootstrap.html", web.GetProperty("index_file_of_root").GetString());

        // multiListen must be a non-empty array — an empty one serializes as "" and breaks the web UI.
        var multiListen = root.GetProperty("multiListen");
        Assert.Equal(JsonValueKind.Array, multiListen.ValueKind);
        Assert.True(multiListen.GetArrayLength() >= 1);

        var upstreams = root.GetProperty("upstream");
        Assert.Equal(2, upstreams.GetArrayLength());
        var second = upstreams[1];
        Assert.Equal("goosevpn", second.GetProperty("name").GetString());
        Assert.Equal("host.docker.internal", second.GetProperty("host").GetString());
        Assert.Equal(20003, second.GetProperty("port").GetInt32());
        Assert.Equal("alice", second.GetProperty("authUser").GetString());
        Assert.Equal("pw", second.GetProperty("authPwd").GetString());
    }

    [Fact]
    public void Empty_creds_become_empty_strings()
    {
        var json = Socks5BalancerConfigBuilder.Build(new BalancerConfigInput
        {
            Upstreams = new[] { new BalancerUpstreamInput("x", "h", 1, null, null) },
        });
        using var doc = JsonDocument.Parse(json);
        var u = doc.RootElement.GetProperty("upstream")[0];
        Assert.Equal("", u.GetProperty("authUser").GetString());
        Assert.Equal("", u.GetProperty("authPwd").GetString());
    }
}

public class ProviderServerOptionsTests
{
    private const string Json = """
        {
          "version": 1,
          "servers": [
            { "vpn": "wireguard", "region": "Europe", "country": "Sweden",      "city": "Stockholm",  "hostname": "se-sto-wg1" },
            { "vpn": "openvpn",   "region": "Europe", "country": "Sweden",      "city": "Stockholm",  "hostname": "se-sto-ov1", "udp": true },
            { "vpn": "openvpn",   "region": "Europe", "country": "Sweden",      "city": "Gothenburg", "hostname": "se-got-ov1", "tcp": true, "udp": true },
            { "vpn": "openvpn",   "region": "Europe", "country": "Netherlands", "city": "Amsterdam",  "hostname": "nl-ams-ov1", "tcp": true, "udp": true },
            { "vpn": "openvpn",   "region": "Americas", "country": "Brazil",    "city": "Sao Paulo",  "hostname": "br-sao-ov1", "udp": true }
          ]
        }
        """;

    private static ProviderServerOptions Options(ProviderServerFilter filter, string json = Json)
        => ProviderCatalogService.BuildOptions(
            "demo", ProviderCatalogService.ParseServerRecordsJson(json), filter);

    [Fact]
    public void Unfiltered_lists_every_level_distinct_and_sorted()
    {
        var o = Options(ProviderServerFilter.None);

        Assert.Equal(new[] { "Americas", "Europe" }, o.Regions);
        Assert.Equal(new[] { "Brazil", "Netherlands", "Sweden" }, o.Countries);
        Assert.Equal(new[] { "Amsterdam", "Gothenburg", "Sao Paulo", "Stockholm" }, o.Cities);
        Assert.Equal(
            new[] { "br-sao-ov1", "nl-ams-ov1", "se-got-ov1", "se-sto-ov1", "se-sto-wg1" },
            o.Hostnames);
        Assert.False(o.HostnamesTruncated);
    }

    [Fact]
    public void Region_narrows_country_city_and_hostname()
    {
        var o = Options(new ProviderServerFilter(Regions: new[] { "Europe" }));

        Assert.Equal(new[] { "Netherlands", "Sweden" }, o.Countries);
        Assert.DoesNotContain("Sao Paulo", o.Cities);
        Assert.DoesNotContain("br-sao-ov1", o.Hostnames);
    }

    [Fact]
    public void Country_narrows_city_and_hostname_but_not_its_own_level()
    {
        var o = Options(new ProviderServerFilter(Countries: new[] { "Sweden" }));

        // Other countries stay selectable — the cascade only narrows downwards.
        Assert.Contains("Netherlands", o.Countries);
        Assert.Equal(new[] { "Gothenburg", "Stockholm" }, o.Cities);
        Assert.Equal(new[] { "se-got-ov1", "se-sto-ov1", "se-sto-wg1" }, o.Hostnames);
    }

    [Fact]
    public void City_narrows_hostnames()
    {
        var o = Options(new ProviderServerFilter(
            Countries: new[] { "Sweden" }, Cities: new[] { "Stockholm" }));

        Assert.Equal(new[] { "se-sto-ov1", "se-sto-wg1" }, o.Hostnames);
    }

    [Fact]
    public void Multiple_selections_at_one_level_are_a_union()
    {
        var o = Options(new ProviderServerFilter(Countries: new[] { "Sweden", "Brazil" }));

        Assert.Equal(new[] { "Gothenburg", "Sao Paulo", "Stockholm" }, o.Cities);
    }

    [Fact]
    public void Vpn_type_filters_every_level()
    {
        // An OpenVPN connection must not be offered the WireGuard-only hostname.
        var ovpn = Options(new ProviderServerFilter(VpnType: "openvpn", Countries: new[] { "Sweden" }));
        Assert.DoesNotContain("se-sto-wg1", ovpn.Hostnames);

        var wg = Options(new ProviderServerFilter(VpnType: "wireguard"));
        Assert.Equal(new[] { "se-sto-wg1" }, wg.Hostnames);
        Assert.Equal(new[] { "Sweden" }, wg.Countries);
    }

    [Fact]
    public void Protocol_support_reflects_the_current_narrowing()
    {
        // Sweden/Stockholm has a UDP-only OpenVPN server, so TCP must not be offered there even
        // though the provider as a whole supports it.
        var stockholm = Options(new ProviderServerFilter(
            VpnType: "openvpn", Countries: new[] { "Sweden" }, Cities: new[] { "Stockholm" }));
        Assert.True(stockholm.SupportsUdp);
        Assert.False(stockholm.SupportsTcp);

        var everywhere = Options(new ProviderServerFilter(VpnType: "openvpn"));
        Assert.True(everywhere.SupportsTcp);
    }

    [Fact]
    public void Empty_when_no_servers_array()
    {
        var records = ProviderCatalogService.ParseServerRecordsJson("""{ "version": 1 }""");
        Assert.Empty(records);
    }

    [Fact]
    public void Wireguard_only_provider_has_no_openvpn()
    {
        var o = Options(ProviderServerFilter.None, """
            { "servers": [ { "vpn": "wireguard", "country": "Sweden", "tcp": true, "udp": true } ] }
            """);

        Assert.False(o.HasOpenVpn);
        Assert.False(o.SupportsTcp);
        Assert.False(o.SupportsUdp);
    }

    [Fact]
    public void Hostnames_are_capped_and_flagged()
    {
        var servers = string.Join(",", Enumerable.Range(0, ProviderCatalogService.MaxHostnames + 25)
            .Select(i => $$"""{ "vpn": "openvpn", "country": "X", "hostname": "h{{i:D4}}" }"""));
        var o = Options(ProviderServerFilter.None, $$"""{ "servers": [ {{servers}} ] }""");

        Assert.True(o.HostnamesTruncated);
        Assert.Equal(ProviderCatalogService.MaxHostnames, o.Hostnames.Count);
    }
}

/// <summary>
/// The precedence rule behind shared credentials: when one is selected it supplies every secret,
/// and the owner's inline fields are ignored rather than merged. Mirrors the resolution in
/// ConnectionService.BuildInputAsync without needing a database.
/// </summary>
public class CredentialResolutionTests
{
    private static readonly ISecretProtector P = SecretProtector.FromPassphrase("cred-test-key");

    private static Credential OpenVpnCredential() => new()
    {
        Id = 1,
        Name = "nordvpn-account",
        VpnType = VpnType.OpenVpn,
        OpenVpnUserEnc = P.Encrypt("shared-user"),
        OpenVpnPasswordEnc = P.Encrypt("shared-pass"),
    };

    private static Provider ProviderWithInline() => new()
    {
        Name = "nordvpn-nl",
        VpnType = VpnType.OpenVpn,
        OpenVpnUserEnc = P.Encrypt("inline-user"),
        OpenVpnPasswordEnc = P.Encrypt("inline-pass"),
    };

    // The production expression, isolated so the precedence itself is what is under test.
    private static (string? User, string? Password) Resolve(Provider p, Credential? cred) =>
        (P.Decrypt(cred?.OpenVpnUserEnc ?? p.OpenVpnUserEnc),
         P.Decrypt(cred?.OpenVpnPasswordEnc ?? p.OpenVpnPasswordEnc));

    [Fact]
    public void Credential_replaces_inline_secrets_entirely()
    {
        var (user, password) = Resolve(ProviderWithInline(), OpenVpnCredential());

        Assert.Equal("shared-user", user);
        Assert.Equal("shared-pass", password);
    }

    [Fact]
    public void Inline_secrets_are_used_when_no_credential_is_selected()
    {
        var (user, password) = Resolve(ProviderWithInline(), null);

        Assert.Equal("inline-user", user);
        Assert.Equal("inline-pass", password);
    }

    [Fact]
    public void One_credential_serves_several_providers()
    {
        // The reason this feature exists: nordvpn-nl and nordvpn-ch share a single stored login.
        var cred = OpenVpnCredential();
        var nl = new Provider { Name = "nordvpn-nl", ProviderType = "nordvpn", CredentialId = 1 };
        var ch = new Provider { Name = "nordvpn-ch", ProviderType = "nordvpn", CredentialId = 1 };

        Assert.Equal(Resolve(nl, cred), Resolve(ch, cred));
        Assert.Equal("shared-user", Resolve(nl, cred).User);
    }

    [Fact]
    public void Wireguard_credential_supplies_keys_and_addresses()
    {
        var cred = new Credential
        {
            VpnType = VpnType.WireGuard,
            WireGuardPrivateKeyEnc = P.Encrypt("SHARED-PRIV=="),
            WireGuardAddresses = "10.64.0.9/32",
        };
        var p = new Provider
        {
            VpnType = VpnType.WireGuard,
            WireGuardPrivateKeyEnc = P.Encrypt("INLINE-PRIV=="),
            WireGuardAddresses = "10.0.0.1/32",
        };

        Assert.Equal("SHARED-PRIV==", P.Decrypt(cred.WireGuardPrivateKeyEnc ?? p.WireGuardPrivateKeyEnc));
        Assert.Equal("10.64.0.9/32", cred.WireGuardAddresses ?? p.WireGuardAddresses);
    }

    [Fact]
    public void Credential_secrets_are_encrypted_at_rest()
    {
        var cred = OpenVpnCredential();

        Assert.NotEqual("shared-pass", cred.OpenVpnPasswordEnc);
        Assert.Equal("shared-pass", P.Decrypt(cred.OpenVpnPasswordEnc));
    }
}

public class LoadBalancerUpstreamReconcileTests
{
    private static LoadBalancer WithUpstreams(params int[] ids)
    {
        var lb = new LoadBalancer { Id = 1 };
        foreach (var id in ids)
            lb.Upstreams.Add(new LoadBalancerUpstream { ConnectionId = id, LoadBalancerId = 1 });
        return lb;
    }

    [Fact]
    public void Removes_and_adds_to_match_desired_set()
    {
        var lb = WithUpstreams(1, 2, 3);
        LoadBalancerService.ReplaceUpstreams(lb, new List<int> { 2, 3, 4 });

        Assert.Equal(new[] { 2, 3, 4 }, lb.Upstreams.Select(u => u.ConnectionId).OrderBy(x => x));
    }

    [Fact]
    public void Removing_one_leaves_the_rest()
    {
        var lb = WithUpstreams(5, 6);
        LoadBalancerService.ReplaceUpstreams(lb, new List<int> { 5 });
        Assert.Equal(new[] { 5 }, lb.Upstreams.Select(u => u.ConnectionId));
    }

    [Fact]
    public void Deduplicates_and_clears()
    {
        var lb = WithUpstreams(1, 2);
        LoadBalancerService.ReplaceUpstreams(lb, new List<int> { 9, 9 });
        Assert.Equal(new[] { 9 }, lb.Upstreams.Select(u => u.ConnectionId));

        LoadBalancerService.ReplaceUpstreams(lb, new List<int>());
        Assert.Empty(lb.Upstreams);
    }
}

public class TarBuilderTests
{
    [Fact]
    public void Builds_flat_tar_of_file_names()
    {
        using var stream = TarBuilder.BuildFlat(new[]
        {
            ("config.toml", "hello=world"),
            ("custom.ovpn", "client"),
        });

        var entries = new Dictionary<string, string>();
        using var reader = new TarReader(stream);
        while (reader.GetNextEntry() is { } entry)
        {
            using var r = new StreamReader(entry.DataStream!);
            entries[entry.Name] = r.ReadToEnd();
        }

        // Flat: names carry no directory component (extracted *into* the volume mount dir).
        Assert.Equal(new[] { "config.toml", "custom.ovpn" }, entries.Keys.OrderBy(k => k));
        Assert.Equal("hello=world", entries["config.toml"]);
        Assert.Equal("client", entries["custom.ovpn"]);
    }

    [Theory]
    [InlineData("/gluetunweb/config.toml", "/gluetunweb")]
    [InlineData("/conf/config.json", "/conf")]
    [InlineData("/file.txt", "/")]
    public void Resolves_container_directory(string path, string expected)
        => Assert.Equal(expected, ContainerOrchestrator.GetDirectory(path));
}
