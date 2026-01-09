# Use ASP.NET Core Runtime for base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Use SDK for build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["DualMind.API/DualMind.API.csproj", "DualMind.API/"]
RUN dotnet restore "DualMind.API/DualMind.API.csproj"
COPY . .
WORKDIR "/src/DualMind.API"
RUN dotnet build "DualMind.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DualMind.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DualMind.API.dll"]
