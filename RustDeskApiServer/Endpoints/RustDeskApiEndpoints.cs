using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RustDeskApiServer.Data;
using RustDeskApiServer.Models;
using RustDeskApiServer.Services;
using System.Text.Json;

namespace RustDeskApiServer.Endpoints;

public static class RustDeskApiEndpoints
{
    private const int EffectiveSeconds = 7200;

    public static RouteGroupBuilder MapRustDeskApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("RustDesk API");

        group.MapPost("/login", Login);
        group.MapPost("/logout", Logout);
        group.MapGet("/ab", GetAddressBook);
        group.MapPost("/ab", PostAddressBook);
        group.MapPost("/ab/get", GetAddressBook); // sciter compatibility
        group.MapGet("/users",  () => Results.Json(new { code = 1, data = "ok" }));
        group.MapPost("/users", () => Results.Json(new { code = 1, data = "ok" }));
        group.MapGet("/peers",  () => Results.Json(new { code = 1, data = "ok" }));
        group.MapPost("/peers", () => Results.Json(new { code = 1, data = "ok" }));
        group.MapPost("/currentUser", CurrentUser);
        group.MapPost("/sysinfo",   SysInfo);
        group.MapPost("/heartbeat", Heartbeat);
        group.MapPost("/audit",     Audit);
        group.MapGet("/down_peers", DownPeers).RequireAuthorization();

