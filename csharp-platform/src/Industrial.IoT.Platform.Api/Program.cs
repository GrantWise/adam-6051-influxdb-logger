// Industrial.IoT.Platform.Api - Web API Application Entry Point
// Configures and starts the Industrial IoT Platform REST API and SignalR services

using Industrial.IoT.Platform.Core.Interfaces;
using Industrial.IoT.Platform.Api.Hubs;
using Industrial.IoT.Platform.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.OpenApi.Models;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Add API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Industrial IoT Platform API", 
        Version = "v1",
        Description = "REST API for Industrial IoT device management, protocol discovery, and data access",
        Contact = new OpenApiContact
        {
            Name = "Industrial IoT Platform",
            Email = "support@industrial-iot.com"
        }
    });
    
    // Include XML comments for API documentation
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
    
    // Add response examples and better documentation
    c.EnableAnnotations();
    c.DocumentFilter<ApiDocumentationFilter>();
});

// Add SignalR for real-time communications
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// Add CORS for web applications
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebClients", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:3001") // React dev servers
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // Required for SignalR
    });
});

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck("api", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("API is running"));

// Add Industrial IoT Platform services
builder.Services.AddScoped<IEnumerable<IDeviceProvider>>(provider => new List<IDeviceProvider>());
builder.Services.AddHostedService<DataStreamService>();

// Add logging
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Industrial IoT Platform API v1");
        c.RoutePrefix = string.Empty; // Serve Swagger UI at the app's root
        c.DocumentTitle = "Industrial IoT Platform API";
        c.DisplayRequestDuration();
        c.EnableDeepLinking();
        c.EnableFilter();
        c.ShowExtensions();
    });
    
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Use CORS
app.UseCors("AllowWebClients");

// Add security headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    await next();
});

// Map controllers
app.MapControllers();

// Map health checks
app.MapHealthChecks("/health");

// Map SignalR hubs
app.MapHub<DeviceDataHub>("/hubs/devicedata");
app.MapHub<DiscoveryHub>("/hubs/discovery");

// Add a simple status endpoint
app.MapGet("/", () => new
{
    Service = "Industrial IoT Platform API",
    Version = "1.0.0",
    Status = "Running",
    Timestamp = DateTimeOffset.UtcNow,
    Documentation = "/swagger"
})
.WithTags("Status")
.WithName("GetApiStatus")
.WithOpenApi();

// Global error handling
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        
        var error = new
        {
            Error = "An unexpected error occurred",
            Timestamp = DateTimeOffset.UtcNow,
            TraceId = context.TraceIdentifier
        };
        
        await context.Response.WriteAsJsonAsync(error);
    });
});

app.Logger.LogInformation("Industrial IoT Platform API starting...");

try
{
    app.Run();
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex, "Industrial IoT Platform API failed to start");
    throw;
}

/// <summary>
/// Custom Swagger documentation filter
/// </summary>
public class ApiDocumentationFilter : Swashbuckle.AspNetCore.SwaggerGen.IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, Swashbuckle.AspNetCore.SwaggerGen.DocumentFilterContext context)
    {
        // Add custom documentation enhancements
        swaggerDoc.Info.License = new OpenApiLicense
        {
            Name = "MIT License",
            Url = new Uri("https://opensource.org/licenses/MIT")
        };
        
        // Add server information
        swaggerDoc.Servers = new List<OpenApiServer>
        {
            new() { Url = "https://localhost:7000", Description = "Development HTTPS" },
            new() { Url = "http://localhost:5000", Description = "Development HTTP" }
        };
        
        // Add tags for better organization
        swaggerDoc.Tags = new List<OpenApiTag>
        {
            new() { Name = "Devices", Description = "Device management and monitoring" },
            new() { Name = "Protocol Discovery", Description = "Scale protocol discovery and templates" },
            new() { Name = "Data", Description = "Time-series and transactional data access" },
            new() { Name = "Status", Description = "API status and health checks" }
        };
    }
}