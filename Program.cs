using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using ResumeAnalyzer.Api.Data;
using ResumeAnalyzer.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------
// Services
// ---------------------------------------------------------------------

builder.Services.AddControllers();

// EF Core + PostgreSQL. Connection string is required and must come from
// configuration — appsettings.json now ships with an empty placeholder
// (see appsettings.json.example for setup instructions), and the real
// value is supplied via dotnet user-secrets locally or an environment
// variable / secrets manager (ConnectionStrings__DefaultConnection) in
// CI/staging/production. The app fails fast at startup if it's missing,
// rather than surfacing a confusing Npgsql connection error later.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is missing. Set it via " +
        "`dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"...\"` " +
        "locally, or the ConnectionStrings__DefaultConnection environment variable " +
        "in other environments. See appsettings.json.example for details.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// JWT configuration — secret is required and must come from configuration.
// appsettings.json ships with an empty placeholder (no real secret is ever
// committed); the real value comes from dotnet user-secrets locally or an
// environment variable / secrets manager (Jwt__Secret) elsewhere — see
// appsettings.json.example. The app intentionally fails fast at startup if
// it's missing or too short, rather than silently signing tokens with a
// weak or empty key.
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Secret is missing or too short (must be at least 32 characters). " +
        "Set it via dotnet user-secrets, an environment variable, or a secrets " +
        "manager — see appsettings.json.example. It must never be committed to " +
        "appsettings.json.");
}

builder.Services.Configure<JwtOptions>(options =>
{
    options.Secret = jwtSecret;
    options.Issuer = builder.Configuration["Jwt:Issuer"] ?? "ResumeAnalyzer.Api";
    options.Audience = builder.Configuration["Jwt:Audience"] ?? "ResumeAnalyzer.Client";
});

// Gemini API configuration — "Gemini:ApiKey" should come from
// user-secrets or an environment variable in real environments, never
// committed to appsettings.json. See appsettings.json.example. Left
// empty/missing, GeminiAnalysisService falls back automatically to the
// deterministic KeywordAnalysisService (see below), so this is not a
// hard startup requirement the way the two checks above are.
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection("Gemini"));

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "ResumeAnalyzer.Api",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "ResumeAnalyzer.Client",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

builder.Services.AddAuthorization();

// Application services (DI)
builder.Services.AddScoped<IFileValidator, ResumeFileValidator>();
builder.Services.AddScoped<IJobDescriptionValidator, JobDescriptionValidator>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IResumeTextExtractor, ResumeTextExtractor>();

// AI-powered analysis via the Gemini API. GeminiAnalysisService falls back
// to the deterministic KeywordAnalysisService automatically if Gemini:ApiKey
// is missing or the API call fails, so this is safe to leave as the default
// even before you've set up a key.
builder.Services.AddHttpClient<IResumeAnalysisService, GeminiAnalysisService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddScoped<IAnalysisOrchestrator, AnalysisOrchestrator>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthOrchestrator, AuthOrchestrator>();

// CORS — allow the Next.js frontend (configure the origin via appsettings)
const string CorsPolicyName = "FrontendPolicy";
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? new[] { "http://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Swagger / OpenAPI, with a Bearer token field so [Authorize] endpoints
// can be tested directly from the Swagger UI.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Resume Analyzer API",
        Version = "v1",
        Description = "Backend API for resume/job-description ATS analysis.",
    });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter a JWT, e.g.: Bearer {your token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "Bearer",
    };

    options.AddSecurityDefinition("Bearer", securityScheme);

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer",
                }
            }
        ] = new List<string>(),
    });
});

var app = builder.Build();

// ---------------------------------------------------------------------
// Middleware pipeline
// ---------------------------------------------------------------------

// Swagger available in all environments so the live API on Render is browsable.
// Remove or gate this behind IsDevelopment() before making the API truly public.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Resume Analyzer API v1");
});

//app.UseHttpsRedirection();

app.UseRouting();

app.UseCors(CorsPolicyName);

// Serves files from wwwroot/uploads (useful for previewing/downloading
// the originally uploaded resume during development).
app.UseStaticFiles();

// IMPORTANT: Authentication must run before Authorization — it's what
// populates HttpContext.User from the JWT. Reversing this order silently
// breaks every [Authorize] endpoint (requests look "anonymous" even with
// a valid token attached).
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Apply any pending EF Core migrations automatically on startup.
// Convenient for local dev; for production you may prefer running
// `dotnet ef database update` as a separate deploy step instead.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();
