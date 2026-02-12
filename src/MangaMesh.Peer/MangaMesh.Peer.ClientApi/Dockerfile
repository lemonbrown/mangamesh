# Stage 1: Build React UI
FROM node:20 AS client-build
WORKDIR /ui
COPY ["MangaMesh.Peer/mangamesh-peer-ui/package.json", "MangaMesh.Peer/mangamesh-peer-ui/package-lock.json", "./"]
RUN npm install
COPY ["MangaMesh.Peer/mangamesh-peer-ui/", "./"]
# Build defaults to /ui/dist
RUN npm run build

# Stage 2: Build .NET API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["MangaMesh.Peer/MangaMesh.Peer.ClientApi/MangaMesh.Peer.ClientApi.csproj", "MangaMesh.Peer/MangaMesh.Peer.ClientApi/"]
COPY ["MangaMesh.Peer/MangaMesh.Peer.Core/MangaMesh.Peer.Core.csproj", "MangaMesh.Peer/MangaMesh.Peer.Core/"]
COPY ["MangaMesh.Shared/MangaMesh.Shared.csproj", "MangaMesh.Shared/"]
RUN dotnet restore "MangaMesh.Peer/MangaMesh.Peer.ClientApi/MangaMesh.Peer.ClientApi.csproj"
COPY . .
WORKDIR "/src/MangaMesh.Peer/MangaMesh.Peer.ClientApi"
RUN dotnet build "MangaMesh.Peer.ClientApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MangaMesh.Peer.ClientApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Final Image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443

COPY --from=publish /app/publish .
# Copy built React assets to wwwroot
COPY --from=client-build /ui/dist ./wwwroot

ENTRYPOINT ["dotnet", "MangaMesh.Peer.ClientApi.dll"]