# Build context is the repository root (Directory.Packages.props and global.json live there).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Packages.props FloodLink.sln ./
COPY backend ./backend
RUN dotnet publish backend/src/FloodLink.Api/FloodLink.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV Database__MigrateOnStartup=true
EXPOSE 8080
ENTRYPOINT ["dotnet", "FloodLink.Api.dll"]
