using MCPVault.Core.Authentication;
using MCPVault.Core.Configuration;
using MCPVault.Core.Interfaces;
using MCPVault.Core.MCP;
using MCPVault.Infrastructure.Database;
using MCPVault.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configuration
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));

// Infrastructure
builder.Services.AddScoped<IDbConnection, PostgreSqlConnection>();
builder.Services.AddScoped<IOrganizationRepository, OrganizationRepository>();

// Core services
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IMcpProxyService, McpProxyService>();

// HttpClient
builder.Services.AddHttpClient("MCP", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "MCPVault/1.0");
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();