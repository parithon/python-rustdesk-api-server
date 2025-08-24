using System.ComponentModel.DataAnnotations;

namespace RustDeskApiServer.Models
{
    public class RustDeskTag
    {
        public int Id { get; set; }

        [StringLength(16)]
        public string UserId { get; set; } = string.Empty;

        [StringLength(60)]
        public string TagName { get; set; } = string.Empty;

        [StringLength(60)]
        public string TagColor { get; set; } = string.Empty;
    }
}