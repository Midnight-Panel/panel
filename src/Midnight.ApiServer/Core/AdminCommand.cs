using Microsoft.EntityFrameworkCore;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Features.Users.Services;

namespace Midnight.ApiServer.Core;

/// <summary>
/// "dotnet Midnight.ApiServer.dll create-admin --email a@b.c [--username name]".
/// The password is read from the environment variable MIDNIGHT_ADMIN_PASSWORD so it never shows up in process lists.
/// </summary>
public static class AdminCommand
{
    public static async Task<int> RunAsync(WebApplication app, string[] args)
    {
        var email = GetOption(args, "--email");
        var username = GetOption(args, "--username");
        var password = Environment.GetEnvironmentVariable("MIDNIGHT_ADMIN_PASSWORD");

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            Console.Error.WriteLine("Usage: create-admin --email <email> [--username <name>] (password in MIDNIGHT_ADMIN_PASSWORD)");
            return 2;
        }

        if (password.Length < 8)
        {
            Console.Error.WriteLine("The password must be at least 8 characters long");
            return 2;
        }

        username ??= DeriveUsername(email);

        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserService>();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();

        try
        {
            var user = await context.Users.FirstOrDefaultAsync(x => x.Email == UserService.NormalizeEmail(email))
                       ?? await users.CreateAsync(username, email, password, [], CancellationToken.None);

            await users.AssignAdministratorRoleAsync(user, CancellationToken.None);

            Console.WriteLine($"Administrator '{user.Username}' ({user.Email}) is ready");
            return 0;
        }
        catch (HttpApiException e)
        {
            Console.Error.WriteLine($"Could not create the administrator: {e.Message}");
            return 1;
        }
    }

    /// <summary>"Max.Mustermann+test@example.com" becomes "max.mustermann-test"</summary>
    internal static string DeriveUsername(string email)
    {
        var local = email.Split('@')[0];
        var cleaned = new string(local.Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' ? char.ToLowerInvariant(c) : '-').ToArray()).Trim('-', '.');

        if (cleaned.Length < 3)
            cleaned = "admin";

        return cleaned.Length > 32 ? cleaned[..32] : cleaned;
    }

    private static string? GetOption(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
