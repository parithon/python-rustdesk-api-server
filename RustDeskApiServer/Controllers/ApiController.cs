using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RustDeskApiServer.Data;
using RustDeskApiServer.Models;
using RustDeskApiServer.Services;
using RustDeskApiServer.ViewModels;
using System.Text.Json;

namespace RustDeskApiServer.Controllers;

[Route("api")]
public class ApiController(
    AppDbContext db,
    UserManager<UserProfile> userManager,
    ITokenService tokenService,
    IFileSizeService fileSizeService,
    IConfiguration configuration) : Controller
{
    private const int EffectiveSeconds = 7200;

    private string GetClientIp()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        return forwarded?.Split(',')[0].Trim()
               ?? HttpContext.Connection.RemoteIpAddress?.ToString()
               ?? string.Empty;
    }

    // POST /api/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] JsonElement body)
    {
        var username = body.TryGetProperty("username", out var u) ? u.GetString() ?? "" : "";
        var password = body.TryGetProperty("password", out var p) ? p.GetString() ?? "" : "";
        var rid = body.TryGetProperty("id", out var r) ? r.GetString() ?? "" : "";
        var uuid = body.TryGetProperty("uuid", out var uid) ? uid.GetString() ?? "" : "";
        var autoLogin = body.TryGetProperty("autoLogin", out var al) && al.GetBoolean();
        var rtype = body.TryGetProperty("type", out var rt) ? rt.GetString() ?? "" : "";
        var deviceInfo = body.TryGetProperty("deviceInfo", out var di) ? di.ToString() : "";

        var user = await userManager.FindByNameAsync(username);
        if (user is null || !await userManager.CheckPasswordAsync(user, password))
            return Json(new { error = "Invalid username or password. Please try again." });

        user.RustDeskId = rid;
        user.Uuid = uuid;
        user.AutoLogin = autoLogin;
        user.RType = rtype;
        user.DeviceInfo = deviceInfo;
        await db.SaveChangesAsync();

        // Bind device if no peer exists for this rid
        if (!await db.Peers.AnyAsync(p2 => p2.RustDeskId == rid))
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

        if (token is not null)
        {
            var age = (DateTime.UtcNow - token.CreateTime).TotalSeconds;
            if (age >= EffectiveSeconds)
            {
                db.Tokens.Remove(token);
                await db.SaveChangesAsync();
                token = null;
            }
        }

        if (token is null)
        {
            token = new RustDeskToken
            {
                Username = user.UserName ?? username,
                UserId = user.Id,
                Uuid = uuid,
                RustDeskId = rid,
                AccessToken = tokenService.GenerateToken(DateTime.UtcNow.Ticks.ToString()),
                CreateTime = DateTime.UtcNow
            };
            db.Tokens.Add(token);
            await db.SaveChangesAsync();
        }

        return Json(new
        {
            access_token = token.AccessToken,
            type = "access_token",
            user = new { name = user.UserName }
        });
    }

    // POST /api/logout
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] JsonElement body)
    {
        var rid = body.TryGetProperty("id", out var r) ? r.GetString() ?? "" : "";
        var uuid = body.TryGetProperty("uuid", out var u) ? u.GetString() ?? "" : "";
        var user = await db.Users.FirstOrDefaultAsync(u2 => u2.RustDeskId == rid && u2.Uuid == uuid);
        if (user is null)
            return Json(new { error = "Invalid request." });

        var token = await db.Tokens.FirstOrDefaultAsync(t => t.UserId == user.Id && t.RustDeskId == rid);
        if (token is not null)
        {
            db.Tokens.Remove(token);
            await db.SaveChangesAsync();
        }
        return Json(new { code = 1 });
    }

    // POST /api/currentUser
    [HttpPost("currentUser")]
    public async Task<IActionResult> CurrentUser()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault() ?? "";
        var accessToken = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authHeader["Bearer ".Length..] : authHeader;

        var token = await db.Tokens.FirstOrDefaultAsync(t => t.AccessToken == accessToken);
        if (token is null) return Json(new { });

        var user = await db.Users.FindAsync(token.UserId);
        if (user is null) return Json(new { });

        return Json(new
        {
            access_token = token.AccessToken,
            type = "access_token",
            name = user.UserName
        });
    }

    // GET+POST /api/ab
    [HttpGet("ab")]
    [HttpPost("ab")]
    public async Task<IActionResult> Ab()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault() ?? "";
        var accessToken = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authHeader["Bearer ".Length..] : authHeader;

        var token = await db.Tokens.FirstOrDefaultAsync(t => t.AccessToken == accessToken);
        if (token is null)
            return Json(new { error = "Failed to retrieve address book." });

        if (Request.Method == "GET")
        {
            var tags = await db.Tags.Where(t => t.UserId == token.UserId).ToListAsync();
            var tagNames = tags.Select(t => t.TagName).ToArray();
            var tagColors = tags
                .Where(t => !string.IsNullOrEmpty(t.TagColor))
                .ToDictionary(t => t.TagName, t => t.TagColor);

            var peers = await db.Peers.Where(p => p.UserId == token.UserId).ToListAsync();
            var peersResult = peers.Select(p => new
            {
                id = p.RustDeskId,
                username = p.Username,
                hostname = p.Hostname,
                alias = p.Alias,
                platform = p.Platform,
                tags = p.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries),
                hash = p.RHash
            }).ToArray();

            var data = new
            {
                tags = tagNames,
                peers = peersResult,
                tag_colors = JsonSerializer.Serialize(tagColors)
            };

            return Json(new
            {
                updated_at = DateTime.UtcNow,
                data = JsonSerializer.Serialize(data)
            });
        }
        else
        {
            using var reader = new StreamReader(Request.Body);
            var rawBody = await reader.ReadToEndAsync();
            var postData = JsonSerializer.Deserialize<JsonElement>(rawBody);

            var dataStr = postData.TryGetProperty("data", out var d) ? d.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(dataStr))
                return Json(new { code = 102, data = "Failed to update address book." });

            var data = JsonSerializer.Deserialize<JsonElement>(dataStr);
            var tagNames = data.TryGetProperty("tags", out var tn)
                ? tn.EnumerateArray().Select(x => x.GetString() ?? "").ToArray()
                : [];
            var tagColorsStr = data.TryGetProperty("tag_colors", out var tc) ? tc.GetString() ?? "" : "";
            var tagColors = string.IsNullOrEmpty(tagColorsStr)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(tagColorsStr) ?? [];
            var peers = data.TryGetProperty("peers", out var pe)
                ? pe.EnumerateArray().ToArray()
                : [];

            if (tagNames.Length > 0)
            {
                db.Tags.RemoveRange(db.Tags.Where(t => t.UserId == token.UserId));
                db.Tags.AddRange(tagNames.Select(name => new RustDeskTag
                {
                    UserId = token.UserId,
                    TagName = name,
                    TagColor = tagColors.TryGetValue(name, out var color) ? color : ""
                }));
                await db.SaveChangesAsync();
            }

            if (peers.Length > 0)
            {
                db.Peers.RemoveRange(db.Peers.Where(p => p.UserId == token.UserId));
                db.Peers.AddRange(peers.Select(p => new RustDeskPeer
                {
                    UserId = token.UserId,
                    RustDeskId = p.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                    Username = p.TryGetProperty("username", out var un) ? un.GetString() ?? "" : "",
                    Hostname = p.TryGetProperty("hostname", out var hn) ? hn.GetString() ?? "" : "",
                    Alias = p.TryGetProperty("alias", out var al) ? al.GetString() ?? "" : "",
                    Platform = p.TryGetProperty("platform", out var pl) ? pl.GetString() ?? "" : "",
                    Tags = p.TryGetProperty("tags", out var tgs)
                        ? string.Join(",", tgs.EnumerateArray().Select(x => x.GetString() ?? ""))
                        : "",
                    RHash = p.TryGetProperty("hash", out var h) ? h.GetString() ?? "" : ""
                }));
                await db.SaveChangesAsync();
            }

            return Json(new { code = 102, data = "Failed to update address book." });
        }
    }

    // POST /api/ab/get  (sciter compatibility)
    [HttpPost("ab/get")]
    public async Task<IActionResult> AbGet()
    {
        Request.Method = "GET";
        return await Ab();
    }

    // GET /api/users
    [HttpGet("users")]
    [HttpPost("users")]
    public IActionResult Users() => Json(new { code = 1, data = "ok" });

    // GET /api/peers
    [HttpGet("peers")]
    [HttpPost("peers")]
    public IActionResult Peers() => Json(new { code = 1, data = "ok" });

    // POST /api/sysinfo
    [HttpPost("sysinfo")]
    public async Task<IActionResult> SysInfo([FromBody] JsonElement body)
    {
        var clientIp = GetClientIp();
        var rid = body.TryGetProperty("id", out var r) ? r.GetString() ?? "" : "";
        var uuid = body.TryGetProperty("uuid", out var u) ? u.GetString() ?? "" : "";

        var device = await db.Devices.FirstOrDefaultAsync(d => d.RustDeskId == rid && d.Uuid == uuid);
        if (device is null)
        {
            db.Devices.Add(new RustDeskDevice
            {
                RustDeskId = rid,
                Cpu = body.TryGetProperty("cpu", out var cpu) ? cpu.GetString() ?? "" : "",
                Hostname = body.TryGetProperty("hostname", out var hn) ? hn.GetString() ?? "" : "",
                Memory = body.TryGetProperty("memory", out var mem) ? mem.GetString() ?? "" : "",
                Os = body.TryGetProperty("os", out var os) ? os.GetString() ?? "" : "",
                Username = body.TryGetProperty("username", out var un) ? un.GetString() ?? "-" : "-",
                Uuid = uuid,
                Version = body.TryGetProperty("version", out var ver) ? ver.GetString() ?? "" : "",
                IpAddress = clientIp,
                CreateTime = DateTime.UtcNow,
                UpdateTime = DateTime.UtcNow
            });
        }
        else
        {
            device.Cpu = body.TryGetProperty("cpu", out var cpu) ? cpu.GetString() ?? device.Cpu : device.Cpu;
            device.Hostname = body.TryGetProperty("hostname", out var hn) ? hn.GetString() ?? device.Hostname : device.Hostname;
            device.Memory = body.TryGetProperty("memory", out var mem) ? mem.GetString() ?? device.Memory : device.Memory;
            device.Os = body.TryGetProperty("os", out var os) ? os.GetString() ?? device.Os : device.Os;
            device.Username = body.TryGetProperty("username", out var un) ? un.GetString() ?? device.Username : device.Username;
            device.Version = body.TryGetProperty("version", out var ver) ? ver.GetString() ?? device.Version : device.Version;
            device.IpAddress = clientIp;
            device.UpdateTime = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return Json(new { data = "ok" });
    }

    // POST /api/heartbeat
    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] JsonElement body)
    {
        var rid = body.TryGetProperty("id", out var r) ? r.GetString() ?? "" : "";
        var uuid = body.TryGetProperty("uuid", out var u) ? u.GetString() ?? "" : "";

        var device = await db.Devices.FirstOrDefaultAsync(d => d.RustDeskId == rid && d.Uuid == uuid);
        if (device is not null)
        {
            device.IpAddress = GetClientIp();
            device.UpdateTime = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var newExpiry = DateTime.UtcNow.AddSeconds(EffectiveSeconds);
        await db.Tokens
            .Where(t => t.RustDeskId == rid && t.Uuid == uuid)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.CreateTime, newExpiry));

        return Json(new { data = "online" });
    }

    // POST /api/audit
    [HttpPost("audit")]
    public async Task<IActionResult> Audit([FromBody] JsonElement body)
    {
        var auditType = body.TryGetProperty("action", out var act) ? act.GetString() ?? "" : "";

        switch (auditType)
        {
            case "new":
            {
                var log = new ConnLog
                {
                    Action = auditType,
                    ConnId = body.TryGetProperty("conn_id", out var ci) ? ci.ToString() : null,
                    FromIp = body.TryGetProperty("ip", out var ip) ? ip.GetString() : null,
                    FromId = string.Empty,
                    RustDeskId = body.TryGetProperty("id", out var id) ? id.GetString() : null,
                    ConnStart = DateTime.UtcNow,
                    SessionId = body.TryGetProperty("session_id", out var si) ? si.ToString() : null,
                    Uuid = body.TryGetProperty("uuid", out var uid) ? uid.GetString() : null
                };
                db.ConnLogs.Add(log);
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
            default when body.TryGetProperty("is_file", out _):
            {
                var infoStr = body.TryGetProperty("info", out var info) ? info.GetString() ?? "{}" : "{}";
                var infoObj = JsonSerializer.Deserialize<JsonElement>(infoStr);
                var files = infoObj.TryGetProperty("files", out var f) ? f.EnumerateArray().ToArray() : [];
                var fileSize = files.Length > 0 && files[0].GetArrayLength() > 1
                    ? fileSizeService.FormatFileSize(files[0][1].GetInt64())
                    : "0B";

                db.FileLogs.Add(new FileLog
                {
                    File = body.TryGetProperty("path", out var path) ? path.GetString() ?? "" : "",
                    UserId = body.TryGetProperty("peer_id", out var pid) ? pid.GetString() ?? "0" : "0",
                    UserIp = infoObj.TryGetProperty("ip", out var userIp) ? userIp.GetString() ?? "0" : "0",
                    RemoteId = body.TryGetProperty("id", out var rid) ? rid.GetString() ?? "0" : "0",
                    FileSize = fileSize,
                    Direction = body.TryGetProperty("type", out var t) ? t.GetInt32() : 0,
                    LoggedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
                break;
            }
            default:
            {
                try
                {
                    if (body.TryGetProperty("peer", out var peer))
                    {
                        var peerId = peer.GetArrayLength() > 0 ? peer[0].GetString() : null;
                        var connId = body.TryGetProperty("conn_id", out var ci) ? ci.ToString() : null;
                        var sessionId = body.TryGetProperty("session_id", out var si) ? si.ToString() : null;

                        await db.ConnLogs
                            .Where(l => l.ConnId == connId)
                            .ExecuteUpdateAsync(s => s
                                .SetProperty(l => l.SessionId, sessionId)
                                .SetProperty(l => l.FromId, peerId));
                    }
                }
                catch { /* ignore audit parse errors */ }
                break;
            }
        }

        return Json(new { code = 1, data = "ok" });
    }
}
