# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081



# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy Project Files
COPY ["mangamesh-backend/src/MangaMesh.Backend/MangaMesh.Backend.Tracker/MangaMesh.Backend.Tracker.csproj", "mangamesh-backend/src/MangaMesh.Backend/MangaMesh.Backend.Tracker/"]
COPY ["mangamesh-shared/src/MangaMesh.Shared/MangaMesh.Shared/MangaMesh.Shared.csproj", "mangamesh-shared/src/MangaMesh.Shared/MangaMesh.Shared/"]

# Restore
RUN dotnet restore "./mangamesh-backend/src/MangaMesh.Backend/MangaMesh.Backend.Tracker/MangaMesh.Backend.Tracker.csproj"

# Copy Everything
COPY . .

# Build
WORKDIR "/src/mangamesh-backend/src/MangaMesh.Backend/MangaMesh.Backend.Tracker"
RUN dotnet build "./MangaMesh.Backend.Tracker.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./MangaMesh.Backend.Tracker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MangaMesh.Backend.Tracker.dll"]