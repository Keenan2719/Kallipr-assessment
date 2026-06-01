# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["task-1-backend/Kallipr.Telemetry.Api/Kallipr.Telemetry.Api.csproj", "task-1-backend/Kallipr.Telemetry.Api/"]
RUN dotnet restore "task-1-backend/Kallipr.Telemetry.Api/Kallipr.Telemetry.Api.csproj"

COPY . .
RUN dotnet publish "task-1-backend/Kallipr.Telemetry.Api/Kallipr.Telemetry.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ConnectionStrings__Sqlite=/data/telemetry.db

VOLUME ["/data"]
EXPOSE 8080

ENTRYPOINT ["dotnet", "Kallipr.Telemetry.Api.dll"]
