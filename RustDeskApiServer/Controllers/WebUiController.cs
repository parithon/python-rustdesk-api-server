using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RustDeskApiServer.Models;

namespace RustDeskApiServer.Controllers;

[Route("webui")]
[Authorize]
public class WebUiController(
    UserManager<UserProfile> userManager,
    IConfiguration configuration) : Controller
{
    [HttpGet]
    [HttpGet("{**path}")]
    public async Task<IActionResult> Index()
    {
        var idServer = configuration.GetValue("IdServer", "");
        if (string.IsNullOrEmpty(idServer))
            idServer = Request.Host.Host;

        ViewBag.Domain = idServer;
        return View("WebUi");
    }
}
