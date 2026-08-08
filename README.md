# ThreadDesk Tally Connector

Phase 1 scaffold for ThreadDesk Tally Connector â€” a Windows agent that syncs Tally with ThreadDesk SaaS.

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
