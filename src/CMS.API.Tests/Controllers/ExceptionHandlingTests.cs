using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.API.Infrastructure;
using CMS.API.Tests.Infrastructure;

namespace CMS.API.Tests.Controllers;

/// <summary>
/// End-to-end tests for the global exception middleware, through the real pipeline via
/// <see cref="ExceptionHandlingTestFactory"/> (DB-free — the only substituted dependency is the signing
/// key source). They prove a thrown exception becomes ONE safe 500, while the deliberate 401 / 403 / 400
/// responses are left exactly as they were.
/// </summary>
public sealed class ExceptionHandlingTests : IClassFixture<ExceptionHandlingTestFactory>
{
    private readonly ExceptionHandlingTestFactory _factory;

    public ExceptionHandlingTests(ExceptionHandlingTestFactory factory) => _factory = factory;

    private HttpClient AuthenticatedClient()
    {
        var token = JwtTokenGenerator.Generate(
            ExceptionHandlingTestFactory.SigningKey, "tester", "Tester", new[] { "User" });
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ---------- a throwing endpoint → one safe 500 ----------

    [Fact]
    public async Task ThrowingEndpoint_Returns500_WithGenericMessage()
    {
        var response = await AuthenticatedClient().GetAsync("/test-diagnostics/throw");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.NotNull(body);
        Assert.Equal(GlobalExceptionMiddleware.GenericMessage, body!.Message);
    }

    [Fact]
    public async Task ThrowingEndpoint_Never_LeaksStackTraceOrSql()
    {
        var response = await AuthenticatedClient().GetAsync("/test-diagnostics/throw");

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SECRET", raw);
        Assert.DoesNotContain("Server=", raw);          // no connection details
        Assert.DoesNotContain("SELECT", raw);           // no SQL text
        Assert.DoesNotContain("StackFrame", raw);       // no stack trace
        Assert.DoesNotContain("InvalidOperationException", raw);
    }

    // ---------- deliberate responses unchanged ----------

    [Fact]
    public async Task Unauthenticated_StillReturns401_NotWrappedAs500()
    {
        var client = _factory.CreateClient();   // no bearer token

        var response = await client.GetAsync("/test-diagnostics/throw");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Forbidden_StillReturns403_NotWrappedAs500()
    {
        // A valid, authenticated token that simply lacks the required role → 403, untouched.
        var response = await AuthenticatedClient().GetAsync("/test-diagnostics/forbidden");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Validation_StillReturns400_NotWrappedAs500()
    {
        // Missing the [Required] Name → the [ApiController] 400 validation problem, untouched.
        var response = await AuthenticatedClient().PostAsJsonAsync(
            "/test-diagnostics/validate", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class ErrorBody
    {
        public string? Message { get; set; }
    }
}
