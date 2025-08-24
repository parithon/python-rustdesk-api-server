using System.ComponentModel.DataAnnotations;

namespace RustDeskApiServer.Models
{
    public class ConnectionLog
    {
        public int Id { get; set; }

        [StringLength(20)]
        public string? Action { get; set; }

        [StringLength(10)]
        public string? ConnectionId { get; set; }

        [StringLength(30)]
        public string? FromIp { get; set; }

        [StringLength(20)]
        public string? FromId { get; set; }

        [StringLength(20)]
        public string? ToId { get; set; }

        public DateTime? ConnectionStart { get; set; }

        public DateTime? ConnectionEnd { get; set; }

        [StringLength(60)]
        public string? SessionId { get; set; }

        [StringLength(60)]
        public string? Uuid { get; set; }
    }
}