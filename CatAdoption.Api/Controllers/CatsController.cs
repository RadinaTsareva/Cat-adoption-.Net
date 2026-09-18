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
    public async Task<IActionResult> CreateCat()
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

        var name = Request.Form["name"];
        var ageStr = Request.Form["age"];
        var sex = Request.Form["sex"];
        var color = Request.Form["color"];
        var description = Request.Form["description"];
        var location = Request.Form["location"];
        var imageFile = Request.Form.Files["image"];

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(ageStr) || string.IsNullOrEmpty(sex))
        {
            return BadRequest(new { message = "Missing required fields" });
        }

        if (!int.TryParse(ageStr, out var age))
        {
            return BadRequest(new { message = "Invalid age" });
        }

        string? imageUrl = null;
        if (imageFile != null && imageFile.Length > 0)
        {
            // For now, just create a base64 or store as URL placeholder
            // In production, you'd upload to S3, Azure, or similar
            using (var ms = new MemoryStream())
            {
                await imageFile.CopyToAsync(ms);
                var fileBytes = ms.ToArray();
                imageUrl = $"data:{imageFile.ContentType};base64,{Convert.ToBase64String(fileBytes)}";
            }
        }

        var cat = new Cat
        {
            Name = name.ToString(),
            Age = age,
            Sex = sex.ToString(),
            Color = color.ToString(),
            Description = description.ToString(),
            Location = location.ToString(),
            ImageUrl = imageUrl,
            UserId = userId
        };

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
    public async Task<IActionResult> GetCats([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var skip = (page - 1) * pageSize;

        var totalCount = await _context.Cats.CountAsync();
        var cats = await _context.Cats
            .AsNoTracking()
            .Include(cat => cat.User)
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
                    cat.User.LastName
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
            Owner = new
            {
                cat.User.Id,
                cat.User.FirstName,
                cat.User.LastName
            }
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateCat(int id, UpdateCatRequest request)
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

        cat.Name = request.Name;
        cat.Age = request.Age;
        cat.Sex = request.Sex;
        cat.Color = request.Color;
        cat.Description = request.Description;
        cat.Location = request.Location;

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
}