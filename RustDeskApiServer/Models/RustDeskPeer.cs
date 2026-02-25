namespace RustDeskApiServer.Models;

public class RustDeskPeer
{
    public int Id { get; set; }
    public required int UserId { get; set; }
    public required string RustDeskId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public string RHash { get; set; } = string.Empty;
}
