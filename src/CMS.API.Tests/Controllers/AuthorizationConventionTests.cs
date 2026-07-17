using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace CMS.API.Tests.Controllers;

/// <summary>
/// Structural guards over authorization, enforced by reflection rather than by remembering.
///
/// The escalation these exist to prevent: any DTO carrying <c>RoleIds</c> / <c>UserIds</c> is synced
/// straight into AppUserRole, and the JWT mints one `role` claim per row — so an endpoint that accepts
/// such a payload without an Admin gate lets a merely-authenticated caller hand itself Admin. Writing
/// one more per-endpoint test would not have caught this: the original gap was that nobody ASKED
/// whether endpoints other than reset-password needed a gate. So the rule is asserted over the whole
/// controller surface at once, and a newly-added endpoint is covered the moment it compiles.
/// </summary>
public sealed class AuthorizationConventionTests
{
    /// <summary>Properties whose presence in a request DTO means the call can change who holds a role.</summary>
    private static readonly string[] RoleGrantingProperties = ["RoleIds", "UserIds"];

    private static IEnumerable<Type> Controllers =>
        typeof(Program).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

    private static IEnumerable<MethodInfo> ActionsOf(Type controller) =>
        controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

    /// <summary>True when the action, or the controller it lives on, demands the Admin role.</summary>
    private static bool RequiresAdmin(MethodInfo action) =>
        action.GetCustomAttributes<AuthorizeAttribute>()
            .Concat(action.DeclaringType!.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
            .Any(a => a.Roles?.Split(',').Select(r => r.Trim()).Contains("Admin") == true);

    private static IEnumerable<MethodInfo> RoleGrantingActions =>
        Controllers.SelectMany(ActionsOf).Where(action =>
            action.GetParameters().Any(p =>
                p.ParameterType.GetProperties().Any(prop => RoleGrantingProperties.Contains(prop.Name))));

    /// <summary>
    /// Every endpoint that can assign roles must require the Admin role. Fails with the offending
    /// action names rather than a bare false, so the fix is obvious from the test output alone.
    /// </summary>
    [Fact]
    public void EveryRoleGrantingEndpoint_RequiresTheAdminRole()
    {
        var offenders = RoleGrantingActions
            .Where(action => !RequiresAdmin(action))
            .Select(action => $"{action.DeclaringType!.Name}.{action.Name}")
            .OrderBy(name => name)
            .ToList();

        Assert.True(offenders.Count == 0,
            "These endpoints accept a role-granting payload (RoleIds/UserIds) without " +
            "[Authorize(Roles = \"Admin\")], so any authenticated caller could grant itself Admin:\n  " +
            string.Join("\n  ", offenders));
    }

    /// <summary>
    /// Guards the guard. If the DTO properties are ever renamed, the scan above would match nothing and
    /// pass vacuously forever — a green test asserting nothing is worse than no test. This fails loudly
    /// instead, pointing at <see cref="RoleGrantingProperties"/> as the thing to update.
    /// </summary>
    [Fact]
    public void TheRoleGrantingScan_ActuallyMatchesSomething()
    {
        var matched = RoleGrantingActions
            .Select(action => $"{action.DeclaringType!.Name}.{action.Name}")
            .ToList();

        Assert.True(matched.Count > 0,
            $"No action was found accepting any of [{string.Join(", ", RoleGrantingProperties)}]. " +
            "Either the role-granting DTO properties were renamed (update RoleGrantingProperties) or " +
            "the endpoints were removed. Until this is reconciled, " +
            $"{nameof(EveryRoleGrantingEndpoint_RequiresTheAdminRole)} proves nothing.");
    }

    /// <summary>
    /// Nothing may be anonymous except logging in. The fallback policy in Program.cs makes auth
    /// opt-out, so a stray [AllowAnonymous] is the one thing that can silently publish an endpoint.
    /// </summary>
    [Fact]
    public void OnlyLogin_IsAnonymous()
    {
        var anonymous = Controllers.SelectMany(ActionsOf)
            .Where(action =>
                action.GetCustomAttributes<AllowAnonymousAttribute>().Any() ||
                action.DeclaringType!.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any())
            .Select(action => $"{action.DeclaringType!.Name}.{action.Name}")
            .OrderBy(name => name)
            .ToList();

        Assert.Equal(["AuthController.Login"], anonymous);
    }
}
