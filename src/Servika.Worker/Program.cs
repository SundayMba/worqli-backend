using Servika.Application;
using Servika.Infrastructure;
using Servika.Infrastructure.BackgroundJobs;

// The dedicated background-jobs host. Runs the marketplace's periodic sweeps
// (stale-tracking cleanup + completion auto-confirm) exactly once, so the API can be
// scaled to multiple instances without the sweeps running N times. Shares the
// Application + Infrastructure layers with the API — same handlers, same database.
//
// It does NOT own the SignalR hubs, so real-time broadcasts (TrackingEnded, in-app
// notification pushes) that need a hub are skipped here (their publisher resolves via
// GetService, absent in this host); the DB work + Expo push still run. Cross-process
// real-time delivery would need a SignalR Redis backplane (not wired yet).
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddBackgroundSweeps();

var host = builder.Build();
host.Run();
