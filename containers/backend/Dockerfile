# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy csproj and restore
COPY FleetTelemetry.csproj ./
RUN dotnet restore "FleetTelemetry.csproj"

# copy everything and publish
COPY . ./
RUN dotnet publish "FleetTelemetry.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "FleetTelemetry.dll"]
