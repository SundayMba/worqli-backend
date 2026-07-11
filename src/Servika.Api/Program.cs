using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Servika.Api.Bookings;
using Servika.Api.Hubs;
using Servika.Api.Middleware;
using Servika.Api.Tracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servika.Application;
using Servika.Infrastructure;
using Servika.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// KYC submissions carry base64 images in the JSON body. The client compresses
// them, but raise Kestrel's ~30MB default so an uncompressed-fallback upload from
// a high-megapixel phone camera isn't rejected/reset.
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 60 * 1024 * 1024);

// --- Services (the "DI container": register everything the app can use) -----

// Infrastructure layer: database, password hashing, JWT/refresh token services.
builder.Services.AddInfrastructure(builder.Configuration);

// Application layer: the use-case handlers (register/login/refresh/logout/me).
builder.Services.AddApplication();

// --- Authentication: validate the JWT bearer token on protected endpoints ----
var jwt = builder.Configuration.GetSection("Jwt");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep claim names as-issued ("sub", "role") instead of remapping them.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwt["Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwt["SigningKey"]!)),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        // SignalR WebSockets can't send the Authorization header, so the JS client
        // passes the token as the `access_token` query param. Lift it onto the
        // request for the hub paths so [Authorize] on the hub still works.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();

// SignalR — real-time hubs (live tracking, chat, notifications).
builder.Services.AddSignalR();

// Real-time notification broadcast (the push dispatcher publishes through this
// port; the implementation needs the hub, so it lives in the Api host).
builder.Services.AddSingleton<
    Servika.Application.Abstractions.Notifications.INotificationRealtimePublisher,
    Servika.Api.Realtime.SignalRNotificationPublisher>();

// Background sweep that ends stale tracking sessions (see TrackingCleanupService).
builder.Services.AddHostedService<TrackingCleanupService>();
// Background sweep that auto-confirms jobs the customer never confirmed.
builder.Services.AddHostedService<CompletionAutoConfirmService>();

// Register MVC controllers. This makes ASP.NET scan the assembly for classes
// that derive from ControllerBase and turn their methods into HTTP endpoints.
builder.Services.AddControllers();

// Swagger / OpenAPI — generates the interactive API docs at /swagger.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Servika API",
        Version = "v1",
        Description =
            "Backend API for Servika — a Lagos service marketplace connecting " +
            "customers with verified artisans. Covers auth, marketplace, bookings, " +
            "payments/wallet, live tracking, reviews, disputes, and admin operations.",
        Contact = new OpenApiContact
        {
            Name = "Servika Engineering",
            Email = "engineering@servika.app",
        },
    });

    // Pull the /// doc-comments (compiled to Servika.Api.xml) into the UI.
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }

    // "Authorize" button in Swagger UI: lets you paste a JWT and have it sent as
    // the Authorization: Bearer header on protected endpoints (e.g. /auth/me).
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT access token (without the 'Bearer ' prefix).",
    });
    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer"), new List<string>() },
    });
});

// Health checks (DB and other dependencies are added in later slices).
builder.Services.AddHealthChecks();

// CORS — gate *browser* clients (the admin dashboard + Expo web/dev). Native mobile
// builds don't send an Origin header, so they aren't subject to CORS and work
// regardless; this list is what browsers are allowed to call the API from.
// Origins come from config ("Cors:AllowedOrigins") so they can change without a
// recompile (override per-env with the Cors__AllowedOrigins__0.. env vars). If none
// are configured we fall back to AllowAnyOrigin — convenient for local dev only.
const string MobileCorsPolicy = "MobileApp";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy(MobileCorsPolicy, policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins);   // production: known origins only
        else
            policy.AllowAnyOrigin();              // dev fallback when nothing configured

        policy.AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

// Apply any pending database migrations on startup, so a fresh database gets its
// tables created (and an existing one gets new ones) without a manual step. This is
// idempotent — it only applies what's missing. Safe here because we run a single API
// instance; with multiple instances you'd move this to a dedicated one-off migration
// step so two instances don't migrate at once.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ServikaDbContext>();
    db.Database.Migrate();
}

// --- HTTP pipeline (the ordered list of middleware each request flows through) ---

// First in the pipeline so it catches exceptions from everything downstream and
// turns our use-case exceptions into clean ProblemDetails responses.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Swagger UI is available in every environment for now (MVP); restrict later.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Servika API v1");
    options.RoutePrefix = "swagger";
});

app.UseCors(MobileCorsPolicy);

// Authentication must run before authorization: first work out *who* the caller
// is (validate the JWT), then enforce *what* they're allowed to do ([Authorize]).
app.UseAuthentication();
app.UseAuthorization();

// Liveness/readiness probe. This stays here (not in a controller) because it is
// framework infrastructure, not a business endpoint — MapHealthChecks wires the
// built-in health-check system (extended with DB/Redis checks in later slices)
// straight to the /health URL.
app.MapHealthChecks("/health");

// Connect the controller classes to the routing system. After this call, every
// [HttpGet]/[HttpPost]/... method on a controller becomes a live endpoint.
app.MapControllers();

// Real-time live-tracking hub. Clients connect at /hubs/tracking?access_token=…
app.MapHub<TrackingHub>("/hubs/tracking");

// Real-time chat delivery hub. Clients connect at /hubs/chat?access_token=…
app.MapHub<ChatHub>("/hubs/chat");

// Real-time in-app notification hub (per-user groups; receive-only).
app.MapHub<NotificationsHub>("/hubs/notifications");

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }
