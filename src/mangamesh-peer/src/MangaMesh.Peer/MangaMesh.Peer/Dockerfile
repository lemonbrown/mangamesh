# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files first to leverage caching
COPY ["mangamesh-client/src/MangaMesh.Client/MangaMesh.Client/MangaMesh.Client.csproj", "mangamesh-client/src/MangaMesh.Client/MangaMesh.Client/"]
COPY ["mangamesh-shared/src/MangaMesh.Shared/MangaMesh.Shared/MangaMesh.Shared.csproj", "mangamesh-shared/src/MangaMesh.Shared/MangaMesh.Shared/"]

# Restore dependencies
RUN dotnet restore "mangamesh-client/src/MangaMesh.Client/MangaMesh.Client/MangaMesh.Client.csproj"

# Copy the rest of the source code
COPY . .

# Build the project
WORKDIR "/src/mangamesh-client/src/MangaMesh.Client/MangaMesh.Client"
RUN dotnet build "MangaMesh.Client.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "MangaMesh.Client.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MangaMesh.Client.dll"]
