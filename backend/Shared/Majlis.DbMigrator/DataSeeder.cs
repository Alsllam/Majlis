using Majlis.Identity.Domain.Constants;
using Majlis.Identity.Domain.Entities;
using Majlis.Identity.EntityFrameworkCore;
using Majlis.Rooms.Domain.Entities;
using Majlis.Rooms.Domain.Enums;
using Majlis.Rooms.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Majlis.DbMigrator;

/// <summary>Idempotent: every step checks before inserting.</summary>
public sealed partial class DataSeeder(
    MajlisIdentityDbContext identityDb,
    RoomsDbContext roomsDb,
    UserManager<MajlisUser> users,
    RoleManager<MajlisRole> roles,
    IOpenIddictApplicationManager applications,
    IOpenIddictScopeManager scopes,
    IOptions<SeedOptions> options,
    ILogger<DataSeeder> logger)
{
    private SeedOptions Seed => options.Value;

    public async Task SeedAsync()
    {
        await SeedRolesAsync();
        await SeedTenantAsync();
        var seeded = await SeedUsersAsync();
        await SeedScopesAsync();
        await SeedClientsAsync();
        await SeedDemoRoomAsync(seeded);
    }

    private async Task SeedRolesAsync()
    {
        foreach (var role in MajlisRoles.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new MajlisRole(role));
            }
        }
    }

    private async Task SeedTenantAsync()
    {
        if (!await identityDb.Tenants.AnyAsync(t => t.Id == Seed.TenantId))
        {
            identityDb.Tenants.Add(new Tenant(Seed.TenantId, Seed.TenantNameAr, Seed.TenantNameEn, Seed.DataRegion));
            await identityDb.SaveChangesAsync();
        }
    }

    private async Task<List<MajlisUser>> SeedUsersAsync()
    {
        var result = new List<MajlisUser>();
        if (string.IsNullOrEmpty(Seed.Password))
        {
            Log.UsersSkipped(logger);
            return result;
        }

        foreach (var u in Seed.Users)
        {
            var user = await users.FindByEmailAsync(u.Email);
            if (user is null)
            {
                user = new MajlisUser
                {
                    Id = Guid.NewGuid(),
                    UserName = u.Email,
                    Email = u.Email,
                    EmailConfirmed = true,
                    DisplayName = u.DisplayName,
                    PreferredLanguage = u.Language,
                    TenantId = Seed.TenantId,
                };
                var created = await users.CreateAsync(user, Seed.Password);
                if (!created.Succeeded)
                {
                    throw new InvalidOperationException($"Could not create {u.Email}: {string.Join("; ", created.Errors.Select(e => e.Description))}");
                }
            }

            foreach (var role in u.Roles.Where(r => MajlisRoles.All.Contains(r)))
            {
                if (!await users.IsInRoleAsync(user, role))
                {
                    await users.AddToRoleAsync(user, role);
                }
            }

            result.Add(user);
        }

        return result;
    }

    private async Task SeedScopesAsync()
    {
        foreach (var (name, resources) in new[]
                 {
                     (MajlisScopes.Api, new[] { MajlisScopes.Api }),
                     (MajlisScopes.AiApi, new[] { MajlisScopes.AiApi }),
                     (MajlisScopes.Internal, new[] { MajlisScopes.Api, MajlisScopes.AiApi }),
                 })
        {
            if (await scopes.FindByNameAsync(name) is null)
            {
                var descriptor = new OpenIddictScopeDescriptor { Name = name };
                descriptor.Resources.UnionWith(resources);
                await scopes.CreateAsync(descriptor);
            }
        }
    }

    private async Task SeedClientsAsync()
    {
        await UpsertAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = MajlisClients.Web,
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Implicit,
            DisplayName = "Majlis web",
            Permissions =
            {
                Permissions.Endpoints.Authorization, Permissions.Endpoints.Token, Permissions.Endpoints.EndSession,
                Permissions.GrantTypes.AuthorizationCode, Permissions.GrantTypes.RefreshToken, Permissions.ResponseTypes.Code,
                Permissions.Scopes.Email, Permissions.Scopes.Profile, Permissions.Scopes.Roles,
                Permissions.Prefixes.Scope + Scopes.OfflineAccess,
                Permissions.Prefixes.Scope + MajlisScopes.Api, Permissions.Prefixes.Scope + MajlisScopes.AiApi,
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        }, Seed.WebRedirectUris);

        if (!string.IsNullOrEmpty(Seed.RealtimeClientSecret))
        {
            await UpsertAsync(ServiceClient(MajlisClients.Realtime, "Majlis realtime host", Seed.RealtimeClientSecret), []);
        }

        if (!string.IsNullOrEmpty(Seed.AiServiceClientSecret))
        {
            await UpsertAsync(ServiceClient(MajlisClients.AiService, "Majlis ai-service", Seed.AiServiceClientSecret), []);
        }
    }

    private static OpenIddictApplicationDescriptor ServiceClient(string clientId, string displayName, string secret) => new()
    {
        ClientId = clientId,
        ClientSecret = secret,
        ClientType = ClientTypes.Confidential,
        DisplayName = displayName,
        Permissions =
        {
            Permissions.Endpoints.Token,
            Permissions.GrantTypes.ClientCredentials,
            Permissions.Prefixes.Scope + MajlisScopes.Internal,
        },
    };

    private async Task UpsertAsync(OpenIddictApplicationDescriptor descriptor, IEnumerable<string> redirectUris)
    {
        foreach (var uri in redirectUris)
        {
            descriptor.RedirectUris.Add(new Uri(uri));
            descriptor.PostLogoutRedirectUris.Add(new Uri(uri));
        }

        var existing = await applications.FindByClientIdAsync(descriptor.ClientId!);
        if (existing is null)
        {
            await applications.CreateAsync(descriptor);
        }
        else
        {
            await applications.UpdateAsync(existing, descriptor);
        }
    }

    private async Task SeedDemoRoomAsync(List<MajlisUser> seededUsers)
    {
        if (seededUsers.Count == 0 || await roomsDb.Rooms.IgnoreQueryFilters().AnyAsync(r => r.TenantId == Seed.TenantId))
        {
            return;
        }

        var room = new Room(Guid.NewGuid(), Seed.TenantId, Seed.DemoWorkspaceId, "غرفة العقود", "مراجعة عقود الموردين مع الوكيل", RoomVisibility.Private);
        foreach (var user in seededUsers)
        {
            room.AddParticipant(user.Id, user.DisplayName, ParticipantRole.Contributor);
        }

        roomsDb.Rooms.Add(room);
        await roomsDb.SaveChangesAsync();
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 9001, Level = LogLevel.Warning, Message = "Seed:Password is not set; demo users and the demo room were not created")]
        public static partial void UsersSkipped(ILogger logger);
    }
}