        return group;
    }

    // GET /api/down_peers — admin-only XLSX export
    private static async Task<IResult> DownPeers(AppDbContext db, UserManager<UserProfile> userManager, HttpContext ctx)
    {
        var user = await userManager.GetUserAsync(ctx.User);
        if (user is null || !user.IsAdmin) return Results.Forbid();

        var devices  = await db.Devices.ToListAsync();
        var peers    = await db.Peers.ToDictionaryAsync(p => p.RustDeskId);
        var userIds  = peers.Values.Select(p => p.UserId).Distinct().ToList();
        var userMap  = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.UserName ?? "");

        var now = DateTime.UtcNow;

        using var wb    = new XLWorkbook();
        var ws = wb.Worksheets.Add("Device Info");
        string[] headers = ["RustDesk ID", "Owner", "Version", "System User", "Hostname",
                             "OS", "CPU", "Memory", "IP Address", "Registered", "Updated", "Status"];

        for (var j = 0; j < headers.Length; j++)
            ws.Cell(1, j + 1).Value = headers[j];

        var row = 2;
        foreach (var dev in devices)
        {
            peers.TryGetValue(dev.RustDeskId, out var peer);
            var owner  = peer is not null && userMap.TryGetValue(peer.UserId, out var un) ? un : "Not Logged In";
            var status = (now - dev.UpdateTime).TotalSeconds <= 120 ? "Online" : "Offline";
            ws.Cell(row, 1).Value  = dev.RustDeskId;
            ws.Cell(row, 2).Value  = owner;
            ws.Cell(row, 3).Value  = dev.Version;
            ws.Cell(row, 4).Value  = dev.Username;
            ws.Cell(row, 5).Value  = dev.Hostname;
            ws.Cell(row, 6).Value  = dev.Os;
            ws.Cell(row, 7).Value  = dev.Cpu;
            ws.Cell(row, 8).Value  = dev.Memory;
            ws.Cell(row, 9).Value  = dev.IpAddress;
            ws.Cell(row, 10).Value = dev.CreateTime.ToLocalTime().ToString("yyyy-MM-dd");
            ws.Cell(row, 11).Value = dev.UpdateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            ws.Cell(row, 12).Value = status;
            row++;
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return Results.File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "DeviceInfo.xlsx");
    }

    private static string GetClientIp(HttpContext ctx)
    {
        var forwarded = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        return forwarded?.Split(',')[0].Trim()
               ?? ctx.Connection.RemoteIpAddress?.ToString()
               ?? string.Empty;
    }

    private static string BearerToken(HttpContext ctx)
    {
        var header = ctx.Request.Headers.Authorization.FirstOrDefault() ?? "";
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header["Bearer ".Length..]
            : header;
    }

    private static async Task<IResult> Login(
        HttpContext ctx,
        AppDbContext db,
        UserManager<UserProfile> userManager,
        ITokenService tokenService)
    {
        using var reader = new StreamReader(ctx.Request.Body);
        var body = JsonSerializer.Deserialize<JsonElement>(await reader.ReadToEndAsync());

        var username = body.TryGetProperty("username", out var u) ? u.GetString() ?? "" : "";
        var password = body.TryGetProperty("password", out var p) ? p.GetString() ?? "" : "";
        var rid      = body.TryGetProperty("id",       out var r) ? r.GetString() ?? "" : "";
        var uuid     = body.TryGetProperty("uuid",     out var uid) ? uid.GetString() ?? "" : "";
        var autoLogin = body.TryGetProperty("autoLogin", out var al) && al.GetBoolean();
        var rtype     = body.TryGetProperty("type",   out var rt) ? rt.GetString() ?? "" : "";
        var deviceInfo = body.TryGetProperty("deviceInfo", out var di) ? di.ToString() : "";

        var user = await userManager.FindByNameAsync(username);
        if (user is null || !await userManager.CheckPasswordAsync(user, password))
            return Results.Json(new { error = "Invalid username or password. Please try again." });

        user.RustDeskId = rid;
        user.Uuid = uuid;
        user.AutoLogin = autoLogin;
        user.RType = rtype;
        user.DeviceInfo = deviceInfo;
        await db.SaveChangesAsync();

        // Bind device if no peer exists for this rid
        if (!await db.Peers.AnyAsync(peer => peer.RustDeskId == rid))
        {
            var device = await db.Devices.FirstOrDefaultAsync(d => d.Uuid == uuid);
            if (device is not null)
            {
                db.Peers.Add(new RustDeskPeer
                {
                    UserId = user.Id,
                    RustDeskId = device.RustDeskId,
                    Hostname = device.Hostname,
                    Username = device.Username
                });
                await db.SaveChangesAsync();
            }
        }

        var token = await db.Tokens.FirstOrDefaultAsync(t =>
            t.UserId == user.Id && t.Username == user.UserName && t.RustDeskId == rid);

        if (token is not null && (DateTime.UtcNow - token.CreateTime).TotalSeconds >= EffectiveSeconds)
        {
            db.Tokens.Remove(token);
            await db.SaveChangesAsync();
            token = null;
        }

        if (token is null)
        {
            token = new RustDeskToken
            {
                Username     = user.UserName ?? username,
                UserId       = user.Id,
                Uuid         = uuid,
                RustDeskId   = rid,
                AccessToken  = tokenService.GenerateToken(DateTime.UtcNow.Ticks.ToString()),
                CreateTime   = DateTime.UtcNow
            };
            db.Tokens.Add(token);
            await db.SaveChangesAsync();
        }

        return Results.Json(new
        {
            access_token = token.AccessToken,
            type         = "access_token",
            user         = new { name = user.UserName }
        });
    }

    private static async Task<IResult> Logout(HttpContext ctx, AppDbContext db)
    {
        using var reader = new StreamReader(ctx.Request.Body);
        var body = JsonSerializer.Deserialize<JsonElement>(await reader.ReadToEndAsync());

        var rid  = body.TryGetProperty("id",   out var r) ? r.GetString() ?? "" : "";
        var uuid = body.TryGetProperty("uuid", out var u) ? u.GetString() ?? "" : "";

        var user = await db.Users.FirstOrDefaultAsync(u2 => u2.RustDeskId == rid && u2.Uuid == uuid);
        if (user is null) return Results.Json(new { error = "Invalid request." });

        var token = await db.Tokens.FirstOrDefaultAsync(t => t.UserId == user.Id && t.RustDeskId == rid);
        if (token is not null)
        {
            db.Tokens.Remove(token);
            await db.SaveChangesAsync();
        }
        return Results.Json(new { code = 1 });
    }

    private static async Task<IResult> CurrentUser(HttpContext ctx, AppDbContext db)
    {
        var accessToken = BearerToken(ctx);
        var token = await db.Tokens.FirstOrDefaultAsync(t => t.AccessToken == accessToken);
        if (token is null) return Results.Json(new { });

        var user = await db.Users.FindAsync(token.UserId);
        if (user is null) return Results.Json(new { });

        return Results.Json(new
        {
            access_token = token.AccessToken,
            type         = "access_token",
            name         = user.UserName
        });
    }

    private static async Task<IResult> GetAddressBook(HttpContext ctx, AppDbContext db)
    {
        var token = await db.Tokens.FirstOrDefaultAsync(t => t.AccessToken == BearerToken(ctx));
        if (token is null) return Results.Json(new { error = "Failed to retrieve address book." });

        var tags = await db.Tags.Where(t => t.UserId == token.UserId).ToListAsync();
        var tagNames  = tags.Select(t => t.TagName).ToArray();
        var tagColors = tags
            .Where(t => !string.IsNullOrEmpty(t.TagColor))
            .ToDictionary(t => t.TagName, t => t.TagColor);

        var peers = await db.Peers.Where(p => p.UserId == token.UserId).ToListAsync();
        var peersResult = peers.Select(p => new
        {
            id       = p.RustDeskId,
            username = p.Username,
            hostname = p.Hostname,
            alias    = p.Alias,
            platform = p.Platform,
            tags     = p.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries),
            hash     = p.RHash
        }).ToArray();

        var data = new
        {
            tags       = tagNames,
            peers      = peersResult,
            tag_colors = JsonSerializer.Serialize(tagColors)
        };

        return Results.Json(new
        {
            updated_at = DateTime.UtcNow,
            data       = JsonSerializer.Serialize(data)
        });
    }

    private static async Task<IResult> PostAddressBook(HttpContext ctx, AppDbContext db)
    {
        var token = await db.Tokens.FirstOrDefaultAsync(t => t.AccessToken == BearerToken(ctx));
        if (token is null) return Results.Json(new { error = "Failed to retrieve address book." });

        using var reader = new StreamReader(ctx.Request.Body);
        var postData = JsonSerializer.Deserialize<JsonElement>(await reader.ReadToEndAsync());

        var dataStr = postData.TryGetProperty("data", out var d) ? d.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(dataStr))
            return Results.Json(new { code = 102, data = "Failed to update address book." });

        var data       = JsonSerializer.Deserialize<JsonElement>(dataStr);
        var tagNames   = data.TryGetProperty("tags", out var tn)
            ? tn.EnumerateArray().Select(x => x.GetString() ?? "").ToArray()
            : [];
        var tagColorsStr = data.TryGetProperty("tag_colors", out var tc) ? tc.GetString() ?? "" : "";
        var tagColors  = string.IsNullOrEmpty(tagColorsStr)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(tagColorsStr) ?? [];
        var peers      = data.TryGetProperty("peers", out var pe)
            ? pe.EnumerateArray().ToArray()
            : [];

        if (tagNames.Length > 0)
        {
            db.Tags.RemoveRange(db.Tags.Where(t => t.UserId == token.UserId));
            db.Tags.AddRange(tagNames.Select(name => new RustDeskTag
            {
                UserId   = token.UserId,
                TagName  = name,
                TagColor = tagColors.TryGetValue(name, out var color) ? color : ""
            }));
            await db.SaveChangesAsync();
        }

        if (peers.Length > 0)
        {
            db.Peers.RemoveRange(db.Peers.Where(p => p.UserId == token.UserId));
            db.Peers.AddRange(peers.Select(p => new RustDeskPeer
            {
                UserId    = token.UserId,
                RustDeskId = p.TryGetProperty("id",       out var id) ? id.GetString() ?? "" : "",
                Username  = p.TryGetProperty("username",  out var un) ? un.GetString() ?? "" : "",
                Hostname  = p.TryGetProperty("hostname",  out var hn) ? hn.GetString() ?? "" : "",
                Alias     = p.TryGetProperty("alias",     out var al) ? al.GetString() ?? "" : "",
                Platform  = p.TryGetProperty("platform",  out var pl) ? pl.GetString() ?? "" : "",
                Tags      = p.TryGetProperty("tags",      out var tgs)
                    ? string.Join(",", tgs.EnumerateArray().Select(x => x.GetString() ?? ""))
                    : "",
                RHash     = p.TryGetProperty("hash",      out var h)  ? h.GetString()  ?? "" : ""
            }));
            await db.SaveChangesAsync();
        }

        return Results.Json(new { code = 102, data = "Failed to update address book." });
    }

    private static async Task<IResult> SysInfo(HttpContext ctx, AppDbContext db)
    {
        var clientIp = GetClientIp(ctx);
        using var reader = new StreamReader(ctx.Request.Body);
        var body = JsonSerializer.Deserialize<JsonElement>(await reader.ReadToEndAsync());

        var rid  = body.TryGetProperty("id",   out var r) ? r.GetString() ?? "" : "";
        var uuid = body.TryGetProperty("uuid", out var u) ? u.GetString() ?? "" : "";

        var device = await db.Devices.FirstOrDefaultAsync(d => d.RustDeskId == rid && d.Uuid == uuid);
        if (device is null)
        {
            db.Devices.Add(new RustDeskDevice
            {
                RustDeskId = rid,
                Cpu        = body.TryGetProperty("cpu",      out var cpu)  ? cpu.GetString()  ?? "" : "",
                Hostname   = body.TryGetProperty("hostname", out var hn)   ? hn.GetString()   ?? "" : "",
                Memory     = body.TryGetProperty("memory",   out var mem)  ? mem.GetString()  ?? "" : "",
                Os         = body.TryGetProperty("os",       out var os)   ? os.GetString()   ?? "" : "",
                Username   = body.TryGetProperty("username", out var un)   ? un.GetString()   ?? "-" : "-",
                Uuid       = uuid,
                Version    = body.TryGetProperty("version",  out var ver)  ? ver.GetString()  ?? "" : "",
                IpAddress  = clientIp,
                CreateTime = DateTime.UtcNow,
                UpdateTime = DateTime.UtcNow
            });
        }
        else
        {
            if (body.TryGetProperty("cpu",      out var cpu))  device.Cpu      = cpu.GetString()  ?? device.Cpu;
            if (body.TryGetProperty("hostname", out var hn))   device.Hostname = hn.GetString()   ?? device.Hostname;
            if (body.TryGetProperty("memory",   out var mem))  device.Memory   = mem.GetString()  ?? device.Memory;
            if (body.TryGetProperty("os",       out var os))   device.Os       = os.GetString()   ?? device.Os;
            if (body.TryGetProperty("username", out var un))   device.Username = un.GetString()   ?? device.Username;
            if (body.TryGetProperty("version",  out var ver))  device.Version  = ver.GetString()  ?? device.Version;
            device.IpAddress  = clientIp;
            device.UpdateTime = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return Results.Json(new { data = "ok" });
    }

    private static async Task<IResult> Heartbeat(HttpContext ctx, AppDbContext db)
    {
        using var reader = new StreamReader(ctx.Request.Body);
        var body = JsonSerializer.Deserialize<JsonElement>(await reader.ReadToEndAsync());

        var rid  = body.TryGetProperty("id",   out var r) ? r.GetString() ?? "" : "";
        var uuid = body.TryGetProperty("uuid", out var u) ? u.GetString() ?? "" : "";

        var device = await db.Devices.FirstOrDefaultAsync(d => d.RustDeskId == rid && d.Uuid == uuid);
        if (device is not null)
        {
            device.IpAddress  = GetClientIp(ctx);
            device.UpdateTime = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var newExpiry = DateTime.UtcNow.AddSeconds(EffectiveSeconds);
        await db.Tokens
            .Where(t => t.RustDeskId == rid && t.Uuid == uuid)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.CreateTime, newExpiry));

        return Results.Json(new { data = "online" });
    }

    private static async Task<IResult> Audit(
        HttpContext ctx,
        AppDbContext db,
        IFileSizeService fileSizeService)
    {
        using var reader = new StreamReader(ctx.Request.Body);
        var body = JsonSerializer.Deserialize<JsonElement>(await reader.ReadToEndAsync());

        var auditType = body.TryGetProperty("action", out var act) ? act.GetString() ?? "" : "";

        switch (auditType)
        {
            case "new":
            {
                db.ConnLogs.Add(new ConnLog
                {
                    Action    = auditType,
                    ConnId    = body.TryGetProperty("conn_id",    out var ci)  ? ci.ToString()              : null,
                    FromIp    = body.TryGetProperty("ip",         out var ip)  ? ip.GetString()             : null,
                    FromId    = string.Empty,
                    RustDeskId = body.TryGetProperty("id",        out var id)  ? id.GetString()             : null,
                    ConnStart = DateTime.UtcNow,
                    SessionId = body.TryGetProperty("session_id", out var si)  ? si.ToString()              : null,
                    Uuid      = body.TryGetProperty("uuid",       out var uid) ? uid.GetString()            : null
                });
                await db.SaveChangesAsync();
                break;
            }

            case "close":
            {
                var connId = body.TryGetProperty("conn_id", out var ci) ? ci.ToString() : null;
                await db.ConnLogs
                    .Where(l => l.ConnId == connId)
                    .ExecuteUpdateAsync(s => s.SetProperty(l => l.ConnEnd, DateTime.UtcNow));
                break;
            }

            default:
            {
                if (body.TryGetProperty("is_file", out _))
                {
                    var infoStr  = body.TryGetProperty("info", out var info) ? info.GetString() ?? "{}" : "{}";
                    var infoObj  = JsonSerializer.Deserialize<JsonElement>(infoStr);
                    var files    = infoObj.TryGetProperty("files", out var f) ? f.EnumerateArray().ToArray() : [];
                    var fileSize = files.Length > 0 && files[0].GetArrayLength() > 1
                        ? fileSizeService.FormatFileSize(files[0][1].GetInt64())
                        : "0B";

                    db.FileLogs.Add(new FileLog
                    {
                        File      = body.TryGetProperty("path",    out var path) ? path.GetString() ?? ""  : "",
                        UserId    = body.TryGetProperty("peer_id", out var pid)  ? pid.GetString()  ?? "0" : "0",
                        UserIp    = infoObj.TryGetProperty("ip",   out var uip)  ? uip.GetString()  ?? "0" : "0",
                        RemoteId  = body.TryGetProperty("id",      out var rid)  ? rid.GetString()  ?? "0" : "0",
                        FileSize  = fileSize,
                        Direction = body.TryGetProperty("type",    out var t)    ? t.GetInt32()             : 0,
                        LoggedAt  = DateTime.UtcNow
                    });
                    await db.SaveChangesAsync();
                }
                else
                {
                    try
                    {
                        if (body.TryGetProperty("peer", out var peer))
                        {
                            var peerId    = peer.GetArrayLength() > 0 ? peer[0].GetString() : null;
                            var connId    = body.TryGetProperty("conn_id",    out var ci) ? ci.ToString() : null;
                            var sessionId = body.TryGetProperty("session_id", out var si) ? si.ToString() : null;
                            await db.ConnLogs
                                .Where(l => l.ConnId == connId)
                                .ExecuteUpdateAsync(s => s
                                    .SetProperty(l => l.SessionId, sessionId)
                                    .SetProperty(l => l.FromId,    peerId));
                        }
                    }
                    catch { /* ignore audit parse errors */ }
                }
                break;
            }
        }

        return Results.Json(new { code = 1, data = "ok" });
    }
}
