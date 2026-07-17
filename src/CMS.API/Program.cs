using System.Globalization;
using System.Threading.RateLimiting;
using CMS.API.Data;
using CMS.API.Infrastructure;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Dapper type handlers must be registered before the first query runs.
DapperConfig.Register();

const string CorsPolicy = "LocalhostCors";

builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IAppRoleRepository, AppRoleRepository>();
builder.Services.AddScoped<IAppUserRepository, AppUserRepository>();
builder.Services.AddScoped<IPartnerRepository, PartnerRepository>();
builder.Services.AddScoped<ICourseGroupRepository, CourseGroupRepository>();
builder.Services.AddScoped<IPublishStatusRepository, PublishStatusRepository>();
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<IFeaturedPromoItemRepository, FeaturedPromoItemRepository>();
builder.Services.AddScoped<ILookupRepository, LookupRepository>();
builder.Services.AddScoped<IRowAuditRepository, RowAuditRepository>();

// Cross-cutting audit writer. Reads the current user from the request (HttpContext), so the accessor
// must be registered too; repositories call it inside their own transaction after a change succeeds.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRowAuditWriter, RowAuditWriter>();

// JWT bearer auth. The signing key lives in SysConfig (read lazily via ISigningKeyProvider), so the
// options are configured through ConfigureJwtBearerOptions rather than an inline static key.
builder.Services.AddSingleton<ISigningKeyProvider, SigningKeyProvider>();
builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

// Every endpoint requires an authenticated user unless it opts out with [AllowAnonymous].
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

// Rate limiting. Login is the one endpoint an ANONYMOUS caller can hammer, and a correct guess there
// is worth every other endpoint — so it is the only thing throttled here. Partitioned by client IP:
// the alternative, partitioning by submitted UserId, would need the request body (which the limiter
// runs too early to have read) and would let an attacker sidestep the limit by varying the id.
//
// The trade-off is that everyone behind one NAT shares a bucket. PermitLimit is set well above human
// typo-retry rates so that stays theoretical, while still cutting automated guessing to ~2/min.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // A rejection MUST carry a body. The limiter's default 429 is empty, and the Angular login page
    // falls back to "使用者代碼或密碼錯誤。" when a failed login has no detail — so a throttled user
    // would be told their password is wrong and would keep retrying. Emitting ProblemDetails in the
    // shape the page already reads (title/detail) makes it say the true thing with no client change.
    options.OnRejected = async (context, ct) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "嘗試次數過多",
            Detail = "登入嘗試過於頻繁，請稍後再試。"
        }, ct);
    };

    options.AddPolicy(RateLimitPolicies.Login, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            // No IP (in-memory test hosts) all share one bucket rather than bypassing the limit.
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0 // Reject immediately; queueing a login just delays the same answer.
            }));
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "CMS API", Version = "v1" });

    // Let Swagger UI send an "Authorization: Bearer <token>" header for the protected endpoints.
    options.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "貼上 /api/Auth/login 取得的 access token（不含 'Bearer ' 前綴）。"
    });
    options.AddSecurityRequirement(new()
    {
        [new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } }] = []
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy => policy
        .WithOrigins("http://localhost:4200", "https://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

// Outermost middleware: catch any exception escaping the pipeline below and return one safe 500.
// It never clears the response, so CORS headers an inner middleware added are preserved.
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();
// The root redirect is a mapped endpoint, so the RequireAuthenticatedUser fallback policy would 401 it
// — opt it out explicitly.
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription().AllowAnonymous();

// No UseHttpsRedirection: the API serves plain HTTP on :5000. The template's redirect would
// 307 every call to https://localhost:5001 (nothing listening there) and break CORS preflight.
app.UseCors(CorsPolicy);

// UseRouting is explicit here because UseRateLimiter must run AFTER it: an endpoint-specific policy
// ([EnableRateLimiting] on the action) can only be found once routing has resolved the endpoint.
// Left implicit, routing would run at MapControllers — after the limiter — and login would silently
// go unthrottled with no error to notice.
app.UseRouting();
app.UseRateLimiter();

// Order matters: CORS first (so preflight OPTIONS is answered before auth runs), then authentication
// and authorization, then the controller endpoints they guard.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposed so CMS.API.Tests can boot the real pipeline via WebApplicationFactory<Program>.
public partial class Program { }
