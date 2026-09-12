using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Servika.Api.Hubs;
using Servika.Api.Middleware;
using Servika.Infrastructure.BackgroundJobs;
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

// --- Fail-closed production config guard --------------------------------------
// In Development we allow insecure conveniences (a placeholder JWT key, the stub
// payment/payout gateways that trust every webhook, AllowAnyOrigin CORS). None of
// those may run in Production: a blank Paystack key there would select the stub
// gateway, which accepts ANY webhook signature — an attacker could mark bookings
// paid for free. Rather than run insecurely, refuse to start and say why.
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"];
var paystackSecretKey = builder.Configuration["Paystack:SecretKey"];
var corsAllowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                         ?? Array.Empty<string>();

if (builder.Environment.IsProduction())
{
    var misconfig = new List<string>();
    if (string.IsNullOrWhiteSpace(jwtSigningKey)
        || jwtSigningKey.Length < 32
        || jwtSigningKey.Contains("REPLACE_WITH", StringComparison.OrdinalIgnoreCase))
    {
        misconfig.Add("Jwt:SigningKey must be a real secret of at least 32 characters "
                      + "(set Jwt__SigningKey).");
    }
    if (string.IsNullOrWhiteSpace(paystackSecretKey))
    {
        misconfig.Add("Paystack:SecretKey is required in Production — without it the payment/"
                      + "payout STUBS run, and they trust every webhook signature (set Paystack__SecretKey).");
    }
    if (corsAllowedOrigins.Length == 0)
    {
        misconfig.Add("Cors:AllowedOrigins must list the browser origins allowed to call the API "
                      + "(set Cors__AllowedOrigins__0, __1, …).");
    }
    // If the phone gate is on, codes must be deliverable — the stub only logs, so
    // turning it on without an SMS provider would lock customers out of booking.
    if (builder.Configuration.GetValue("Auth:RequirePhoneForBooking", false)
        && string.IsNullOrWhiteSpace(builder.Configuration["Sms:ApiKey"]))
    {
        misconfig.Add("Auth:RequirePhoneForBooking is on but Sms:ApiKey is unset — customers "
                      + "couldn't receive codes. Set Sms__ApiKey or turn the gate off.");
    }
    if (misconfig.Count > 0)
    {
        throw new InvalidOperationException(
            "Refusing to start: insecure production configuration:\n - "
            + string.Join("\n - ", misconfig));
    }
}

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

// Real-time tracking broadcast (the stale-session sweep publishes through this port
// so it can broadcast TrackingEnded while hosted in the Api; the worker has no hub).
builder.Services.AddSingleton<
    Servika.Application.Abstractions.Tracking.ITrackingRealtimePublisher,
    Servika.Api.Realtime.SignalRTrackingPublisher>();

// Periodic background sweeps (stale-tracking cleanup + completion auto-confirm).
// They live in Infrastructure so the dedicated Servika.Worker can run them when the
// API is scaled out. Until then the API runs them in-process (default true); set
// Worker:RunSweepsInApi=false on the API instances once the Worker is deployed, so
// the sweeps run exactly once.
if (builder.Configuration.GetValue("Worker:RunSweepsInApi", true))
{
    builder.Services.AddBackgroundSweeps();
}

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

// Per-IP rate limit on the phone-OTP endpoints (defence-in-depth vs SMS pumping,
// on top of the per-user daily cap). A generous fixed window so legit use — one
// send + a few verify attempts — never trips it. Returns 429 when exceeded.
// (Behind a proxy/LB, register forwarded-headers so the client IP is real.)
const string PhoneOtpRateLimit = "phone-otp";
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(PhoneOtpRateLimit, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 12,
                Window = TimeSpan.FromMinutes(10),
            }));
    // The in-app checkout polls verify every few seconds while a bank transfer
    // confirms; generous per IP, still a ceiling against hammering the provider.
    options.AddPolicy("payment-verify", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(2),
            }));
});

// CORS — gate *browser* clients (the admin dashboard + Expo web/dev). Native mobile
// builds don't send an Origin header, so they aren't subject to CORS and work
// regardless; this list is what browsers are allowed to call the API from.
// Origins come from config ("Cors:AllowedOrigins") so they can change without a
// recompile (override per-env with the Cors__AllowedOrigins__0.. env vars). If none
// are configured we fall back to AllowAnyOrigin — convenient for local dev only.
const string MobileCorsPolicy = "MobileApp";
builder.Services.AddCors(options =>
{
    options.AddPolicy(MobileCorsPolicy, policy =>
    {
        if (corsAllowedOrigins.Length > 0)
            policy.WithOrigins(corsAllowedOrigins);   // known origins only
        else if (!builder.Environment.IsProduction())
            policy.AllowAnyOrigin();                  // dev-only fallback (Production
                                                      // already threw at startup)

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

// Swagger exposes the full API surface + an auth-primed "try it" console, so it's
// off in Production unless explicitly opted in (Swagger:Enabled=true for a staging
// box). Always on outside Production for local dev.
if (!app.Environment.IsProduction() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Servika API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseCors(MobileCorsPolicy);

// Rate limiting runs before the endpoints it guards.
app.UseRateLimiter();

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
