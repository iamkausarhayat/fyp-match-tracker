using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FypMatchTracker.Data;
using FypMatchTracker.Models;

namespace FypMatchTracker.Controllers;

/// <summary>
/// Controller handling user authentication (Login, Register, Logout)
/// Uses ASP.NET Core Cookie Authentication and secure PasswordHasher.
/// </summary>
public class AccountController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly PasswordHasher<AppUser> _hasher;

    public AccountController(ApplicationDbContext context)
    {
        _context = context;
        _hasher = new PasswordHasher<AppUser>();
    }

    // ==========================================
    // GET: /Account/Login
    // ==========================================
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        // If already logged in, redirect based on role
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            if (User.IsInRole("Coordinator"))
                return RedirectToAction("Dashboard", "Coordinator");

            return RedirectToAction("Index", "Home");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    // ==========================================
    // POST: /Account/Login
    // ==========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ViewBag.ErrorMessage = "Please enter both email and password.";
            return View();
        }

        var normalizedEmail = email.Trim().ToLower();
        var user = await _context.Users
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);

        if (user == null)
        {
            ViewBag.ErrorMessage = "Invalid email or password. Please try again.";
            return View();
        }

        // Verify hashed password
        var verificationResult = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            ViewBag.ErrorMessage = "Invalid email or password. Please try again.";
            return View();
        }

        if (user.Status != "Active")
        {
            ViewBag.ErrorMessage = "Your account is inactive. Please contact the FYP Coordinator.";
            return View();
        }

        // Build authentication claims
        var claims = new List<System.Security.Claims.Claim>
        {
            new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new System.Security.Claims.Claim(ClaimTypes.Name, user.FullName),
            new System.Security.Claims.Claim(ClaimTypes.Email, user.Email),
            new System.Security.Claims.Claim(ClaimTypes.Role, user.Role)
        };

        if (user.DepartmentId.HasValue)
        {
            claims.Add(new System.Security.Claims.Claim("DepartmentId", user.DepartmentId.Value.ToString()));
            claims.Add(new System.Security.Claims.Claim("DepartmentName", user.Department?.Name ?? ""));
        }

        if (!string.IsNullOrEmpty(user.University))
        {
            claims.Add(new System.Security.Claims.Claim("University", user.University));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        // Redirect based on ReturnUrl or User Role
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        if (user.Role == "Coordinator")
        {
            return RedirectToAction("Dashboard", "Coordinator");
        }

        return RedirectToAction("Index", "Home");
    }

    // ==========================================
    // GET: /Account/GuestLogin
    // ==========================================
    [HttpGet]
    public async Task<IActionResult> GuestLogin(string? returnUrl)
    {
        var claims = new List<System.Security.Claims.Claim>
        {
            new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, "0"),
            new System.Security.Claims.Claim(ClaimTypes.Name, "Guest Student"),
            new System.Security.Claims.Claim(ClaimTypes.Email, "guest@numl.edu.pk"),
            new System.Security.Claims.Claim(ClaimTypes.Role, "Guest"),
            new System.Security.Claims.Claim("University", "NUML Islamabad")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    // ==========================================
    // GET: /Account/Register
    // ==========================================
    [HttpGet]
    public async Task<IActionResult> Register()
    {
        ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
        return View();
    }

    // ==========================================
    // POST: /Account/Register
    // ==========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string fullName, string university, string email, int departmentId, string password, string confirmPassword)
    {
        ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();

        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(university) ||
            string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ViewBag.ErrorMessage = "All fields are required.";
            return View();
        }

        if (password != confirmPassword)
        {
            ViewBag.ErrorMessage = "Passwords do not match.";
            return View();
        }

        if (password.Length < 6)
        {
            ViewBag.ErrorMessage = "Password must be at least 6 characters long.";
            return View();
        }

        var normalizedEmail = email.Trim().ToLower();

        // Check if email already registered
        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
        {
            ViewBag.ErrorMessage = "An account with this email already exists.";
            return View();
        }

        var newUser = new AppUser
        {
            FullName = fullName.Trim(),
            University = university.Trim(),
            Email = normalizedEmail,
            DepartmentId = departmentId,
            Role = "Student",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };

        newUser.PasswordHash = _hasher.HashPassword(newUser, password);

        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Registration successful! You can now log in with your credentials.";
        return RedirectToAction("Login");
    }

    // ==========================================
    // GET & POST: /Account/Logout
    // ==========================================
    [HttpGet]
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    // ==========================================
    // GET: /Account/AccessDenied
    // ==========================================
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
