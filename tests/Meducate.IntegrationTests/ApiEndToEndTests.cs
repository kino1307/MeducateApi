using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Meducate.API;
using Microsoft.AspNetCore.WebUtilities;

namespace Meducate.IntegrationTests;

public class ApiEndToEndTests(MeducateApiFactory factory) : IClassFixture<MeducateApiFactory>
{
    private static HttpClient NewClient(MeducateApiFactory factory) => factory.CreateClient();

    private static HttpClient NewClientWithCsrfHeader(MeducateApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Requested-By", "MeducateAPI");
        return client;
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await NewClient(factory).GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnversionedTopicsRoute_NoLongerExists()
    {
        // Regression check for the /api -> /api/v1 migration.
        var response = await NewClient(factory).GetAsync("/api/topics");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task VersionedTopicsRoute_WithDemoKey_ReturnsOkWithRateLimitHeaders()
    {
        var client = NewClient(factory);
        client.DefaultRequestHeaders.Add("X-Api-Key", ApiConstants.DemoRawKey);

        var response = await client.GetAsync("/api/v1/topics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-RateLimit-Limit"));
        Assert.True(response.Headers.Contains("X-RateLimit-Remaining"));
    }

    [Fact]
    public async Task MutatingRequest_WithoutCsrfHeaderOrApiKey_IsForbidden()
    {
        var response = await NewClient(factory).PostAsJsonAsync("/api/v1/waitlist", new { email = "csrf-check@example.com" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RegisterVerifyAndFetchSession_FullFlow()
    {
        var client = NewClientWithCsrfHeader(factory);
        var email = $"integration-{Guid.NewGuid():N}@example.com";

        var registerResponse = await client.PostAsJsonAsync("/api/v1/users/register", new { email });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var verifyUrl = factory.EmailService.LastVerificationUrl;
        Assert.NotNull(verifyUrl);
        var token = QueryHelpers.ParseQuery(new Uri(verifyUrl!).Query)["token"].ToString();
        Assert.NotEmpty(token);

        var verifyResponse = await client.PostAsJsonAsync("/api/v1/users/verify", new { token });
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        var authCookie = ExtractSetCookieValue(verifyResponse, "meducateapi_auth");
        Assert.NotNull(authCookie);

        var sessionClient = NewClientWithCsrfHeader(factory);
        sessionClient.DefaultRequestHeaders.Add("Cookie", authCookie);

        var meResponse = await sessionClient.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        using var meDoc = JsonDocument.Parse(await meResponse.Content.ReadAsStringAsync());
        Assert.Equal(email, meDoc.RootElement.GetProperty("email").GetString());
        Assert.True(meDoc.RootElement.GetProperty("isEmailVerified").GetBoolean());
    }

    [Fact]
    public async Task CreateOrganisationAndApiKey_FullFlow()
    {
        var client = NewClientWithCsrfHeader(factory);
        var email = $"integration-org-{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync("/api/v1/users/register", new { email });
        var token = QueryHelpers.ParseQuery(new Uri(factory.EmailService.LastVerificationUrl!).Query)["token"].ToString();
        var verifyResponse = await client.PostAsJsonAsync("/api/v1/users/verify", new { token });
        var authCookie = ExtractSetCookieValue(verifyResponse, "meducateapi_auth")!;

        var sessionClient = NewClientWithCsrfHeader(factory);
        sessionClient.DefaultRequestHeaders.Add("Cookie", authCookie);

        var orgResponse = await sessionClient.PostAsJsonAsync("/api/v1/orgs", new { organisationName = "Integration Test Org" });
        Assert.Equal(HttpStatusCode.Created, orgResponse.StatusCode);
        var orgId = (await orgResponse.Content.ReadAsStringAsync()).Trim('"');

        var keyResponse = await sessionClient.PostAsJsonAsync($"/api/v1/orgs/{orgId}/keys", new { name = "Test Key" });
        Assert.Equal(HttpStatusCode.Created, keyResponse.StatusCode);

        using var keyDoc = JsonDocument.Parse(await keyResponse.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(keyDoc.RootElement.GetProperty("apiKey").GetString()));
    }

    private static string? ExtractSetCookieValue(HttpResponseMessage response, string cookieName)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
            return null;

        foreach (var cookie in cookies)
        {
            if (cookie.StartsWith(cookieName + "=", StringComparison.OrdinalIgnoreCase))
                return cookie[..cookie.IndexOf(';')];
        }

        return null;
    }
}
