using CMS.API.Data;
using CMS.API.Infrastructure;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
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

// Order matters: CORS first (so preflight OPTIONS is answered before auth runs), then authentication
// and authorization, then the controller endpoints they guard.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposed so CMS.API.Tests can boot the real pipeline via WebApplicationFactory<Program>.
public partial class Program { }
