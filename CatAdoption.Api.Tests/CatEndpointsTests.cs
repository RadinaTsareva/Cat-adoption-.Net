using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using CatAdoption.Api.Data;
using CatAdoption.Api.Models;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CatAdoption.Api.Tests;

public class CatEndpointsTests
{
    private const int OwnerUserId = 1;

    [Fact]
    public async Task Post_Get_Put_Delete_Cat_endpoints_work_end_to_end()
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
                        .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
                });
            });

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();

        var owner = new User
        {
            Id = OwnerUserId,
            FirstName = "Mia",
            LastName = "Owner",
            Email = "mia@example.com",
            PasswordHash = "hash",
            Role = UserRoles.Admin
        };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var postContent = new MultipartFormDataContent
        {
            { new StringContent("Shadow"), "name" },
            { new StringContent("2"), "age" },
            { new StringContent("Female"), "sex" },
            { new StringContent("Black"), "color" },
            { new StringContent("Playful"), "description" },
            { new StringContent("Varna"), "location" },
            { new StringContent(CatStatuses.InProgress), "status" }
        };

        var postResponse = await client.PostAsync("/api/cats", postContent);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        var created = await postResponse.Content.ReadFromJsonAsync<JsonElement>();
        var createdCatId = created.GetProperty("catId").GetInt32();

        var listResponse = await client.GetAsync("/api/cats");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listJson = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        var listData = listJson.GetProperty("data").Deserialize<JsonElement[]>();
        Assert.NotNull(listData);
        Assert.Single(listData);
        Assert.Equal("Shadow", listData[0].GetProperty("name").GetString());
        Assert.Equal(CatStatuses.InProgress, listData[0].GetProperty("status").GetString());

        var filteredResponse = await client.GetAsync("/api/cats?sex=Female&color=Black&status=in-progress&city=Varna");
        Assert.Equal(HttpStatusCode.OK, filteredResponse.StatusCode);
        var filteredJson = await filteredResponse.Content.ReadFromJsonAsync<JsonElement>();
        var filteredData = filteredJson.GetProperty("data").Deserialize<JsonElement[]>();
        Assert.NotNull(filteredData);
        Assert.Single(filteredData);
        Assert.Equal(createdCatId, filteredData[0].GetProperty("id").GetInt32());

        var waitingFilteredResponse = await client.GetAsync("/api/cats?status=waiting-adoption");
        Assert.Equal(HttpStatusCode.OK, waitingFilteredResponse.StatusCode);
        var waitingFilteredJson = await waitingFilteredResponse.Content.ReadFromJsonAsync<JsonElement>();
        var waitingFilteredData = waitingFilteredJson.GetProperty("data").Deserialize<JsonElement[]>();
        Assert.NotNull(waitingFilteredData);
        Assert.Empty(waitingFilteredData);

        var getResponse = await client.GetAsync($"/api/cats/{createdCatId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var getJson = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(CatStatuses.InProgress, getJson.GetProperty("status").GetString());

        using var imageContent = new ByteArrayContent(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO0B9y0AAAAASUVORK5CYII="));
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");

        using var putContent = new MultipartFormDataContent
        {
            { new StringContent("Updated"), "name" },
            { new StringContent("4"), "age" },
            { new StringContent("Male"), "sex" },
            { new StringContent("Cream"), "color" },
            { new StringContent("Still friendly"), "description" },
            { new StringContent("Plovdiv"), "location" },
            { new StringContent(CatStatuses.Adopted), "status" },
            { imageContent, "image", "cat.png" }
        };

        var putResponse = await client.PutAsync($"/api/cats/{createdCatId}", putContent);
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var updatedListResponse = await client.GetAsync("/api/cats");
        Assert.Equal(HttpStatusCode.OK, updatedListResponse.StatusCode);
        var updatedListJson = await updatedListResponse.Content.ReadFromJsonAsync<JsonElement>();
        var updatedListData = updatedListJson.GetProperty("data").Deserialize<JsonElement[]>();
        Assert.NotNull(updatedListData);
        Assert.Single(updatedListData);
        Assert.Equal(CatStatuses.Adopted, updatedListData[0].GetProperty("status").GetString());
        Assert.StartsWith("data:image/png;base64,", updatedListData[0].GetProperty("imageUrl").GetString());

        var adoptedFilteredResponse = await client.GetAsync("/api/cats?status=adopted&city=Plovdiv");
        Assert.Equal(HttpStatusCode.OK, adoptedFilteredResponse.StatusCode);
        var adoptedFilteredJson = await adoptedFilteredResponse.Content.ReadFromJsonAsync<JsonElement>();
        var adoptedFilteredData = adoptedFilteredJson.GetProperty("data").Deserialize<JsonElement[]>();
        Assert.NotNull(adoptedFilteredData);
        Assert.Single(adoptedFilteredData);
        Assert.Equal(createdCatId, adoptedFilteredData[0].GetProperty("id").GetInt32());

        var deleteResponse = await client.DeleteAsync($"/api/cats/{createdCatId}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var seedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/seed-cats");
        var seedResponse = await client.SendAsync(seedRequest);
        Assert.Equal(HttpStatusCode.OK, seedResponse.StatusCode);

        var seededListResponse = await client.GetAsync("/api/cats");
        Assert.Equal(HttpStatusCode.OK, seededListResponse.StatusCode);
        var seededListJson = await seededListResponse.Content.ReadFromJsonAsync<JsonElement>();
        var seededListData = seededListJson.GetProperty("data").Deserialize<JsonElement[]>();
        Assert.NotNull(seededListData);
        Assert.Equal(10, seededListData.Length);

        var seededWaitingResponse = await client.GetAsync("/api/cats?status=waiting-adoption");
        Assert.Equal(HttpStatusCode.OK, seededWaitingResponse.StatusCode);
        var seededWaitingJson = await seededWaitingResponse.Content.ReadFromJsonAsync<JsonElement>();
        var seededWaitingData = seededWaitingJson.GetProperty("data").Deserialize<JsonElement[]>();
        Assert.NotNull(seededWaitingData);
        Assert.Equal(10, seededWaitingData.Length);

        await db.Database.EnsureDeletedAsync();
    }

    private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthenticationHandler(
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
                new Claim(ClaimTypes.NameIdentifier, OwnerUserId.ToString()),
                new Claim(ClaimTypes.Role, UserRoles.Admin),
                new Claim(ClaimTypes.Name, "test-user")
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
