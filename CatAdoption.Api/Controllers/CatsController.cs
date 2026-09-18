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
    public async Task<IActionResult> CreateCat(CreateCatRequest request)
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

        var cat = new Cat
        {
            Name = request.Name,
            Age = request.Age,
            Sex = request.Sex,
            Color = request.Color,
            Description = request.Description,
            Location = request.Location,
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
    public async Task<IActionResult> GetCats()
    {
        var cats = await _context.Cats
            .AsNoTracking()
            .Include(cat => cat.User)
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
                Owner = new
                {
                    cat.User.Id,
                    cat.User.FirstName,
                    cat.User.LastName
                }
            })
            .ToListAsync();

        return Ok(cats);
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

        if (cat.UserId != userId)
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

        if (cat.UserId != userId)
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