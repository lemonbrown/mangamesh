# Base stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["MangaMesh.Peer/MangaMesh.Peer.Core/MangaMesh.Peer.Core.csproj", "MangaMesh.Peer/MangaMesh.Peer.Core/"]
COPY ["MangaMesh.Shared/MangaMesh.Shared.csproj", "MangaMesh.Shared/"]

RUN dotnet restore "MangaMesh.Peer/MangaMesh.Peer.Core/MangaMesh.Peer.Core.csproj"

COPY . .
WORKDIR "/src/MangaMesh.Peer/MangaMesh.Peer.Core"
RUN dotnet build "MangaMesh.Peer.Core.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "MangaMesh.Peer.Core.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MangaMesh.Peer.Core.dll"]
