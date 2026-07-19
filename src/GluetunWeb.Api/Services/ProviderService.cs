using GluetunWeb.Api.Crypto;
using GluetunWeb.Api.Data;
using GluetunWeb.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GluetunWeb.Api.Services;

public class ProviderService(AppDbContext db, ISecretProtector protector)
{
    /// <summary>Includes the shared credential so responses can name it.</summary>
    private IQueryable<Provider> LoadQuery() => db.Providers.AsNoTracking().Include(p => p.Credential);

    public async Task<List<ProviderResponse>> ListAsync(CancellationToken ct = default)
    {
        var providers = await LoadQuery().OrderBy(p => p.Name).ToListAsync(ct);
        return providers.Select(ToResponse).ToList();
    }

    public async Task<ProviderResponse?> GetAsync(int id, CancellationToken ct = default)
    {
        var p = await LoadQuery().FirstOrDefaultAsync(x => x.Id == id, ct);
        return p is null ? null : ToResponse(p);
    }

    public async Task<ProviderResponse> CreateAsync(ProviderRequest r, CancellationToken ct = default)
    {
        Validate(r);
        await ValidateCredentialAsync(r, ct);
        if (await db.Providers.AnyAsync(p => p.Name == r.Name.Trim(), ct))
            throw new ValidationException($"A provider named '{r.Name}' already exists.");

        var p = new Provider();
        Apply(p, r, isCreate: true);
        db.Providers.Add(p);
        await db.SaveChangesAsync(ct);
        return ToResponse(await LoadQuery().FirstAsync(x => x.Id == p.Id, ct));
    }

    public async Task<ProviderResponse?> UpdateAsync(int id, ProviderRequest r, CancellationToken ct = default)
    {
        Validate(r);
        await ValidateCredentialAsync(r, ct);
        var p = await db.Providers.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return null;
        if (await db.Providers.AnyAsync(x => x.Name == r.Name.Trim() && x.Id != id, ct))
            throw new ValidationException($"A provider named '{r.Name}' already exists.");

        Apply(p, r, isCreate: false);
        await db.SaveChangesAsync(ct);
        return ToResponse(await LoadQuery().FirstAsync(x => x.Id == id, ct));
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var p = await db.Providers.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return false;
        if (await db.Connections.AnyAsync(c => c.ProviderId == id, ct))
            throw new ValidationException("Provider is in use by one or more connections.");
        db.Providers.Remove(p);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static void Validate(ProviderRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name))
            throw new ValidationException("Provider name is required.");
        if (string.IsNullOrWhiteSpace(r.ProviderType))
            throw new ValidationException("Provider type (VPN_SERVICE_PROVIDER) is required.");
        if (!Enum.TryParse<VpnType>(NormalizeVpnType(r.VpnType), true, out _))
            throw new ValidationException("VPN type must be 'openvpn' or 'wireguard'.");
        if (!string.IsNullOrWhiteSpace(r.OpenVpnProtocol)
            && !Enum.TryParse<OpenVpnProtocol>(r.OpenVpnProtocol.Trim(), true, out _))
            throw new ValidationException("OpenVPN protocol must be 'udp' or 'tcp'.");
    }

    /// <summary>
    /// A credential must exist and match the provider's VPN type — an OpenVPN provider given a
    /// WireGuard credential would deploy with no usable secrets and fail only at connect time.
    /// </summary>
    private async Task ValidateCredentialAsync(ProviderRequest r, CancellationToken ct)
    {
        if (r.CredentialId is null) return;

        var cred = await db.Credentials.AsNoTracking().FirstOrDefaultAsync(c => c.Id == r.CredentialId, ct)
                   ?? throw new ValidationException("The selected credential no longer exists.");

        var vpnType = Enum.Parse<VpnType>(NormalizeVpnType(r.VpnType), true);
        if (cred.VpnType != vpnType)
        {
            throw new ValidationException(
                $"Credential '{cred.Name}' holds {cred.VpnType.ToString().ToLowerInvariant()} secrets, " +
                $"but this provider uses {vpnType.ToString().ToLowerInvariant()}.");
        }
    }

    private void Apply(Provider p, ProviderRequest r, bool isCreate)
    {
        p.Name = r.Name.Trim();
        p.ProviderType = r.ProviderType.Trim();
        p.VpnType = Enum.Parse<VpnType>(NormalizeVpnType(r.VpnType), true);
        // WireGuard is UDP-only; keep the stored value at the default rather than a misleading "tcp".
        p.OpenVpnProtocol = p.VpnType == VpnType.OpenVpn && !string.IsNullOrWhiteSpace(r.OpenVpnProtocol)
            ? Enum.Parse<OpenVpnProtocol>(r.OpenVpnProtocol.Trim(), true)
            : OpenVpnProtocol.Udp;
        p.CredentialId = r.CredentialId;
        p.OpenVpnUserEnc = ApplySecret(r.OpenVpnUser, p.OpenVpnUserEnc, isCreate);
        p.OpenVpnPasswordEnc = ApplySecret(r.OpenVpnPassword, p.OpenVpnPasswordEnc, isCreate);
        p.WireGuardPrivateKeyEnc = ApplySecret(r.WireGuardPrivateKey, p.WireGuardPrivateKeyEnc, isCreate);
        p.WireGuardPresharedKeyEnc = ApplySecret(r.WireGuardPresharedKey, p.WireGuardPresharedKeyEnc, isCreate);
        p.WireGuardAddresses = Blank(r.WireGuardAddresses);
        p.ServerCountries = Blank(r.ServerCountries);
        p.ServerCities = Blank(r.ServerCities);
        p.ServerRegions = Blank(r.ServerRegions);
        p.ServerHostnames = Blank(r.ServerHostnames);
        p.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private string? ApplySecret(string? incoming, string? existingEnc, bool isCreate)
    {
        if (incoming is null) return isCreate ? null : existingEnc; // unchanged
        if (incoming.Length == 0) return null;                       // cleared
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

    private ProviderResponse ToResponse(Provider p) => new(
        p.Id,
        p.Name,
        p.ProviderType,
        p.VpnType.ToString().ToLowerInvariant(),
        p.OpenVpnProtocol.ToString().ToLowerInvariant(),
        p.CredentialId,
        p.Credential?.Name,
        protector.Decrypt(p.OpenVpnUserEnc),   // username is not a key; safe to display for editing
        !string.IsNullOrEmpty(p.OpenVpnPasswordEnc),
        !string.IsNullOrEmpty(p.WireGuardPrivateKeyEnc),
        !string.IsNullOrEmpty(p.WireGuardPresharedKeyEnc),
        p.WireGuardAddresses,
        p.ServerCountries,
        p.ServerCities,
        p.ServerRegions,
        p.ServerHostnames,
        p.CreatedAt,
        p.UpdatedAt);
}
