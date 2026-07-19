using GluetunWeb.Api.Crypto;
using GluetunWeb.Api.Data;
using GluetunWeb.Api.Gluetun;
using GluetunWeb.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GluetunWeb.Api.Services;

public class CustomVpnService(AppDbContext db, ISecretProtector protector)
{
    /// <summary>Includes the shared credential so responses can name it.</summary>
    private IQueryable<CustomVpnConfig> LoadQuery() =>
        db.CustomVpnConfigs.AsNoTracking().Include(c => c.Credential);

    /// <summary>
    /// A referenced credential must exist and be an OpenVPN one — a WireGuard custom config carries
    /// its keys in the config text, so only OPENVPN_USER/PASSWORD can come from a credential.
    /// </summary>
    private async Task ValidateCredentialAsync(int? credentialId, CancellationToken ct)
    {
        if (credentialId is null) return;

        var cred = await db.Credentials.AsNoTracking().FirstOrDefaultAsync(c => c.Id == credentialId, ct)
                   ?? throw new ValidationException("The selected credential no longer exists.");

        if (cred.VpnType != VpnType.OpenVpn)
        {
            throw new ValidationException(
                $"Credential '{cred.Name}' is a WireGuard credential. Custom configs can only take " +
                "OpenVPN credentials — WireGuard keys belong in the config text itself.");
        }
    }

    public async Task<List<CustomVpnResponse>> ListAsync(CancellationToken ct = default)
    {
        var items = await LoadQuery().OrderBy(c => c.Name).ToListAsync(ct);
        return items.Select(ToResponse).ToList();
    }

    public async Task<CustomVpnResponse?> GetAsync(int id, CancellationToken ct = default)
    {
        var c = await LoadQuery().FirstOrDefaultAsync(x => x.Id == id, ct);
        return c is null ? null : ToResponse(c);
    }

    public async Task<CustomVpnResponse> CreateAsync(CustomVpnRequest r, CancellationToken ct = default)
    {
        var vpnType = ParseAndValidate(r, requireConfig: true);
        await ValidateCredentialAsync(r.CredentialId, ct);
        if (await db.CustomVpnConfigs.AnyAsync(c => c.Name == r.Name.Trim(), ct))
            throw new ValidationException($"A custom config named '{r.Name}' already exists.");

        var c = new CustomVpnConfig
        {
            Name = r.Name.Trim(),
            VpnType = vpnType,
            RawConfigEnc = protector.Encrypt(r.RawConfig!.Trim())!,
            Notes = Blank(r.Notes),
            OpenVpnUserEnc = ApplySecret(r.OpenVpnUser, null),
            OpenVpnPasswordEnc = ApplySecret(r.OpenVpnPassword, null),
            EndpointDnsName = Blank(r.EndpointDnsName),
            CredentialId = r.CredentialId,
        };
        db.CustomVpnConfigs.Add(c);
        await db.SaveChangesAsync(ct);
        return ToResponse(await LoadQuery().FirstAsync(x => x.Id == c.Id, ct));
    }

