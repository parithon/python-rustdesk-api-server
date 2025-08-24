using System.ComponentModel.DataAnnotations;

namespace RustDeskApiServer.Models
{
    public class FileLog
    {
        public int Id { get; set; }

        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty;

        [StringLength(20)]
        public string RemoteId { get; set; } = "0";

        [StringLength(20)]
        public string UserId { get; set; } = "0";

        [StringLength(20)]
        public string UserIp { get; set; } = "0";

        [StringLength(500)]
        public string FileSize { get; set; } = string.Empty;

        public int Direction { get; set; } = 0;

        public DateTime? LoggedAt { get; set; }
    }
}