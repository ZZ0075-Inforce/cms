using CMS.API.Data;
using CMS.API.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Dapper type handlers must be registered before the first query runs.
DapperConfig.Register();

const string CorsPolicy = "LocalhostCors";

builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IAppRoleRepository, AppRoleRepository>();
builder.Services.AddScoped<IAppUserRepository, AppUserRepository>();
builder.Services.AddScoped<IPartnerRepository, PartnerRepository>();
builder.Services.AddScoped<ICourseGroupRepository, CourseGroupRepository>();
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<IFeaturedPromoItemRepository, FeaturedPromoItemRepository>();
builder.Services.AddScoped<ILookupRepository, LookupRepository>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "CMS API", Version = "v1" });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy => policy
        .WithOrigins("http://localhost:4200", "https://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

// No UseHttpsRedirection: the API serves plain HTTP on :5000. The template's redirect would
// 307 every call to https://localhost:5001 (nothing listening there) and break CORS preflight.
app.UseCors(CorsPolicy);
app.MapControllers();

app.Run();
