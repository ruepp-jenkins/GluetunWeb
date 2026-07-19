using GluetunWeb.Api.Crypto;
using GluetunWeb.Api.Data;
using GluetunWeb.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GluetunWeb.Api.Services;

/// <summary>
/// Named, reusable credential sets. One VPN account usually backs several providers (the same
/// NordVPN login for a NL and a CH provider entry), so the secrets live here once and are referenced
/// rather than duplicated. As everywhere else, secrets are decrypted only when building a container
/// and are never serialized back to the browser.
/// </summary>
public class CredentialService(AppDbContext db, ISecretProtector protector)
{
    public async Task<List<CredentialResponse>> ListAsync(CancellationToken ct = default)
    {
        var items = await db.Credentials.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
        var result = new List<CredentialResponse>(items.Count);
        foreach (var c in items)
            result.Add(ToResponse(c, await CountUsagesAsync(c.Id, ct)));
        return result;
    }

    public async Task<CredentialResponse?> GetAsync(int id, CancellationToken ct = default)
    {
        var c = await db.Credentials.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return c is null ? null : ToResponse(c, await CountUsagesAsync(id, ct));
    }

    public async Task<CredentialResponse> CreateAsync(CredentialRequest r, CancellationToken ct = default)
    {
        var vpnType = Validate(r);
        if (await db.Credentials.AnyAsync(c => c.Name == r.Name.Trim(), ct))
            throw new ValidationException($"A credential named '{r.Name}' already exists.");

        var c = new Credential();
        Apply(c, r, vpnType, isCreate: true);
        db.Credentials.Add(c);
        await db.SaveChangesAsync(ct);
        return ToResponse(c, 0);
    }

    public async Task<CredentialResponse?> UpdateAsync(int id, CredentialRequest r, CancellationToken ct = default)
    {
        var vpnType = Validate(r);
        var c = await db.Credentials.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return null;
        if (await db.Credentials.AnyAsync(x => x.Name == r.Name.Trim() && x.Id != id, ct))
            throw new ValidationException($"A credential named '{r.Name}' already exists.");

        Apply(c, r, vpnType, isCreate: false);
        await db.SaveChangesAsync(ct);
        return ToResponse(c, await CountUsagesAsync(id, ct));
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var c = await db.Credentials.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;

        var uses = await CountUsagesAsync(id, ct);
        if (uses > 0)
        {
            throw new ValidationException(
                $"Credential '{c.Name}' is in use by {uses} provider(s)/custom config(s). " +
                "Point them elsewhere first.");
        }

        db.Credentials.Remove(c);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Connections that would be affected by a change to this credential, whether it reaches them
    /// through a provider or a custom config. Secrets are baked into a container at creation, so a
    /// running connection keeps the old ones until it is redeployed — the caller uses this to say
    /// exactly which ones that is.
    /// </summary>
    public async Task<List<CredentialUsageResponse>> GetAffectedConnectionsAsync(
        int id, CancellationToken ct = default)
    {
        var connections = await db.Connections
            .AsNoTracking()
            .Include(c => c.Provider)
            .Include(c => c.CustomVpnConfig)
            .Where(c => (c.Provider != null && c.Provider.CredentialId == id)
                        || (c.CustomVpnConfig != null && c.CustomVpnConfig.CredentialId == id))
            .OrderBy(c => c.Identifier)
            .ToListAsync(ct);

        return connections.Select(c => new CredentialUsageResponse(
            c.Id,
            c.Identifier,
            c.Provider?.Name ?? c.CustomVpnConfig?.Name ?? "—",
            c.Status,
            Deployed: !string.IsNullOrEmpty(c.ContainerId),
            // Deploying a stopped connection would also start it, so only running ones are offered
            // for an automatic redeploy.
            Running: !string.IsNullOrEmpty(c.ContainerId) && c.Status == "running")).ToList();
    }

    private async Task<int> CountUsagesAsync(int id, CancellationToken ct)
        => await db.Providers.CountAsync(p => p.CredentialId == id, ct)
           + await db.CustomVpnConfigs.CountAsync(c => c.CredentialId == id, ct);

    private static VpnType Validate(CredentialRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name))
            throw new ValidationException("Credential name is required.");
        if (!Enum.TryParse<VpnType>(NormalizeVpnType(r.VpnType), true, out var vpnType))
            throw new ValidationException("VPN type must be 'openvpn' or 'wireguard'.");
        return vpnType;
    }

    private void Apply(Credential c, CredentialRequest r, VpnType vpnType, bool isCreate)
    {
        c.Name = r.Name.Trim();
        c.VpnType = vpnType;
        c.OpenVpnUserEnc = ApplySecret(r.OpenVpnUser, c.OpenVpnUserEnc, isCreate);
        c.OpenVpnPasswordEnc = ApplySecret(r.OpenVpnPassword, c.OpenVpnPasswordEnc, isCreate);
        c.WireGuardPrivateKeyEnc = ApplySecret(r.WireGuardPrivateKey, c.WireGuardPrivateKeyEnc, isCreate);
        c.WireGuardPresharedKeyEnc = ApplySecret(r.WireGuardPresharedKey, c.WireGuardPresharedKeyEnc, isCreate);
        c.WireGuardAddresses = Blank(r.WireGuardAddresses);
        c.Notes = Blank(r.Notes);
        c.UpdatedAt = DateTimeOffset.UtcNow;
    }

    // null => unchanged, "" => cleared, otherwise encrypt.
    private string? ApplySecret(string? incoming, string? existingEnc, bool isCreate)
    {
        if (incoming is null) return isCreate ? null : existingEnc;
        if (incoming.Length == 0) return null;
        return protector.Encrypt(incoming);
    }

    private static string NormalizeVpnType(string? v)
        => (v ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "wireguard" or "wg" => "WireGuard",
            "openvpn" or "ovpn" => "OpenVpn",
            _ => v ?? string.Empty,
        };

    private static string? Blank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private CredentialResponse ToResponse(Credential c, int usedBy) => new(
        c.Id,
        c.Name,
        c.VpnType.ToString().ToLowerInvariant(),
        protector.Decrypt(c.OpenVpnUserEnc),  // a username is not a key; shown so it is recognisable
        !string.IsNullOrEmpty(c.OpenVpnPasswordEnc),
        !string.IsNullOrEmpty(c.WireGuardPrivateKeyEnc),
        !string.IsNullOrEmpty(c.WireGuardPresharedKeyEnc),
        c.WireGuardAddresses,
        c.Notes,
        usedBy,
        c.CreatedAt,
        c.UpdatedAt);
}
