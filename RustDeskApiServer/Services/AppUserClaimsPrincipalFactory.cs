using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RustDeskApiServer.Models;

namespace RustDeskApiServer.Services;

public class AppUserClaimsPrincipalFactory(
    UserManager<UserProfile> userManager,
    IOptions<IdentityOptions> optionsAccessor)
    : UserClaimsPrincipalFactory<UserProfile>(userManager, optionsAccessor)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(UserProfile user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim("IsAdmin", user.IsAdmin.ToString().ToLowerInvariant()));
        return identity;
    }
}
