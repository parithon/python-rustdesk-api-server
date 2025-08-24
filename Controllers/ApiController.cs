using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RustDeskApiServer.Data;
using RustDeskApiServer.Models;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using System.Text;

namespace RustDeskApiServer.Controllers
{
    [ApiController]
    [Route("api")]
    public class ApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<UserProfile> _userManager;
        private readonly SignInManager<UserProfile> _signInManager;
        private readonly IConfiguration _configuration;

        public ApiController(
            ApplicationDbContext context,
            UserManager<UserProfile> userManager,
            SignInManager<UserProfile> signInManager,
            IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { error = "Invalid request" });
            }

            var user = await _userManager.FindByNameAsync(request.Username);
            if (user == null)
            {
                return BadRequest(new { error = "Invalid username or password" });
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
            if (!result.Succeeded)
            {
                return BadRequest(new { error = "Invalid username or password" });
            }

            // Update user information
            user.RustDeskId = request.Id;
            user.Uuid = request.Uuid;
            user.AutoLogin = request.AutoLogin;
            user.RType = request.Type;
            user.DeviceInfo = JsonSerializer.Serialize(request.DeviceInfo);
            user.LastLogin = DateTime.UtcNow;

            await _userManager.UpdateAsync(user);

            // Handle device association
            await HandleDeviceAssociation(user, request);

            // Generate or get token
            var token = await GetOrCreateToken(user);

            return Ok(new
            {
                access_token = token.AccessToken,
                type = "access_token",
                user = new { name = user.UserName }
            });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { error = "Invalid request" });
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.RustDeskId == request.Id && u.Uuid == request.Uuid);

            if (user == null)
            {
                return BadRequest(new { error = "Invalid request" });
            }

            var token = await _context.RustDeskTokens
                .FirstOrDefaultAsync(t => t.UserId == user.Id && t.RustDeskId == user.RustDeskId);

            if (token != null)
            {
                _context.RustDeskTokens.Remove(token);
                await _context.SaveChangesAsync();
            }

            return Ok(new { code = 1 });
        }

        [HttpPost("currentUser")]
        public async Task<IActionResult> CurrentUser()
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return BadRequest(new { error = "Invalid authorization header" });
            }

            var accessToken = authHeader.Substring("Bearer ".Length);
            var token = await _context.RustDeskTokens
                .FirstOrDefaultAsync(t => t.AccessToken == accessToken);

            if (token == null)
            {
                return BadRequest(new { error = "Invalid token" });
            }

            var user = await _userManager.FindByIdAsync(token.UserId);
            if (user == null)
            {
                return BadRequest(new { error = "User not found" });
            }

            return Ok(new
            {
                access_token = token.AccessToken,
                type = "access_token",
                name = user.UserName
            });
        }

        [HttpGet("ab")]
        [HttpPost("ab")]
        public async Task<IActionResult> AddressBook()
        {
            var token = await GetTokenFromRequest();
            if (token == null)
            {
                return BadRequest(new { error = "Failed to retrieve list" });
            }

            if (Request.Method == "GET")
            {
                return await GetAddressBook(token);
            }
            else
            {
                return await UpdateAddressBook(token);
            }
        }

        private async Task<RustDeskToken?> GetTokenFromRequest()
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return null;
            }

            var accessToken = authHeader.Substring("Bearer ".Length);
            return await _context.RustDeskTokens
                .FirstOrDefaultAsync(t => t.AccessToken == accessToken);
        }

        private async Task<IActionResult> GetAddressBook(RustDeskToken token)
        {
            var tags = await _context.RustDeskTags
                .Where(t => t.UserId == token.UserId)
                .ToListAsync();

            var tagNames = tags.Select(t => t.TagName).ToList();
            var tagColors = tags.ToDictionary(t => t.TagName, t => t.TagColor);

            var peers = await _context.RustDeskPeers
                .Where(p => p.UserId == token.UserId)
                .ToListAsync();

            var peerList = peers.Select(p => new
            {
                id = p.RustDeskId,
                username = p.Username,
                hostname = p.Hostname,
                platform = p.Platform,
                alias = p.Alias,
                tags = p.Tags?.Split(',').Where(t => !string.IsNullOrEmpty(t)).ToList() ?? new List<string>(),
                hash = p.ConnectionPassword
            }).ToList();

            return Ok(new
            {
                updated_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                data = JsonSerializer.Serialize(new
                {
                    tag_name = tagNames,
                    tag_color = tagColors,
                    peers = peerList
                })
            });
        }

        private async Task<IActionResult> UpdateAddressBook(RustDeskToken token)
        {
            var requestBody = await new StreamReader(Request.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<AddressBookUpdateRequest>(requestBody);

            if (data?.Data == null)
            {
                return BadRequest(new { error = "Invalid data" });
            }

            // Update tags
            if (data.Data.TagNames != null)
            {
                var existingTags = await _context.RustDeskTags
                    .Where(t => t.UserId == token.UserId)
                    .ToListAsync();

                _context.RustDeskTags.RemoveRange(existingTags);

                foreach (var tagName in data.Data.TagNames)
                {
                    var color = data.Data.TagColors?.GetValueOrDefault(tagName) ?? "";
                    _context.RustDeskTags.Add(new RustDeskTag
                    {
                        UserId = token.UserId,
                        TagName = tagName,
                        TagColor = color
                    });
                }
            }

            // Update peers
            if (data.Data.Peers != null)
            {
                var existingPeers = await _context.RustDeskPeers
                    .Where(p => p.UserId == token.UserId)
                    .ToListAsync();

                _context.RustDeskPeers.RemoveRange(existingPeers);

                foreach (var peer in data.Data.Peers)
                {
                    _context.RustDeskPeers.Add(new RustDeskPeer
                    {
                        UserId = token.UserId,
                        RustDeskId = peer.Id ?? "",
                        Username = peer.Username ?? "",
                        Hostname = peer.Hostname ?? "",
                        Platform = peer.Platform ?? "",
                        Alias = peer.Alias ?? "",
                        Tags = string.Join(",", peer.Tags ?? new List<string>()),
                        ConnectionPassword = peer.Hash ?? ""
                    });
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { code = 1 });
        }

        private async Task HandleDeviceAssociation(UserProfile user, LoginRequest request)
        {
            var peer = await _context.RustDeskPeers
                .FirstOrDefaultAsync(p => p.RustDeskId == request.Id);

            if (peer == null)
            {
                var device = await _context.RustDeskDevices
                    .FirstOrDefaultAsync(d => d.Uuid == request.Uuid);

                if (device != null)
                {
                    peer = new RustDeskPeer
                    {
                        UserId = user.Id,
                        RustDeskId = device.RustDeskId,
                        Hostname = device.Hostname,
                        Username = device.Username
                    };
                    _context.RustDeskPeers.Add(peer);
                    await _context.SaveChangesAsync();
                }
            }
        }

        [HttpPost("ab/get")]
        public async Task<IActionResult> AddressBookGet()
        {
            // Compatibility endpoint for x86-sciter version client
            return await AddressBook();
        }

        [HttpPost("users")]
        public async Task<IActionResult> Users()
        {
            var token = await GetTokenFromRequest();
            if (token == null)
            {
                return BadRequest(new { error = "Invalid token" });
            }

            // Return empty users list for compatibility
            return Ok(new { });
        }

        [HttpPost("peers")]
        public async Task<IActionResult> Peers()
        {
            var token = await GetTokenFromRequest();
            if (token == null)
            {
                return BadRequest(new { error = "Invalid token" });
            }

            // Return empty peers list for compatibility
            return Ok(new { });
        }

        [HttpPost("sysinfo")]
        public async Task<IActionResult> SystemInfo([FromBody] SystemInfoRequest request)
        {
            if (request?.Id == null || request?.Uuid == null)
            {
                return BadRequest(new { error = "Invalid request" });
            }

            // Check if device exists and update or create
            var device = await _context.RustDeskDevices
                .FirstOrDefaultAsync(d => d.RustDeskId == request.Id || d.Uuid == request.Uuid);

            if (device == null)
            {
                device = new RustDeskDevice
                {
                    RustDeskId = request.Id,
                    Uuid = request.Uuid,
                    CreateTime = DateTime.UtcNow
                };
                _context.RustDeskDevices.Add(device);
            }

            // Update device information
            device.Hostname = request.Hostname ?? device.Hostname;
            device.Username = request.Username ?? device.Username;
            device.Os = request.Os ?? device.Os;
            device.Cpu = request.Cpu ?? device.Cpu;
            device.Memory = request.Memory ?? device.Memory;
            device.Version = request.Version ?? device.Version;
            device.IpAddress = GetClientIp() ?? device.IpAddress;
            device.UpdateTime = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { });
        }

        [HttpPost("heartbeat")]
        public async Task<IActionResult> Heartbeat([FromBody] HeartbeatRequest request)
        {
            if (request?.Id == null)
            {
                return BadRequest(new { error = "Invalid request" });
            }

            // Update device last seen time
            var device = await _context.RustDeskDevices
                .FirstOrDefaultAsync(d => d.RustDeskId == request.Id);

            if (device != null)
            {
                device.UpdateTime = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return Ok(new { });
        }

        [HttpPost("audit")]
        public async Task<IActionResult> Audit([FromBody] AuditRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { error = "Invalid request" });
            }

            // Log connection or file transfer activity
            if (request.Action == "conn")
            {
                var connLog = new ConnectionLog
                {
                    Action = request.Action,
                    ConnectionId = request.ConnId?.ToString(),
                    FromIp = GetClientIp(),
                    FromId = request.FromId,
                    ToId = request.Id,
                    ConnectionStart = request.ConnStart?.DateTime,
                    ConnectionEnd = request.ConnEnd?.DateTime,
                    SessionId = request.SessionId,
                    Uuid = request.Uuid
                };
                _context.ConnectionLogs.Add(connLog);
            }
            else if (request.Action == "file")
            {
                var fileLog = new FileLog
                {
                    FilePath = request.File ?? "",
                    RemoteId = request.Id ?? "",
                    UserId = request.UserId ?? "",
                    UserIp = GetClientIp() ?? "",
                    FileSize = request.FileSize?.ToString() ?? "",
                    Direction = request.Direction ?? 0,
                    LoggedAt = DateTime.UtcNow
                };
                _context.FileLogs.Add(fileLog);
            }

            await _context.SaveChangesAsync();
            return Ok(new { });
        }

        private string? GetClientIp()
        {
            var xForwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xForwardedFor))
            {
                return xForwardedFor.Split(',')[0].Trim();
            }
            return Request.HttpContext.Connection.RemoteIpAddress?.ToString();
        }

        private async Task<RustDeskToken> GetOrCreateToken(UserProfile user)
        {
            var token = await _context.RustDeskTokens
                .FirstOrDefaultAsync(t => t.UserId == user.Id && 
                                        t.Username == user.UserName && 
                                        t.RustDeskId == user.RustDeskId);

            // Check if token is expired (assuming 24 hours expiration)
            if (token != null)
            {
                var effectiveSeconds = 24 * 60 * 60; // 24 hours
                var now = DateTime.UtcNow;
                var elapsed = (now - token.CreateTime).TotalSeconds;
                
                if (elapsed >= effectiveSeconds)
                {
                    _context.RustDeskTokens.Remove(token);
                    await _context.SaveChangesAsync();
                    token = null;
                }
            }

            if (token == null)
            {
                var salt = _configuration["TokenSalt"] ?? "rustdesk_default_salt";
                var accessTokenValue = ComputeMd5Hash(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString() + salt);

                token = new RustDeskToken
                {
                    Username = user.UserName ?? "",
                    UserId = user.Id,
                    Uuid = user.Uuid ?? "",
                    RustDeskId = user.RustDeskId ?? "",
                    AccessToken = accessTokenValue,
                    CreateTime = DateTime.UtcNow
                };

                _context.RustDeskTokens.Add(token);
                await _context.SaveChangesAsync();
            }

            return token;
        }

        private static string ComputeMd5Hash(string input)
        {
            using var md5 = MD5.Create();
            var inputBytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = md5.ComputeHash(inputBytes);
            return Convert.ToHexString(hashBytes).ToLower();
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string Uuid { get; set; } = string.Empty;
        public bool AutoLogin { get; set; } = true;
        public string Type { get; set; } = string.Empty;
        public object? DeviceInfo { get; set; }
    }

    public class LogoutRequest
    {
        public string Id { get; set; } = string.Empty;
        public string Uuid { get; set; } = string.Empty;
    }

    public class AddressBookUpdateRequest
    {
        public AddressBookData? Data { get; set; }
    }

    public class AddressBookData
    {
        public List<string>? TagNames { get; set; }
        public Dictionary<string, string>? TagColors { get; set; }
        public List<PeerData>? Peers { get; set; }
    }

    public class PeerData
    {
        public string? Id { get; set; }
        public string? Username { get; set; }
        public string? Hostname { get; set; }
        public string? Platform { get; set; }
        public string? Alias { get; set; }
        public List<string>? Tags { get; set; }
        public string? Hash { get; set; }
    }

    public class SystemInfoRequest
    {
        public string? Id { get; set; }
        public string? Uuid { get; set; }
        public string? Hostname { get; set; }
        public string? Username { get; set; }
        public string? Os { get; set; }
        public string? Cpu { get; set; }
        public string? Memory { get; set; }
        public string? Version { get; set; }
    }

    public class HeartbeatRequest
    {
        public string? Id { get; set; }
    }

    public class AuditRequest
    {
        public string? Action { get; set; }
        public string? Id { get; set; }
        public string? Uuid { get; set; }
        public int? ConnId { get; set; }
        public string? FromId { get; set; }
        public DateTimeOffset? ConnStart { get; set; }
        public DateTimeOffset? ConnEnd { get; set; }
        public string? SessionId { get; set; }
        public string? File { get; set; }
        public string? UserId { get; set; }
        public long? FileSize { get; set; }
        public int? Direction { get; set; }
    }
}