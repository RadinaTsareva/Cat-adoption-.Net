using Microsoft.EntityFrameworkCore;
using CatAdoption.Api.Models;

namespace CatAdoption.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
        
    }
    public DbSet<User> Users { get; set; }

}