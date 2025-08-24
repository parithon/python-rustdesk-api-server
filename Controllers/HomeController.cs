using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RustDeskApiServer.Data;
using RustDeskApiServer.Models;
using System.Security.Cryptography;
using System.Text;

namespace RustDeskApiServer.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<UserProfile> _userManager;
        private readonly SignInManager<UserProfile> _signInManager;
        private readonly IConfiguration _configuration;

        public HomeController(
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

        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Dashboard");
            }
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Dashboard");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Please enter username and password";
                return View();
            }

            var user = await _userManager.FindByNameAsync(username);
            if (user == null)
            {
                ViewBag.Error = "Invalid username or password";
                return View();
            }

            var result = await _signInManager.PasswordSignInAsync(user, password, true, false);
            if (result.Succeeded)
            {
                return RedirectToAction("Dashboard");
            }

            ViewBag.Error = "Invalid username or password";
            return View();
        }

        [HttpGet]
        public IActionResult Register()
        {
            var allowRegistration = _configuration.GetValue<bool>("AllowRegistration", true);
            if (!allowRegistration)
            {
                return NotFound();
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(string username, string password, string confirmPassword)
        {
            var allowRegistration = _configuration.GetValue<bool>("AllowRegistration", true);
            if (!allowRegistration)
            {
                return NotFound();
            }

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Please enter username and password";
                return View();
            }

            if (password != confirmPassword)
            {
                ViewBag.Error = "Passwords do not match";
                return View();
            }

            if (password.Length < 8 || password.Length > 20)
            {
                ViewBag.Error = "Password must be 8-20 characters long";
                return View();
            }

            var existingUser = await _userManager.FindByNameAsync(username);
            if (existingUser != null)
            {
                ViewBag.Error = "Username already exists";
                return View();
            }

            var user = new UserProfile
            {
                UserName = username,
                Email = username + "@rustdesk.local" // dummy email
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                ViewBag.Success = "Registration successful. Please go to login page.";
                return View();
            }

            ViewBag.Error = string.Join(", ", result.Errors.Select(e => e.Description));
            return View();
        }

        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }

        public async Task<IActionResult> Dashboard()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login");
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login");
            }

            var devices = await _context.RustDeskDevices.ToListAsync();
            var peers = await _context.RustDeskPeers
                .Where(p => p.UserId == currentUser.Id)
                .ToListAsync();

            var model = new DashboardViewModel
            {
                User = currentUser,
                Devices = devices,
                Peers = peers,
                IsAdmin = currentUser.IsAdmin
            };

            return View(model);
        }

        public async Task<IActionResult> Devices(int page = 1)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login");
            }

            var pageSize = 20;
            var devices = await _context.RustDeskDevices
                .OrderByDescending(d => d.UpdateTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var totalCount = await _context.RustDeskDevices.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.HasPreviousPage = page > 1;
            ViewBag.HasNextPage = page < totalPages;

            return View(devices);
        }

        public async Task<IActionResult> ConnectionLogs(int page = 1)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login");
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !currentUser.IsAdmin)
            {
                return Forbid();
            }

            var pageSize = 20;
            var logs = await _context.ConnectionLogs
                .OrderByDescending(l => l.ConnectionStart)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var totalCount = await _context.ConnectionLogs.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.HasPreviousPage = page > 1;
            ViewBag.HasNextPage = page < totalPages;

            return View(logs);
        }

        public async Task<IActionResult> FileLogs(int page = 1)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login");
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !currentUser.IsAdmin)
            {
                return Forbid();
            }

            var pageSize = 20;
            var logs = await _context.FileLogs
                .OrderByDescending(l => l.LoggedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var totalCount = await _context.FileLogs.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.HasPreviousPage = page > 1;
            ViewBag.HasNextPage = page < totalPages;

            return View(logs);
        }
    }

    public class DashboardViewModel
    {
        public UserProfile User { get; set; } = new();
        public List<RustDeskDevice> Devices { get; set; } = new();
        public List<RustDeskPeer> Peers { get; set; } = new();
        public bool IsAdmin { get; set; }
    }
}