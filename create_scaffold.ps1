# PowerShell script to create Phase‑1 scaffold for ThreadDesk
# Save as create_scaffold.ps1 and run from repository root

$files = @{
"README.md" = @'
# ThreadDesk Tally Connector

Phase 1 scaffold for ThreadDesk Tally Connector — a Windows agent that syncs Tally with ThreadDesk SaaS.

This repository contains a multi-project .NET solution scaffold (projects in src/) with a Worker Service, WPF shell, core libraries, API client skeleton, Tally mock provider, infrastructure helpers, and a unit test project. This initial commit focuses on configuration, logging, SQLite scaffolding, and DI so Phase 2 features can be added iteratively.

Quick start (local)

1. Install .NET 8 SDK (https://dotnet.microsoft.com/download)
2. From repo root, create a solution and add projects:

   dotnet new sln -n ThreadDesk
   dotnet sln add src/ThreadDesk.*/*.csproj

3. Build:

   dotnet build

4. Run unit tests:

   dotnet test

Notes
- This is a starting scaffold. Follow the development plan to implement features in phases.
'@

".gitignore" = @'
# Ignore Visual Studio temporary files, build outputs, and user-specific files

bin/
obj/
.vs/
*.user
*.suo

# Rider
.idea/

# NuGet
*.nupkg
.packages

# Logs
*.log

# OS
Thumbs.db
.DS_Store
'@

"src/ThreadDesk.Agent/ThreadDesk.Agent.csproj" = @'
<Project Sdk="Microsoft.NET.Sdk.Worker">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
    <PackageReference Include="Serilog.AspNetCore" Version="7.0.0" />
    <PackageReference Include="Serilog.Sinks.File" Version="6.0.0" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.0" />
    <PackageReference Include="Dapper" Version="2.0.123" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\ThreadDesk.Core\ThreadDesk.Core.csproj" />
    <ProjectReference Include="..\ThreadDesk.Infrastructure\ThreadDesk.Infrastructure.csproj" />
    <ProjectReference Include="..\ThreadDesk.Api\ThreadDesk.Api.csproj" />
    <ProjectReference Include="..\ThreadDesk.Tally\ThreadDesk.Tally.csproj" />
  </ItemGroup>
</Project>
'@

"src/ThreadDesk.Agent/Program.cs" = @'
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using ThreadDesk.Infrastructure.Logging;
using ThreadDesk.Core;
using ThreadDesk.Api;
using ThreadDesk.Tally;

namespace ThreadDesk.Agent;

public class Program
{
    public static async Task Main(string[] args)
    {
        LogConfigurator.Configure();

        var host = Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                // configuration
                services.Configure<AppConfig>(context.Configuration.GetSection("AppConfig"));

                // infrastructure
                services.AddSingleton<ISqliteService, SqliteService>();

                // api client
                services.AddHttpClient<IThreadDeskApiClient, ThreadDeskApiClient>(client =>
                {
                    // Base address configured by AppConfig or environment
                });

                // Tally
                services.AddSingleton<ITallyConnectionManager, MockTallyProvider>();

                // Hosted worker
                services.AddHostedService<Worker>();
            })
            .Build();

        await host.RunAsync();
    }
}
'@

"src/ThreadDesk.Agent/Worker.cs" = @'
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ThreadDesk.Agent;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ThreadDesk Agent worker running.");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Heartbeat from worker at: {time}", DateTimeOffset.Now);
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (TaskCanceledException) { }
        }

        _logger.LogInformation("ThreadDesk Agent worker stopping.");
    }
}
'@

"src/ThreadDesk.Core/ThreadDesk.Core.csproj" = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
'@

"src/ThreadDesk.Core/Models/AppConfig.cs" = @'
namespace ThreadDesk.Core;

public class AppConfig
{
    public string ApiBaseUrl { get; set; } = "https://yourapp.com/api";
    public int SyncIntervalMinutes { get; set; } = 30;
    public string AgentVersion { get; set; } = "1.0.0";
}
'@

"src/ThreadDesk.Infrastructure/ThreadDesk.Infrastructure.csproj" = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Serilog" Version="2.12.0" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.0" />
    <PackageReference Include="Dapper" Version="2.0.123" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\\ThreadDesk.Core\\ThreadDesk.Core.csproj" />
  </ItemGroup>
</Project>
'@

"src/ThreadDesk.Infrastructure/Logging/SerilogExtensions.cs" = @'
using System;
using System.IO;
using Serilog;

namespace ThreadDesk.Infrastructure.Logging;

public static class LogConfigurator
{
    public static void Configure()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var logDir = Path.Combine(programData, "ThreadDesk", "Logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(logDir, "agent.log"), rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Log.Information("Logger configured. Logs at {logDir}", logDir);
    }
}
'@

"src/ThreadDesk.Api/ThreadDesk.Api.csproj" = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\\ThreadDesk.Core\\ThreadDesk.Core.csproj" />
  </ItemGroup>
</Project>
'@

"src/ThreadDesk.Api/ThreadDeskApiClient.cs" = @'
using System.Net.Http;
using System.Threading.Tasks;
using ThreadDesk.Core;

namespace ThreadDesk.Api;

public interface IThreadDeskApiClient
{
    Task<bool> PingAsync();
}

public class ThreadDeskApiClient : IThreadDeskApiClient
{
    private readonly HttpClient _client;
    private readonly AppConfig _config;

    public ThreadDeskApiClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<bool> PingAsync()
    {
        // Simple health check - implementation to be expanded
        var resp = await _client.GetAsync("/api/health");
        return resp.IsSuccessStatusCode;
    }
}
'@

"src/ThreadDesk.Tally/ThreadDesk.Tally.csproj" = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\\ThreadDesk.Core\\ThreadDesk.Core.csproj" />
  </ItemGroup>
</Project>
'@

"src/ThreadDesk.Tally/MockTallyProvider.cs" = @'
using System.Collections.Generic;
using System.Threading.Tasks;
using ThreadDesk.Core;

namespace ThreadDesk.Tally;

public interface ITallyConnectionManager
{
    Task<bool> TestConnectionAsync();
    Task<IEnumerable<string>> GetCompaniesAsync();
}

public class MockTallyProvider : ITallyConnectionManager
{
    public Task<bool> TestConnectionAsync()
    {
        return Task.FromResult(true);
    }

    public Task<IEnumerable<string>> GetCompaniesAsync()
    {
        var companies = new List<string> { "Shree Fabrics Pvt Ltd", "Demo Co" };
        return Task.FromResult<IEnumerable<string>>(companies);
    }
}
'@

"tests/ThreadDesk.UnitTests/ThreadDesk.UnitTests.csproj" = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit" Version="2.5.3" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.4.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\\ThreadDesk.Core\\ThreadDesk.Core.csproj" />
    <ProjectReference Include="..\\ThreadDesk.Tally\\ThreadDesk.Tally.csproj" />
  </ItemGroup>
</Project>
'@

"tests/ThreadDesk.UnitTests/UnitTest1.cs" = @'
using System.Threading.Tasks;
using ThreadDesk.Tally;
using Xunit;

namespace ThreadDesk.UnitTests;

public class TallyMockTests
{
    [Fact]
    public async Task MockTally_Returns_Companies()
    {
        var mock = new MockTallyProvider();
        var ok = await mock.TestConnectionAsync();
        var companies = await mock.GetCompaniesAsync();

        Assert.True(ok);
        Assert.NotEmpty(companies);
    }
}
'@

".github/workflows/dotnet.yml" = @'
name: .NET

on:
  push:
    branches: [ main, feature/phase-1 ]
  pull_request:
    branches: [ main ]

jobs:
  build:
    runs-on: windows-latest

    steps:
    - uses: actions/checkout@v4
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'
    - name: Restore
      run: dotnet restore
    - name: Build
      run: dotnet build --no-restore --configuration Release
    - name: Test
      run: dotnet test --no-build --verbosity normal
'@

"src/ThreadDesk.Agent/appsettings.json" = @'
{
  "AppConfig": {
    "ApiBaseUrl": "https://staging.yourapp.com/api",
    "SyncIntervalMinutes": 30,
    "AgentVersion": "1.0.0"
  }
}
'@

"docs/README_PLACEHOLDERS.md" = @'
# Docs placeholders

This folder contains placeholder files for the documentation required by the project. Fill these out as feature implementation progresses.
'@
}

# Create directories and files
foreach ($path in $files.Keys) {
    $dir = Split-Path $path
    if ($dir -and -not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    $content = $files[$path]
    $content | Out-File -FilePath $path -Encoding utf8
    Write-Host "Created $path"
}

Write-Host "Scaffold files created. You can now run:"
Write-Host "  dotnet new sln -n ThreadDesk"
Write-Host "  dotnet sln add src/ThreadDesk.*/*.csproj"
Write-Host "  dotnet build"
Write-Host "  dotnet test"