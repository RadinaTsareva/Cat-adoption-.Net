using Microsoft.EntityFrameworkCore;

namespace CatAdoption.Api.Seeding;

public static class AppSeedData
{
    private static readonly string[] InitialCatNames =
    {
        "Whiskers", "Luna", "Mittens", "Shadow", "Tiger", "Smokey", "Ginger", "Patch", "Buddy", "Max",
        "Charlie", "Simba", "Felix", "Toby", "Milo", "Oliver", "Oscar", "Jasper", "Coco", "Bella"
    };

    private static readonly string[] Colors =
    {
        "Orange", "Black", "White", "Gray", "Brown", "Tabby", "Calico", "Siamese", "Persian", "Bengal"
    };

    private static readonly string[] Locations =
    {
        "Sofia", "Plovdiv", "Varna", "Burgas", "Ruse", "Stara Zagora", "Sliven", "Pleven"
    };

    private static readonly string[] Descriptions =
    {
        "Friendly and playful kitten, loves to cuddle",
        "Independent cat, perfect for an experienced owner",
        "Very social and energetic, needs active playtime",
        "Quiet and calm, ideal for apartments",
        "Curious explorer, loves climbing trees",
        "Affectionate lap cat, loves attention",
        "Shy at first but warms up quickly",
        "Adventurous spirit, loves outdoor activities",
        "Lazy and relaxed, perfect companion",
        "Clever and trainable, learns tricks quickly"
    };

    public static async Task EnsureSeedUsersAsync(ApplicationDbContext db)
    {
        await UpsertUserAsync(
            db,
            "john@example.com",
            "John",
            "Doe",
            "Sofia",
            UserRoles.Admin,
            "password123");

        await UpsertUserAsync(
            db,
            "jane@example.com",
            "Jane",
            "Smith",
            "Plovdiv",
            UserRoles.CareGiver,
            "password456");

        await UpsertUserAsync(
            db,
            "alex@example.com",
            "Alex",
            "Petrov",
            "Varna",
            UserRoles.PetAdopter,
            "password789");

        await db.SaveChangesAsync();
    }

    public static async Task EnsureInitialCatsAsync(ApplicationDbContext db)
    {
        if (await db.Cats.AnyAsync())
        {
            return;
        }

        var admin = await db.Users.FirstAsync(u => u.Email == "john@example.com");
        var careGiver = await db.Users.FirstAsync(u => u.Email == "jane@example.com");

        var cats = CreateInitialCats(admin.Id, careGiver.Id);
        db.Cats.AddRange(cats);
        await db.SaveChangesAsync();
    }

    public static IReadOnlyList<Cat> CreateInitialCats(int adminUserId, int careGiverUserId)
    {
        var cats = new List<Cat>(InitialCatNames.Length);

        for (int i = 0; i < InitialCatNames.Length; i++)
        {
            cats.Add(BuildCat(
                name: InitialCatNames[i],
                age: (i % 5) + 1,
                sex: i % 2 == 0 ? "Male" : "Female",
                color: Colors[i % Colors.Length],
                description: Descriptions[i % Descriptions.Length],
                location: Locations[i % Locations.Length],
                ownerId: i < 10 ? adminUserId : careGiverUserId));
        }

        return cats;
    }

    public static IReadOnlyList<Cat> CreateGeneratedCats(int ownerId, int startIndex, int count = 10)
    {
        var cats = new List<Cat>(count);

        for (int i = 0; i < count; i++)
        {
            var sequenceNumber = startIndex + i + 1;

            cats.Add(BuildCat(
                name: $"Admin Seed Cat {sequenceNumber}",
                age: (sequenceNumber % 5) + 1,
                sex: sequenceNumber % 2 == 0 ? "Male" : "Female",
                color: Colors[sequenceNumber % Colors.Length],
                description: Descriptions[sequenceNumber % Descriptions.Length],
                location: Locations[sequenceNumber % Locations.Length],
                ownerId: ownerId));
        }

        return cats;
    }

    private static async Task UpsertUserAsync(
        ApplicationDbContext db,
        string email,
        string firstName,
        string lastName,
        string city,
        string role,
        string password)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        var hasher = new PasswordHasher<User>();

        if (user == null)
        {
            user = new User
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                City = city,
                Role = role
            };
            db.Users.Add(user);
        }

        user.FirstName = firstName;
        user.LastName = lastName;
        user.City = city;
        user.Role = role;
        user.PasswordHash = hasher.HashPassword(user, password);
    }

    private static Cat BuildCat(
        string name,
        int age,
        string sex,
        string color,
        string description,
        string location,
        int ownerId)
    {
        return new Cat
        {
            Name = name,
            Age = age,
            Sex = sex,
            Color = color,
            Description = description,
            Location = location,
            Status = CatStatuses.WaitingAdoption,
            UserId = ownerId
        };
    }
}


