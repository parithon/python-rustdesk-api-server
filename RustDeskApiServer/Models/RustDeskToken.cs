namespace RustDeskApiServer.Models;

public class RustDeskToken
{
    public int Id { get; set; }
    public required string Username { get; init; }
    public required string RustDeskId { get; init; }
    public required int UserId { get; init; }
    public required string Uuid { get; init; }
    public string AccessToken { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
}
