namespace RustDeskApiServer.Models;

public class RustDeskDevice
{
    public int Id { get; set; }
    public string RustDeskId { get; set; } = string.Empty;
    public string Cpu { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string Memory { get; set; } = string.Empty;
    public string Os { get; set; } = string.Empty;
    public string Uuid { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}