    public async Task<CustomVpnResponse?> UpdateAsync(int id, CustomVpnRequest r, CancellationToken ct = default)
    {
        var c = await db.CustomVpnConfigs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return null;

        // rawConfig null => keep existing config; validate only if a new one was provided.
        var vpnType = ParseAndValidate(r, requireConfig: r.RawConfig is not null);
        await ValidateCredentialAsync(r.CredentialId, ct);
        if (await db.CustomVpnConfigs.AnyAsync(x => x.Name == r.Name.Trim() && x.Id != id, ct))
            throw new ValidationException($"A custom config named '{r.Name}' already exists.");

        c.Name = r.Name.Trim();
        c.VpnType = vpnType;
        c.Notes = Blank(r.Notes);
        c.OpenVpnUserEnc = ApplySecret(r.OpenVpnUser, c.OpenVpnUserEnc);
        c.OpenVpnPasswordEnc = ApplySecret(r.OpenVpnPassword, c.OpenVpnPasswordEnc);
        c.EndpointDnsName = Blank(r.EndpointDnsName);
        c.CredentialId = r.CredentialId;
        if (r.RawConfig is not null)
            c.RawConfigEnc = protector.Encrypt(r.RawConfig.Trim())!;
        c.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return ToResponse(await LoadQuery().FirstAsync(x => x.Id == id, ct));
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var c = await db.CustomVpnConfigs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        if (await db.Connections.AnyAsync(x => x.CustomVpnConfigId == id, ct))
            throw new ValidationException("Custom config is in use by one or more connections.");
        db.CustomVpnConfigs.Remove(c);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Returns the decrypted raw config (server-side use only, e.g. building a container).</summary>
    public async Task<string?> GetRawConfigAsync(int id, CancellationToken ct = default)
    {
        var c = await db.CustomVpnConfigs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return c is null ? null : protector.Decrypt(c.RawConfigEnc);
    }

    private static VpnType ParseAndValidate(CustomVpnRequest r, bool requireConfig)
    {
        if (string.IsNullOrWhiteSpace(r.Name))
            throw new ValidationException("Config name is required.");
        var normalized = (r.VpnType ?? string.Empty).Trim().ToLowerInvariant();
        var vpnType = normalized switch
        {
            "wireguard" or "wg" => VpnType.WireGuard,
            "openvpn" or "ovpn" => VpnType.OpenVpn,
            _ => throw new ValidationException("VPN type must be 'openvpn' or 'wireguard'."),
        };

        if (requireConfig)
        {
            if (string.IsNullOrWhiteSpace(r.RawConfig))
                throw new ValidationException("Config content (file upload or raw text) is required.");
            var error = vpnType == VpnType.WireGuard
                ? WireGuardConfigParser.Validate(r.RawConfig)
                : OpenVpnConfigValidator.Validate(r.RawConfig);
            if (error is not null)
                throw new ValidationException(error);

            // Guard the DNS-placeholder workflow against a common mistake.
            if (EndpointResolver.HasPlaceholder(r.RawConfig) && string.IsNullOrWhiteSpace(r.EndpointDnsName))
                throw new ValidationException(
                    $"Config uses the {EndpointResolver.Placeholder} placeholder — set an endpoint DNS name so it can be resolved.");
        }
        return vpnType;
    }

    private string? ApplySecret(string? incoming, string? existingEnc)
    {
        if (incoming is null) return existingEnc; // unchanged
        if (incoming.Length == 0) return null;     // cleared
        return protector.Encrypt(incoming);
    }

    private static string? Blank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    /// <summary>Builds a non-secret one-line summary (endpoint/remote host) for the table.</summary>
    private string Summarize(CustomVpnConfig c)
    {
        try
        {
            var raw = protector.Decrypt(c.RawConfigEnc) ?? string.Empty;
            if (c.VpnType == VpnType.WireGuard)
            {
                var wg = WireGuardConfigParser.Parse(raw);
                var ep = wg.EndpointHost is null ? "?" : $"{wg.EndpointHost}:{wg.EndpointPort ?? "?"}";
                return $"wireguard → endpoint {ep}";
            }
            var remote = raw.Split('\n')
                .Select(l => l.Trim())
                .FirstOrDefault(l => l.StartsWith("remote ", StringComparison.OrdinalIgnoreCase));
            return remote is null ? "openvpn" : $"openvpn → {remote}";
        }
        catch
        {
            return c.VpnType.ToString().ToLowerInvariant();
        }
    }

    private CustomVpnResponse ToResponse(CustomVpnConfig c) => new(
        c.Id,
        c.Name,
        c.VpnType.ToString().ToLowerInvariant(),
        c.Notes,
        Summarize(c),
        c.CredentialId,
        c.Credential?.Name,
        protector.Decrypt(c.OpenVpnUserEnc),
        !string.IsNullOrEmpty(c.OpenVpnPasswordEnc),
        c.EndpointDnsName,
        c.CreatedAt,
        c.UpdatedAt);
}
