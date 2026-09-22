using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using CatAdoption.Api.Data;
using CatAdoption.Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CatAdoption.Api.Tests;

public class ChatEndpointsTests
{
    private const int CareGiverUserId = 11;
    private const int PetAdopterUserId = 12;

    [Fact]
    public async Task Chat_endpoints_allow_pet_adopter_and_care_giver_to_message_each_other()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("environment", "Test");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                    services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<ApplicationDbContext>>();
                    services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<DbContext>>();
                    services.AddDbContext<ApplicationDbContext>(options =>
                        options.UseSqlite(connection));

                    services.AddAuthentication("Test")
                        .AddScheme<AuthenticationSchemeOptions, TestChatAuthenticationHandler>("Test", _ => { });
                });
            });

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();

        db.Users.AddRange(
            new User
            {
                Id = CareGiverUserId,
                FirstName = "Jane",
                LastName = "Care",
                Email = "jane@example.com",
                PasswordHash = "hash",
                Role = UserRoles.CareGiver
            },
            new User
            {
                Id = PetAdopterUserId,
                FirstName = "Alex",
                LastName = "Adopter",
                Email = "alex@example.com",
                PasswordHash = "hash",
                Role = UserRoles.PetAdopter
            });
        await db.SaveChangesAsync();

        TestChatAuthenticationHandler.CurrentUserId = PetAdopterUserId;
        TestChatAuthenticationHandler.CurrentRole = UserRoles.PetAdopter;
        TestChatAuthenticationHandler.CurrentName = "Alex Adopter";

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var contactsResponse = await client.GetAsync("/api/messages/contacts");
        Assert.Equal(HttpStatusCode.OK, contactsResponse.StatusCode);
        var contactsJson = await contactsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var contacts = contactsJson.Deserialize<JsonElement[]>();
        Assert.NotNull(contacts);
        Assert.Single(contacts);
        Assert.Equal(CareGiverUserId, contacts[0].GetProperty("id").GetInt32());
        Assert.Equal(UserRoles.CareGiver, contacts[0].GetProperty("role").GetString());

        var sendResponse = await client.PostAsJsonAsync("/api/messages", new
        {
            receiverId = CareGiverUserId,
            content = "Hello! I would like to ask about the cat."
        });

        Assert.Equal(HttpStatusCode.OK, sendResponse.StatusCode);
        var sendJson = await sendResponse.Content.ReadFromJsonAsync<JsonElement>();
        var conversationId = sendJson.GetProperty("conversationId").GetInt32();
        Assert.True(conversationId > 0);

        var conversationsResponse = await client.GetAsync("/api/messages/conversations");
        Assert.Equal(HttpStatusCode.OK, conversationsResponse.StatusCode);
        var conversationsJson = await conversationsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var conversations = conversationsJson.Deserialize<JsonElement[]>();
        Assert.NotNull(conversations);
        Assert.Single(conversations);
        Assert.Equal(conversationId, conversations[0].GetProperty("conversationId").GetInt32());
        Assert.Equal(CareGiverUserId, conversations[0].GetProperty("otherUserId").GetInt32());

        TestChatAuthenticationHandler.CurrentUserId = CareGiverUserId;
        TestChatAuthenticationHandler.CurrentRole = UserRoles.CareGiver;
        TestChatAuthenticationHandler.CurrentName = "Jane Care";

        var conversationResponse = await client.GetAsync($"/api/messages/conversations/{conversationId}");
        Assert.Equal(HttpStatusCode.OK, conversationResponse.StatusCode);
        var conversationJson = await conversationResponse.Content.ReadFromJsonAsync<JsonElement>();
        var messages = conversationJson.GetProperty("messages").Deserialize<JsonElement[]>();
        Assert.NotNull(messages);
        Assert.Single(messages);
        Assert.Equal("Hello! I would like to ask about the cat.", messages[0].GetProperty("content").GetString());
        Assert.True(messages[0].GetProperty("isRead").GetBoolean());

        var readMessageId = messages[0].GetProperty("id").GetInt32();
        var markAsReadResponse = await client.PutAsync($"/api/messages/{readMessageId}/read", null);
        Assert.Equal(HttpStatusCode.OK, markAsReadResponse.StatusCode);

        var conversationsAfterReadResponse = await client.GetAsync("/api/messages/conversations");
        Assert.Equal(HttpStatusCode.OK, conversationsAfterReadResponse.StatusCode);
        var conversationsAfterReadJson = await conversationsAfterReadResponse.Content.ReadFromJsonAsync<JsonElement>();
        var conversationsAfterRead = conversationsAfterReadJson.Deserialize<JsonElement[]>();
        Assert.NotNull(conversationsAfterRead);
        Assert.Single(conversationsAfterRead);
        Assert.Equal(0, conversationsAfterRead[0].GetProperty("unreadCount").GetInt32());
    }

    private sealed class TestChatAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public static int CurrentUserId { get; set; }
        public static string CurrentRole { get; set; } = UserRoles.PetAdopter;
        public static string CurrentName { get; set; } = "Test User";

        public TestChatAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, CurrentUserId.ToString()),
                new Claim(ClaimTypes.Role, CurrentRole),
                new Claim(ClaimTypes.Name, CurrentName)
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}

