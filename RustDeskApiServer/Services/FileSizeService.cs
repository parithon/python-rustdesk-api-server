namespace RustDeskApiServer.Services;

public interface IFileSizeService
{
    string FormatFileSize(long bytes);
}

public class FileSizeService : IFileSizeService
{
    private static readonly string[] SizeNames = ["B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"];

    public string FormatFileSize(long bytes)
    {
        if (bytes == 0) return "0B";

        var i = (int)Math.Floor(Math.Log(bytes, 1024));
        var p = Math.Pow(1024, i);
        var s = Math.Round(bytes / p, 2);
        return $"{s} {SizeNames[i]}";
    }
}
