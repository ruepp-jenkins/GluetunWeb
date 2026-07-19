using GluetunWeb.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GluetunWeb.Api.Auth;

/// <summary>
/// Single-admin authentication. Passwords are hashed with ASP.NET Core Identity's
/// <see cref="PasswordHasher{TUser}"/> (PBKDF2 + per-password salt). Plaintext passwords are never
/// stored and never returned to the frontend.
/// </summary>
public class AuthService(AppDbContext db)
{
    private readonly PasswordHasher<AdminUser> _hasher = new();

    public Task<bool> AdminExistsAsync(CancellationToken ct = default)
        => db.AdminUsers.AnyAsync(ct);

    /// <summary>Creates the admin account on first run. Fails if one already exists.</summary>
    public async Task<(bool ok, string? error)> SetupAsync(string username, string password, CancellationToken ct = default)
    {
        if (await AdminExistsAsync(ct))
            return (false, "An administrator account already exists.");

        var err = ValidatePassword(password);
        if (err is not null)
            return (false, err);

        var user = new AdminUser { Username = username.Trim() };
        user.PasswordHash = _hasher.HashPassword(user, password);
        db.AdminUsers.Add(user);
        await db.SaveChangesAsync(ct);
        return (true, null);
    }

    /// <summary>Validates credentials; transparently upgrades the hash if the algorithm changed.</summary>
    public async Task<AdminUser?> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default)
    {
        var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Username == username, ct);
        if (user is null)
            return null;

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
            return null;

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, password);
            await db.SaveChangesAsync(ct);
        }
        return user;
    }

    public async Task<(bool ok, string? error)> ChangePasswordAsync(string username, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await ValidateCredentialsAsync(username, currentPassword, ct);
        if (user is null)
            return (false, "Current password is incorrect.");

        var err = ValidatePassword(newPassword);
        if (err is not null)
            return (false, err);

        user.PasswordHash = _hasher.HashPassword(user, newPassword);
        await db.SaveChangesAsync(ct);
        return (true, null);
    }

    public static string? ValidatePassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8)
            return "Password must be at least 8 characters.";
        return null;
    }
}
