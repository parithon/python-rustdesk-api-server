using System.ComponentModel.DataAnnotations;

namespace RustDeskApiServer.Models
{
    public class RustDeskPeer
    {
        public int Id { get; set; }

        [StringLength(16)]
        public string UserId { get; set; } = string.Empty;

        [StringLength(60)]
        public string RustDeskId { get; set; } = string.Empty;

        [StringLength(20)]
        public string Username { get; set; } = string.Empty;

        [StringLength(30)]
        public string Hostname { get; set; } = string.Empty;

        [StringLength(30)]
        public string Alias { get; set; } = string.Empty;

        [StringLength(30)]
        public string Platform { get; set; } = string.Empty;

        [StringLength(30)]
        public string Tags { get; set; } = string.Empty;

        [StringLength(60)]
        public string ConnectionPassword { get; set; } = string.Empty;
    }
}