using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
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
public class WebController(
    AppDbContext db,
    UserManager<UserProfile> userManager,
    SignInManager<UserProfile> signInManager,
    ITokenService tokenService,
    IConfiguration configuration) : Controller
{
    private const int PageSize = 15;
    private const int LogPageSize = 20;
    private const int ShareLinkExpiryMinutes = 15;

    // GET / -> redirect
    [HttpGet("/")]
    [AllowAnonymous]
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect("/api/work");
        return Redirect("/api/user_action?action=login");
    }

    // GET+POST /api/user_action
    [HttpGet("user_action")]
    [HttpPost("user_action")]
    [AllowAnonymous]
    public async Task<IActionResult> UserAction()
    {
        var action = Request.Query["action"].FirstOrDefault() ?? "";
        return action switch
        {
            "login" => await UserLogin(),
            "register" => await UserRegister(),
            "logout" => await UserLogout(),
            _ => NotFound()
        };
    }

    private async Task<IActionResult> UserLogin()
    {
        if (Request.Method == "GET")
            return View("Login");

        var form = await Request.ReadFormAsync();
        var username = form["account"].FirstOrDefault() ?? "";
        var password = form["password"].FirstOrDefault() ?? "";

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            return Json(new { code = 0, msg = "Username or password is missing." });

        var user = await userManager.FindByNameAsync(username);
        if (user is null || !await userManager.CheckPasswordAsync(user, password))
            return Json(new { code = 0, msg = "Invalid username or password." });

        await signInManager.SignInAsync(user, isPersistent: false);
        return Json(new { code = 1, url = "/api/work" });
    }

    private async Task<IActionResult> UserRegister()
    {
        if (Request.Method == "GET")
            return View("Register");

        var allowRegistration = configuration.GetValue("AllowRegistration", true);
        if (!allowRegistration)
            return Json(new { code = 0, msg = "Registration is currently closed. Please contact the administrator." });

        var form = await Request.ReadFormAsync();
        var username = form["user"].FirstOrDefault() ?? "";
        var password = form["pwd"].FirstOrDefault() ?? "";

        if (username.Length <= 3)
            return Json(new { code = 0, msg = "Username must be more than 3 characters." });

        if (password.Length < 8 || password.Length > 20)
            return Json(new { code = 0, msg = "Password must be between 8 and 20 characters." });

        if (await userManager.FindByNameAsync(username) is not null)
            return Json(new { code = 0, msg = "Username already exists." });

        var isFirstUser = !await db.Users.AnyAsync();
        var newUser = new UserProfile
        {
            UserName = username,
            IsAdmin = isFirstUser,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(newUser, password);
        if (!result.Succeeded)
            return Json(new { code = 0, msg = string.Join(", ", result.Errors.Select(e => e.Description)) });

        return Json(new { code = 1, msg = "Registration successful. Please login." });
    }

    private async Task<IActionResult> UserLogout()
    {
        await signInManager.SignOutAsync();
        return Redirect("/api/user_action?action=login");
    }

    // GET /api/work
    [HttpGet("work")]
    [Authorize]
    public async Task<IActionResult> Work()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Redirect("/api/user_action?action=login");

        var showType = Request.Query["show_type"].FirstOrDefault() ?? "";
        var showAll = showType == "admin" && user.IsAdmin;
        var page = int.TryParse(Request.Query["page"], out var p) ? p : 1;

        IEnumerable<DeviceViewModel> items = showAll
            ? await GetAllDeviceViewModels()
            : await GetUserDeviceViewModels(user.Id);

        var totalItems = items.Count();
        var totalPages = (int)Math.Ceiling(totalItems / (double)PageSize);
        var pageItems = items.Skip((page - 1) * PageSize).Take(PageSize);

        return View("Work", new WorkViewModel(user, showAll, pageItems, page, totalPages));
    }

    private async Task<IEnumerable<DeviceViewModel>> GetUserDeviceViewModels(int userId)
    {
        var peers = await db.Peers.Where(p => p.UserId == userId).ToListAsync();
        var rids = peers.Select(p => p.RustDeskId).ToList();
        var devices = await db.Devices.Where(d => rids.Contains(d.RustDeskId)).ToListAsync();
        var deviceMap = devices.ToDictionary(d => d.RustDeskId);
        var now = DateTime.UtcNow;

        return peers.Select(peer =>
        {
            deviceMap.TryGetValue(peer.RustDeskId, out var dev);
            return new DeviceViewModel(
                RustDeskId: peer.RustDeskId,
                Version: dev?.Version ?? "",
                HasRHash: peer.RHash.Length > 1 ? "Yes" : "No",
                Username: peer.Username,
                Hostname: peer.Hostname,
                Alias: peer.Alias,
                Platform: peer.Platform,
                Os: dev?.Os ?? "",
                Cpu: dev?.Cpu ?? "",
                Memory: dev?.Memory ?? "",
                IpAddress: dev?.IpAddress ?? "",
                CreateTime: dev?.CreateTime.ToLocalTime().ToString("yyyy-MM-dd") ?? "",
                UpdateTime: dev?.UpdateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "",
                Status: dev is not null && (now - dev.UpdateTime).TotalSeconds <= 120 ? "Online" : "Offline"
            );
        });
    }

    private async Task<IEnumerable<DeviceViewModel>> GetAllDeviceViewModels()
    {
        var devices = await db.Devices.ToListAsync();
        var peers = await db.Peers.ToListAsync();
        var peerMap = peers.ToDictionary(p => p.RustDeskId);
        var now = DateTime.UtcNow;

        var userIds = peers.Select(p => p.UserId).Distinct().ToList();
        var users = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.UserName ?? "");

        return devices.Select(dev =>
        {
            peerMap.TryGetValue(dev.RustDeskId, out var peer);
            var rustUser = peer is not null && users.TryGetValue(peer.UserId, out var un) ? un : "Not Logged In";
            return new DeviceViewModel(
                RustDeskId: dev.RustDeskId,
                Version: dev.Version,
                HasRHash: peer?.RHash.Length > 1 ? "Yes" : "No",
                Username: dev.Username,
                Hostname: dev.Hostname,
                Alias: peer?.Alias ?? "",
                Platform: peer?.Platform ?? "",
                Os: dev.Os,
                Cpu: dev.Cpu,
                Memory: dev.Memory,
                IpAddress: dev.IpAddress,
                CreateTime: dev.CreateTime.ToLocalTime().ToString("yyyy-MM-dd"),
                UpdateTime: dev.UpdateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                Status: (now - dev.UpdateTime).TotalSeconds <= 120 ? "Online" : "Offline",
                RustUser: rustUser
            );
        });
    }

    // GET /api/down_peers
    [HttpGet("down_peers")]
    [Authorize]
    public async Task<IActionResult> DownPeers()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null || !user.IsAdmin) return Redirect("/api/work");

        var allInfo = (await GetAllDeviceViewModels()).ToList();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Device Info");
        string[] headers = ["RustDesk ID", "Rust User", "Version", "Username", "Hostname", "OS", "CPU", "Memory",
                             "IP Address", "Create Time", "Update Time", "Status", "Platform", "Alias", "Has Password"];

        for (var j = 0; j < headers.Length; j++)
            sheet.Cell(1, j + 1).Value = headers[j];

        for (var i = 0; i < allInfo.Count; i++)
        {
            var item = allInfo[i];
            sheet.Cell(i + 2, 1).Value = item.RustDeskId;
            sheet.Cell(i + 2, 2).Value = item.RustUser;
            sheet.Cell(i + 2, 3).Value = item.Version;
            sheet.Cell(i + 2, 4).Value = item.Username;
            sheet.Cell(i + 2, 5).Value = item.Hostname;
            sheet.Cell(i + 2, 6).Value = item.Os;
            sheet.Cell(i + 2, 7).Value = item.Cpu;
            sheet.Cell(i + 2, 8).Value = item.Memory;
            sheet.Cell(i + 2, 9).Value = item.IpAddress;
            sheet.Cell(i + 2, 10).Value = item.CreateTime;
            sheet.Cell(i + 2, 11).Value = item.UpdateTime;
            sheet.Cell(i + 2, 12).Value = item.Status;
            sheet.Cell(i + 2, 13).Value = item.Platform;
            sheet.Cell(i + 2, 14).Value = item.Alias;
            sheet.Cell(i + 2, 15).Value = item.HasRHash;
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Seek(0, SeekOrigin.Begin);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "DeviceInfo.xlsx");
    }

    // GET+POST /api/share and /api/share/{hash}
    [HttpGet("share")]
    [HttpGet("share/{hash}")]
    [HttpPost("share")]
    [Authorize]
    public async Task<IActionResult> Share(string? hash = null)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Redirect("/api/user_action?action=login");

        // Expire old share links
        var shareLinks = await db.ShareLinks
            .Where(s => s.UserId == user.Id && !s.IsUsed && !s.IsExpired)
            .ToListAsync();

        foreach (var sl in shareLinks)
        {
            if ((DateTime.UtcNow - sl.CreateTime).TotalMinutes >= ShareLinkExpiryMinutes)
            {
                sl.IsExpired = true;
            }
        }
        await db.SaveChangesAsync();

        if (Request.Method == "GET" && hash is not null)
        {
            // Someone is visiting a share link
            var shareLink = await db.ShareLinks.FirstOrDefaultAsync(s => s.SHash == hash);
            if (shareLink is null)
                return View("Message", (Title: "Error", Message: $"Share link does not exist or has expired."));

            if (shareLink.UserId == user.Id)
                return View("Message", (Title: "Error", Message: "You cannot use your own share link."));

            shareLink.IsUsed = true;
            var peerIds = shareLink.Peers.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var selfPeerIds = await db.Peers
                .Where(p => p.UserId == user.Id)
                .Select(p => p.RustDeskId)
                .ToListAsync();
            var sharedPeers = await db.Peers
                .Where(p => peerIds.Contains(p.RustDeskId) && p.UserId == shareLink.UserId)
                .ToListAsync();

            var added = new List<string>();
            foreach (var peer in sharedPeers)
            {
                if (selfPeerIds.Contains(peer.RustDeskId)) continue;
                db.Peers.Add(new RustDeskPeer
                {
                    UserId = user.Id,
                    RustDeskId = peer.RustDeskId,
                    Username = peer.Username,
                    Hostname = peer.Hostname,
                    Alias = peer.Alias,
                    Platform = peer.Platform,
                    Tags = peer.Tags,
                    RHash = peer.RHash
                });
                added.Add(peer.RustDeskId);
            }
            await db.SaveChangesAsync();

            var msg = string.Join(", ", added) + " successfully acquired.";
            return View("Message", (Title: "Success", Message: msg));
        }

        if (Request.Method == "POST")
        {
            var form = await Request.ReadFormAsync();
            var dataStr = form["data"].FirstOrDefault() ?? "[]";
            var data = JsonSerializer.Deserialize<SharePeerItem[]>(dataStr) ?? [];
            if (data.Length == 0)
                return Json(new { code = 0, msg = "No devices selected." });

            var rustdeskIds = string.Join(",", data.Select(d => d.Title.Split('|')[0]));
            var newLink = new ShareLink
            {
                UserId = user.Id,
                SHash = tokenService.GenerateToken(DateTime.UtcNow.Ticks.ToString()),
                Peers = rustdeskIds,
                CreateTime = DateTime.UtcNow
            };
            db.ShareLinks.Add(newLink);
            await db.SaveChangesAsync();
            return Json(new { code = 1, shash = newLink.SHash });
        }

        // GET /api/share
        var peers = await db.Peers.Where(p => p.UserId == user.Id).ToListAsync();
        var currentLinks = await db.ShareLinks
            .Where(s => s.UserId == user.Id && !s.IsUsed && !s.IsExpired)
            .ToListAsync();

        var peerItems = peers.Select((p, i) => new SharePeerItem(
            Value: (i + 1).ToString(),
            Title: $"{p.RustDeskId}|{p.Alias}"
        ));

        var linkItems = currentLinks.Select(s => new ShareLinkViewModel(
            SHash: s.SHash,
            IsUsed: s.IsUsed,
            IsExpired: s.IsExpired,
            CreateTime: s.CreateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            Peers: s.Peers
        ));

        return View("Share", new ShareViewModel(user, peerItems, linkItems));
    }

    // GET /api/conn_log
    [HttpGet("conn_log")]
    [Authorize]
    public async Task<IActionResult> ConnLog()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Redirect("/api/user_action?action=login");

        var page = int.TryParse(Request.Query["page"], out var p) ? p : 1;
        var logs = await GetConnLogViewModels();
        var total = logs.Count;
        var totalPages = (int)Math.Ceiling(total / (double)LogPageSize);
        var items = logs.Skip((page - 1) * LogPageSize).Take(LogPageSize);
        return View("ConnLog", new ConnLogPageViewModel(user, items, page, totalPages));
    }

    // GET /api/file_log
    [HttpGet("file_log")]
    [Authorize]
    public async Task<IActionResult> FileLog()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Redirect("/api/user_action?action=login");

        var page = int.TryParse(Request.Query["page"], out var p) ? p : 1;
        var logs = await GetFileLogViewModels();
        var total = logs.Count;
        var totalPages = (int)Math.Ceiling(total / (double)LogPageSize);
        var items = logs.Skip((page - 1) * LogPageSize).Take(LogPageSize);
        return View("FileLog", new FileLogPageViewModel(user, items, page, totalPages));
    }

    private async Task<List<ConnLogViewModel>> GetConnLogViewModels()
    {
        var logs = await db.ConnLogs.OrderByDescending(l => l.ConnStart).ToListAsync();
        var allPeers = await db.Peers.ToListAsync();
        var peerAliasMap = allPeers.ToDictionary(p => p.RustDeskId, p => p.Alias);

        return logs.Select(log =>
        {
            peerAliasMap.TryGetValue(log.RustDeskId ?? "", out var alias);
            peerAliasMap.TryGetValue(log.FromId ?? "", out var fromAlias);
            string duration;
            try
            {
                if (log.ConnEnd.HasValue && log.ConnStart.HasValue)
                {
                    var total = (int)(log.ConnEnd.Value - log.ConnStart.Value).TotalSeconds;
                    duration = $"{total / 3600:D2}:{total % 3600 / 60:D2}:{total % 60:D2}";
                }
                else duration = "-";
            }
            catch { duration = "-"; }

            return new ConnLogViewModel(
                FromIp: log.FromIp ?? "",
                FromId: log.FromId ?? "",
                FromAlias: fromAlias ?? "UNKNOWN",
                RustDeskId: log.RustDeskId ?? "",
                Alias: alias ?? "UNKNOWN",
                ConnStart: log.ConnStart?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                ConnEnd: log.ConnEnd?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                Duration: duration
            );
        }).ToList();
    }

    private async Task<List<FileLogViewModel>> GetFileLogViewModels()
    {
        var logs = await db.FileLogs.OrderByDescending(l => l.LoggedAt).ToListAsync();
        var allPeers = await db.Peers.ToListAsync();
        var peerAliasMap = allPeers.ToDictionary(p => p.RustDeskId, p => p.Alias);

        return logs.Select(log =>
        {
            peerAliasMap.TryGetValue(log.RemoteId, out var remoteAlias);
            peerAliasMap.TryGetValue(log.UserId, out var userAlias);
            return new FileLogViewModel(
                File: log.File,
                RemoteId: log.RemoteId,
                RemoteAlias: remoteAlias ?? "UNKNOWN",
                UserId: log.UserId,
                UserAlias: userAlias ?? "UNKNOWN",
                UserIp: log.UserIp,
                FileSize: log.FileSize,
                Direction: log.Direction,
                LoggedAt: log.LoggedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
            );
        }).ToList();
    }
}
