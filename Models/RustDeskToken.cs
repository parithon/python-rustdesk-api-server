using System.ComponentModel.DataAnnotations;

namespace RustDeskApiServer.Models
{
    public class RustDeskToken
    {
        public int Id { get; set; }

        [StringLength(20)]
        public string Username { get; set; } = string.Empty;

        [StringLength(16)]
        public string RustDeskId { get; set; } = string.Empty;

        [StringLength(16)]
        public string UserId { get; set; } = string.Empty;

        [StringLength(60)]
        public string Uuid { get; set; } = string.Empty;

        [StringLength(60)]
        public string AccessToken { get; set; } = string.Empty;

        public DateTime CreateTime { get; set; } = DateTime.UtcNow;
    }
}