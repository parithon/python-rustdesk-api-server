namespace RustDeskApiServer.Models;

public class RustDeskTag
{
    public int Id { get; set; }
    public required int UserId { get; init; }
    public required string TagName { get; init; }
    public string TagColor { get; set; } = string.Empty;
}
