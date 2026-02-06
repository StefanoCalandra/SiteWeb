using System.Text;
using Asp.Versioning;
using FluentValidation;
using FluentValidation.AspNetCore;
using HotChocolate.AspNetCore;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SiteWeb.Api.Auth;
using SiteWeb.Api.CQRS;
using SiteWeb.Api.Data;
using SiteWeb.Api.GraphQL;
using SiteWeb.Api.Hubs;
using SiteWeb.Api.Messaging;
using SiteWeb.Api.Tenancy;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("siteweb"));

// Multi-tenant: risolve il tenant dalla richiesta HTTP.
builder.Services.AddScoped<ITenantProvider, HeaderTenantProvider>();
builder.Services.AddScoped<TenantContext>();
builder.Services.AddHttpContextAccessor();

// CQRS + Validation: registra handler e validatori.
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<CreateBookingCommand>());
builder.Services.AddValidatorsFromAssemblyContaining<CreateBookingCommandValidator>();
builder.Services.AddFluentValidationAutoValidation();

// Auth: store refresh token in memoria e servizio JWT.
builder.Services.AddSingleton<RefreshTokenStore>();
builder.Services.AddSingleton<TokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var key = Encoding.UTF8.GetBytes(TokenService.SecretKey);
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = TokenService.Issuer,
            ValidAudience = TokenService.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

builder.Services.AddAuthorization();

// Versioning API: URL e header.
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version"));
}).AddMvc();

builder.Services.AddControllers();
builder.Services.AddSignalR();
// Rate limiting per protezione di base.
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("fixed", limiterOptions =>
    {
        limiterOptions.PermitLimit = 60;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
    });
});

builder.Services.AddGraphQLServer()
    .AddQueryType<ExerciseQuery>();

// Outbox dispatcher: pubblica eventi dalla tabella outbox.
builder.Services.AddHostedService<OutboxDispatcher>();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();

// Middleware multi-tenant: imposta il tenant in scope.
app.UseMiddleware<TenantMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Applica rate limiting ai controller.
app.MapControllers().RequireRateLimiting("fixed");
app.MapHub<DashboardHub>("/hubs/dashboard");
app.MapGraphQL("/graphql");

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
