using Microsoft.AspNetCore.Identity;

namespace RustDeskApiServer.Models;

public class UserProfile : IdentityUser<int>
{
    public string RustDeskId { get; set; } = string.Empty;
    public string Uuid { get; set; } = string.Empty;
    public bool AutoLogin { get; set; } = true;
    public string RType { get; set; } = string.Empty;
    public string DeviceInfo { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
}
