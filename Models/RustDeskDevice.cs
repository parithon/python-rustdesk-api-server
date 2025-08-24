using System.ComponentModel.DataAnnotations;

namespace RustDeskApiServer.Models
{
    public class RustDeskDevice
    {
        public int Id { get; set; }

        [StringLength(60)]
        public string RustDeskId { get; set; } = string.Empty;

        [StringLength(100)]
        public string Cpu { get; set; } = string.Empty;

        [StringLength(100)]
        public string Hostname { get; set; } = string.Empty;

        [StringLength(100)]
        public string Memory { get; set; } = string.Empty;

        [StringLength(100)]
        public string Os { get; set; } = string.Empty;

        [StringLength(100)]
        public string Uuid { get; set; } = string.Empty;

        [StringLength(100)]
        public string Username { get; set; } = string.Empty;

        [StringLength(100)]
        public string Version { get; set; } = string.Empty;

        [StringLength(60)]
        public string IpAddress { get; set; } = string.Empty;

        public DateTime CreateTime { get; set; } = DateTime.UtcNow;

        public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
    }
}