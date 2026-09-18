using System.Security.Claims;
using CatAdoption.Api.Seeding;

namespace CatAdoption.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = UserRoles.Admin)]
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AdminController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost("seed-cats")]
    public async Task<IActionResult> SeedCats()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var adminUserId))
        {
            return Unauthorized();
        }

        var admin = await _context.Users.FirstOrDefaultAsync(u => u.Id == adminUserId && u.Role == UserRoles.Admin);
        if (admin == null)
        {
            return Forbid();
        }

        var startIndex = await _context.Cats.MaxAsync(c => (int?)c.Id) ?? 0;
        var cats = AppSeedData.CreateGeneratedCats(admin.Id, startIndex, 10);
        _context.Cats.AddRange(cats);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "10 cats were created successfully.",
            createdCount = cats.Count
        });
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _context.Users
            .AsNoTracking()
            .OrderBy(u => u.Role)
            .ThenBy(u => u.FirstName)
            .Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                u.Role,
                u.City,
                u.CreatedAt
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpPut("users/{id:int}/role")]
    public async Task<IActionResult> UpdateUserRole(int id, UpdateUserRoleRequest request)
    {
        var newRole = NormalizeRole(request.Role);
        if (newRole == null)
        {
            return BadRequest(new { message = "Invalid role." });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        if (user.Role == UserRoles.Admin)
        {
            return BadRequest(new { message = "Admin role cannot be changed." });
        }

        user.Role = newRole;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "User role updated successfully.",
            userId = user.Id,
            role = user.Role
        });
    }

    [HttpDelete("users/{id:int}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var currentUserId))
        {
            return Unauthorized();
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        if (user.Role == UserRoles.Admin)
        {
            return BadRequest(new { message = "Admin users cannot be deleted." });
        }

        if (user.Id == currentUserId)
        {
            return BadRequest(new { message = "You cannot delete your own account from the admin panel." });
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "User deleted successfully.",
            userId = user.Id
        });
    }

    private static string? NormalizeRole(string? requestedRole)
    {
        if (string.Equals(requestedRole, UserRoles.CareGiver, StringComparison.OrdinalIgnoreCase))
        {
            return UserRoles.CareGiver;
        }

        if (string.Equals(requestedRole, UserRoles.PetAdopter, StringComparison.OrdinalIgnoreCase))
        {
            return UserRoles.PetAdopter;
        }

        return null;
    }
}


