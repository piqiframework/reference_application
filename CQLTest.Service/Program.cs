using CQLTest.Service;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Java sidecar — process lifecycle
builder.Services.Configure<JavaSidecarOptions>(
    builder.Configuration.GetSection(JavaSidecarOptions.Section));
builder.Services.AddSingleton<JavaSidecarLauncher>();
builder.Services.AddHostedService<JavaSidecarHostedService>();

// Named HttpClient that talks to the Java sidecar
builder.Services.AddHttpClient("sidecar", (sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<JavaSidecarOptions>>().Value;
    client.BaseAddress = new Uri(opts.BaseUrl);
    client.Timeout     = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

var app = builder.Build();

app.MapControllers();
app.Run();

// Expose Program for WebApplicationFactory in tests
public partial class Program { }
