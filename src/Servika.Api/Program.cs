using Microsoft.OpenApi;
using Servika.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// --- Services (the "DI container": register everything the app can use) -----

// Infrastructure layer: database (and, in later slices, hashing/JWT/payments).
builder.Services.AddInfrastructure(builder.Configuration);


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
});

// Health checks (DB and other dependencies are added in later slices).
builder.Services.AddHealthChecks();

// CORS — allow the Expo mobile app (and dev tooling) to call the API.
const string MobileCorsPolicy = "MobileApp";
builder.Services.AddCors(options =>
{
    options.AddPolicy(MobileCorsPolicy, policy =>
        policy.AllowAnyOrigin()   // tightened to known origins before production
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// --- HTTP pipeline (the ordered list of middleware each request flows through) ---

// Swagger UI is available in every environment for now (MVP); restrict later.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Servika API v1");
    options.RoutePrefix = "swagger";
});

app.UseCors(MobileCorsPolicy);

// Liveness/readiness probe. This stays here (not in a controller) because it is
// framework infrastructure, not a business endpoint — MapHealthChecks wires the
// built-in health-check system (extended with DB/Redis checks in later slices)
// straight to the /health URL.
app.MapHealthChecks("/health");

// Connect the controller classes to the routing system. After this call, every
// [HttpGet]/[HttpPost]/... method on a controller becomes a live endpoint.
app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }
