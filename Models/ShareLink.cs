using System.ComponentModel.DataAnnotations;

namespace RustDeskApiServer.Models
{
    public class ShareLink
    {
        public int Id { get; set; }

        [StringLength(16)]
        public string UserId { get; set; } = string.Empty;

        [StringLength(60)]
        public string ShareHash { get; set; } = string.Empty;

        [StringLength(20)]
        public string PeerIds { get; set; } = string.Empty;

        public bool IsUsed { get; set; } = false;

        public bool IsExpired { get; set; } = false;

        public DateTime CreateTime { get; set; } = DateTime.UtcNow;
    }
}