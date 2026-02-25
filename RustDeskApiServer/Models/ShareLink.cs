namespace RustDeskApiServer.Models;

public class ShareLink
{
    public int Id { get; set; }
    public required int UserId { get; init; }
    public required string SHash { get; init; }
    public string Peers { get; set; } = string.Empty;
    public bool IsUsed { get; set; }
    public bool IsExpired { get; set; }
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
}
