FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 7078

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["MangaMesh.Index/MangaMesh.Index.AdminApi/MangaMesh.Index.AdminApi.csproj", "MangaMesh.Index/MangaMesh.Index.AdminApi/"]
COPY ["MangaMesh.Shared/MangaMesh.Shared.csproj", "MangaMesh.Shared/"]
RUN dotnet restore "MangaMesh.Index/MangaMesh.Index.AdminApi/MangaMesh.Index.AdminApi.csproj"
COPY . .
WORKDIR "/src/MangaMesh.Index/MangaMesh.Index.AdminApi"
RUN dotnet build "MangaMesh.Index.AdminApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MangaMesh.Index.AdminApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MangaMesh.Index.AdminApi.dll"]
