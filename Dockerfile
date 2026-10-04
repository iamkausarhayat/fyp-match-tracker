# ==========================================
# Multi-stage Dockerfile for .NET 10 ASP.NET Core
# 100% Free 24/7 Deployment on Render.com / Railway
# ==========================================

# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["FypMatchTracker.csproj", "./"]
RUN dotnet restore "./FypMatchTracker.csproj"

# Copy full application code and build
COPY . .
RUN dotnet publish "FypMatchTracker.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime Environment
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# Environment configuration
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "FypMatchTracker.dll"]
