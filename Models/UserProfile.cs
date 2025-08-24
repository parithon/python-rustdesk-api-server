using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace RustDeskApiServer.Models
{
    public class UserProfile : IdentityUser
    {
        [StringLength(16)]
        public string? RustDeskId { get; set; }

        [StringLength(60)]
        public string? Uuid { get; set; }

        public bool AutoLogin { get; set; } = true;

        [StringLength(20)]
        public string? RType { get; set; }

        public string? DeviceInfo { get; set; }

        public bool IsAdmin { get; set; } = false;

        public DateTime? LastLogin { get; set; }
    }
}