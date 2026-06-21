# Multi-stage build for the Servika API. Host-agnostic — works on Railway,
# Render, Fly.io, or any container platform.
#
# Required environment variables at runtime (override the dev appsettings):
#   ConnectionStrings__Postgres   e.g. Host=...;Port=5432;Database=...;Username=...;Password=...
#   Jwt__SigningKey               a long random secret (>= 32 chars)
#   Resend__ApiKey                your Resend API key (enables real email)
#   Resend__FromEmail             verified sender, or onboarding@resend.dev to start
# ASP.NET Core listens on port 8080 by default in containers.

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first (cached unless project files change).
COPY Servika.sln ./
COPY src/Servika.Api/Servika.Api.csproj src/Servika.Api/
COPY src/Servika.Application/Servika.Application.csproj src/Servika.Application/
COPY src/Servika.Contracts/Servika.Contracts.csproj src/Servika.Contracts/
COPY src/Servika.Domain/Servika.Domain.csproj src/Servika.Domain/
COPY src/Servika.Infrastructure/Servika.Infrastructure.csproj src/Servika.Infrastructure/
COPY src/Servika.Worker/Servika.Worker.csproj src/Servika.Worker/
COPY tests/Servika.UnitTests/Servika.UnitTests.csproj tests/Servika.UnitTests/
COPY tests/Servika.IntegrationTests/Servika.IntegrationTests.csproj tests/Servika.IntegrationTests/
RUN dotnet restore src/Servika.Api/Servika.Api.csproj

# Build + publish the API.
COPY . .
RUN dotnet publish src/Servika.Api/Servika.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "Servika.Api.dll"]
