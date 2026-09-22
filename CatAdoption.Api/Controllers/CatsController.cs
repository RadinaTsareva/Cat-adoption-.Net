using System.Security.Claims;
using CatAdoption.Api.Data;
using CatAdoption.Api.DTOs;
using CatAdoption.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatAdoption.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CatsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CatsController(ApplicationDbContext context)
    {
        _context = context;
    }


    [HttpPost]
    [Authorize(Roles = UserRoles.Admin + "," + UserRoles.CareGiver)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateCat([FromForm] CreateCatRequest request)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userIdClaim == null)
        {
            return Unauthorized();
        }

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Sex))
        {
            return BadRequest(new { message = "Missing required fields" });
        }

        if (!CatStatuses.TryNormalize(request.Status, out var status))
        {
            return BadRequest(new { message = "Invalid status" });
        }

        var imageUrl = await ConvertImageToDataUrlAsync(request.Image);

        var cat = new Cat
        {
            Name = request.Name?.Trim() ?? string.Empty,
            Age = request.Age,
            Sex = request.Sex?.Trim() ?? string.Empty,
            Color = request.Color?.Trim() ?? string.Empty,
            Description = request.Description?.Trim() ?? string.Empty,
            Location = request.Location?.Trim() ?? string.Empty,
            Status = status,
            ImageUrl = imageUrl,
            UserId = userId
        };

        if (cat.Age <= 0)
        {
            return BadRequest(new { message = "Invalid age" });
        }

        _context.Cats.Add(cat);

        await _context.SaveChangesAsync();

        return Created("", new
        {
            message = "Cat listing created successfully.",
            catId = cat.Id
        });
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetCats(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sex = null,
        [FromQuery] string? color = null,
        [FromQuery] string? status = null,
        [FromQuery] string? city = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var skip = (page - 1) * pageSize;
        var normalizedStatus = string.Empty;

        if (!string.IsNullOrWhiteSpace(status) && !CatStatuses.TryNormalize(status, out normalizedStatus))
        {
            return BadRequest(new { message = "Invalid status" });
        }

        var query = _context.Cats
            .AsNoTracking()
            .Include(cat => cat.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(sex))
        {
            var sexFilter = sex.Trim().ToLowerInvariant();
            query = query.Where(cat => cat.Sex.ToLower() == sexFilter);
        }

        if (!string.IsNullOrWhiteSpace(color))
        {
            var colorFilter = color.Trim().ToLowerInvariant();
            query = query.Where(cat => cat.Color.ToLower().Contains(colorFilter));
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            var cityFilter = city.Trim().ToLowerInvariant();
            query = query.Where(cat => cat.Location.ToLower().Contains(cityFilter));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = normalizedStatus switch
            {
                CatStatuses.WaitingAdoption => query.Where(cat =>
                    cat.Status.ToLower() == CatStatuses.WaitingAdoption ||
                    cat.Status.ToLower() == "available"),
                CatStatuses.InProgress => query.Where(cat =>
                    cat.Status.ToLower() == CatStatuses.InProgress ||
                    cat.Status.ToLower() == "in progress" ||
                    cat.Status.ToLower() == "in process of adoption"),
                CatStatuses.Adopted => query.Where(cat => cat.Status.ToLower() == CatStatuses.Adopted),
                _ => query
            };
        }

        var totalCount = await query.CountAsync();
        var cats = await query
            .OrderByDescending(c => c.Id)
            .Skip(skip)
            .Take(pageSize)
            .Select(cat => new
            {
                cat.Id,
                cat.Name,
                cat.Age,
                cat.Sex,
                cat.Color,
                cat.Description,
                cat.Location,
                cat.Status,
                cat.ImageUrl,
                Owner = new
                {
                    cat.User.Id,
                    cat.User.FirstName,
                    cat.User.LastName,
                    cat.User.Role
                }
            })
            .ToListAsync();

        return Ok(new
        {
            data = cats,
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCat(int id)
    {
        var cat = await _context.Cats
            .AsNoTracking()
            .Include(cat => cat.User)
            .FirstOrDefaultAsync(cat => cat.Id == id);

        if (cat == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            cat.Id,
            cat.Name,
            cat.Age,
            cat.Sex,
            cat.Color,
            cat.Description,
            cat.Location,
            cat.Status,
            cat.ImageUrl,
            Owner = new
            {
                cat.User.Id,
                cat.User.FirstName,
                cat.User.LastName,
                cat.User.Role
            }
        });
    }

    [HttpPut("{id:int}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateCat(int id, [FromForm] UpdateCatRequest request)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userIdClaim == null)
        {
            return Unauthorized();
        }

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var cat = await _context.Cats
            .FirstOrDefaultAsync(cat => cat.Id == id);

        if (cat == null)
        {
            return NotFound();
        }

        if (cat.UserId != userId && !User.IsInRole(UserRoles.Admin))
        {
            return Forbid();
        }

        var status = cat.Status;
        if (!string.IsNullOrWhiteSpace(request.Status) && !CatStatuses.TryNormalize(request.Status, out status))
        {
            return BadRequest(new { message = "Invalid status" });
        }


        var imageUrl = cat.ImageUrl;
        if (request.Image != null && request.Image.Length > 0)
        {
            imageUrl = await ConvertImageToDataUrlAsync(request.Image);
        }

        cat.Name = request.Name?.Trim() ?? string.Empty;
        cat.Age = request.Age;
        cat.Sex = request.Sex?.Trim() ?? string.Empty;
        cat.Color = request.Color?.Trim() ?? string.Empty;
        cat.Description = request.Description?.Trim() ?? string.Empty;
        cat.Location = request.Location?.Trim() ?? string.Empty;
        cat.Status = status;
        cat.ImageUrl = imageUrl;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Cat listing updated successfully.",
            catId = cat.Id
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteCat(int id)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userIdClaim == null)
        {
            return Unauthorized();
        }

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var cat = await _context.Cats
            .FirstOrDefaultAsync(cat => cat.Id == id);

        if (cat == null)
        {
            return NotFound();
        }

        if (cat.UserId != userId && !User.IsInRole(UserRoles.Admin))
        {
            return Forbid();
        }

        _context.Cats.Remove(cat);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Cat listing deleted successfully.",
            catId = cat.Id
        });
    }

    private static async Task<string?> ConvertImageToDataUrlAsync(IFormFile? imageFile)
    {
        if (imageFile == null || imageFile.Length == 0)
        {
            return null;
        }

        using var ms = new MemoryStream();
        await imageFile.CopyToAsync(ms);
        var fileBytes = ms.ToArray();
        return $"data:{imageFile.ContentType};base64,{Convert.ToBase64String(fileBytes)}";
    }
}