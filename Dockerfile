FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY RustDeskApiServer/RustDeskApiServer.csproj RustDeskApiServer/
RUN dotnet restore RustDeskApiServer/RustDeskApiServer.csproj

COPY RustDeskApiServer/ RustDeskApiServer/
RUN dotnet publish RustDeskApiServer/RustDeskApiServer.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN mkdir -p /app/db

COPY --from=build /app/publish .
# Copy static web client assets if present
COPY --from=build /src/RustDeskApiServer/wwwroot ./wwwroot

ENV ASPNETCORE_URLS=http://0.0.0.0:21114
ENV TZ=UTC

EXPOSE 21114/tcp
EXPOSE 21114/udp

ENTRYPOINT ["dotnet", "RustDeskApiServer.dll"]
