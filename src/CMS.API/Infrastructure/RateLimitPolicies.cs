namespace CMS.API.Infrastructure;

/// <summary>
/// Names of the rate-limiting policies configured in Program.cs. Shared so the policy is registered
/// and applied under the same string — a typo in an <c>[EnableRateLimiting]</c> attribute throws at
/// startup rather than silently leaving an endpoint unthrottled, but only if both sides read from here.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>Throttles <c>POST /api/Auth/login</c> per client IP.</summary>
    public const string Login = "login";
}
