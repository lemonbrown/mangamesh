FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5170
EXPOSE 4200

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["MangaMesh.Peer/MangaMesh.Peer.GatewayApi/MangaMesh.Peer.GatewayApi.csproj", "MangaMesh.Peer/MangaMesh.Peer.GatewayApi/"]
COPY ["MangaMesh.Peer/MangaMesh.Peer.Core/MangaMesh.Peer.Core.csproj", "MangaMesh.Peer/MangaMesh.Peer.Core/"]
COPY ["MangaMesh.Shared/MangaMesh.Shared.csproj", "MangaMesh.Shared/"]
COPY ["MangaMesh.Peer/MangaMesh.Peer.ClientApi/MangaMesh.Peer.ClientApi.csproj", "MangaMesh.Peer/MangaMesh.Peer.ClientApi/"]
RUN dotnet restore "MangaMesh.Peer/MangaMesh.Peer.GatewayApi/MangaMesh.Peer.GatewayApi.csproj"
COPY . .
WORKDIR "/src/MangaMesh.Peer/MangaMesh.Peer.GatewayApi"
RUN dotnet build "MangaMesh.Peer.GatewayApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MangaMesh.Peer.GatewayApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MangaMesh.Peer.GatewayApi.dll"]
