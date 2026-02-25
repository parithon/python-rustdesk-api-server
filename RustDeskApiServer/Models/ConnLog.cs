namespace RustDeskApiServer.Models;

public class ConnLog
{
    public int Id { get; set; }
    public string? Action { get; set; }
    public string? ConnId { get; set; }
    public string? FromIp { get; set; }
    public string? FromId { get; set; }
    public string? RustDeskId { get; set; }
    public DateTime? ConnStart { get; set; }
    public DateTime? ConnEnd { get; set; }
    public string? SessionId { get; set; }
    public string? Uuid { get; set; }
}
