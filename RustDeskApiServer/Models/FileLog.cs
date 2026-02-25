namespace RustDeskApiServer.Models;

public class FileLog
{
    public int Id { get; set; }
    public string File { get; set; } = string.Empty;
    public string RemoteId { get; set; } = "0";
    public string UserId { get; set; } = "0";
    public string UserIp { get; set; } = "0";
    public string FileSize { get; set; } = string.Empty;
    public int Direction { get; set; }
    public DateTime? LoggedAt { get; set; }
}
