using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RecipeService.Dtos;
using RecipeService.Services;
using Xunit;

namespace RecipeService.Tests.Controllers;

// Exercises the real [Authorize(Policy = AdminOnly)] pipeline end-to-end via an in-process
// TestServer, not the "construct the controller directly" pattern every other controller test in
// this repo uses -- that pattern calls the action method straight from the test, bypassing
// ASP.NET Core's authorization middleware entirely, so a non-admin-rejection test for ANY
// AdminOnly endpoint didn't previously exist. See TestAuthHandler for how admin/non-admin is
// faked without a real Auth0 token.
public class ImagesControllerAuthorizationTests : IClassFixture<ImagesControllerAuthorizationTests.TestFactory>
{
    private readonly TestFactory _factory;

    public ImagesControllerAuthorizationTests(TestFactory factory)
    {
        _factory = factory;
    }

    private static HttpRequestMessage MakeUploadRequest()
    {
        var jpegBytes = new byte[64];
        jpegBytes[0] = 0xFF;
        jpegBytes[1] = 0xD8;
        jpegBytes[2] = 0xFF;

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(jpegBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "file", "photo.jpg");

        return new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = content };
    }

    [Fact]
    public async Task Upload_WithoutAdminRole_Returns403()
    {
        var client = _factory.CreateClient();

        var response = await client.SendAsync(MakeUploadRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Upload_WithAdminRole_ReturnsOkWithUploadedUrl()
    {
        var client = _factory.CreateClient();
        var request = MakeUploadRequest();
        request.Headers.Add("X-Test-Admin", "true");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ImageUploadResponse>();
        body!.Url.Should().Be(TestFactory.FakeUploadedUrl);
    }

    public class TestFactory : WebApplicationFactory<Program>
    {
        public const string FakeUploadedUrl = "https://res.cloudinary.com/demo/image/upload/v1/test.jpg";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Dummy values so the app host starts without real Auth0 credentials or a live
            // Postgres -- nothing in this flow ever touches the DbContext, and authentication
            // itself is replaced below.
            builder.UseSetting("Auth0:Domain", "test.auth0.local");
            builder.UseSetting("Auth0:Audience", "https://test.api");
            builder.UseSetting(
                "ConnectionStrings:RecipeDb", "Host=localhost;Database=test;Username=test;Password=test");

            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                });

                // Never a real Cloudinary call -- same fake-upload-service substitution
                // ImagesServiceTests uses, just wired through DI here instead of constructed
                // directly.
                var mockUploadService = new Mock<IImageUploadService>();
                mockUploadService
                    .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(FakeUploadedUrl);
                services.AddScoped<IImageUploadService>(_ => mockUploadService.Object);
            });
        }
    }
}
