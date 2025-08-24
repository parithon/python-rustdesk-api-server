# Use the official .NET 9 runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 21114

# Use the .NET 9 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["RustDeskApiServer.csproj", "."]
RUN dotnet restore "RustDeskApiServer.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "RustDeskApiServer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "RustDeskApiServer.csproj" -c Release -o /app/publish

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create directories
RUN mkdir -p /app/db

# Set environment variables
ENV ASPNETCORE_URLS=http://+:21114
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "RustDeskApiServer.dll"]
