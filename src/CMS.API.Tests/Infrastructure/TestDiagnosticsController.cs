using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Tests.Infrastructure;

/// <summary>
/// A controller that exists ONLY in the test assembly (wired in via
/// <see cref="ExceptionHandlingTestFactory"/>'s application part). It gives the exception-handling tests
/// endpoints that (a) throw, (b) demand a role no test token holds → 403, and (c) validate a body → 400,
/// without adding any of that to the shipping API. It rides the real auth pipeline, so every action
/// needs a valid bearer token (the fallback policy), except the role-gated one which yields 403.
/// </summary>
[ApiController]
[Route("test-diagnostics")]
public class TestDiagnosticsController : ControllerBase
{
    /// <summary>The sensitive text a leak would expose — asserted absent from the 500 body.</summary>
    public const string SecretDetail =
        "SECRET boom: Server=db;Password=hunter2; SELECT * FROM Users; at StackFrame.Line 42";

    [HttpGet("throw")]
    public IActionResult Throw() => throw new InvalidOperationException(SecretDetail);

    [HttpGet("forbidden")]
    [Authorize(Roles = "RoleNoTokenEverHas")]
    public IActionResult Forbidden() => Ok();

    public sealed class ValidatePayload
    {
        [Required]
        public string? Name { get; set; }
    }

    [HttpPost("validate")]
    public IActionResult Validate([FromBody] ValidatePayload payload) => Ok(payload);
}
