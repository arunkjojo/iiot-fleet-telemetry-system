---
name: devops
description: Docker, Docker Compose, and GitHub Actions patterns for the IIoT Fleet Telemetry System. Activates for containerization tasks, CI/CD pipeline changes, health check configuration, and environment variable management.
---

# DevOps Skill — IIoT Fleet Telemetry Infrastructure

## Service Topology

```
db (postgres:16-alpine)
  └─ depends on: (none)
  
backend (ASP.NET Core 8)
  └─ depends on: db (service_healthy)
  
frontend (Next.js 15)
  └─ depends on: backend (service_healthy)
```

Network: `iiot-fleet-net` — all services communicate by service name.

## docker-compose.yml Reference

```yaml
services:
  db:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: fleet_telemetry
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5
    restart: unless-stopped

  backend:
    build: ./backend
    ports:
      - "8080:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      FRONTEND_ORIGIN: http://frontend:3000
      ADDITIONAL_FRONTEND_ORIGINS: http://localhost:3000
      ConnectionStrings__Fleet: "Host=db;Database=fleet_telemetry;Username=postgres;Password=postgres"
    depends_on:
      db:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8080/ || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 3
    restart: unless-stopped

  frontend:
    build: ./frontend
    ports:
      - "3000:3000"
    environment:
      NEXT_PUBLIC_API_URL: http://backend:8080
    depends_on:
      backend:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:3000/ || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 3
    restart: unless-stopped

volumes:
  postgres_data:

networks:
  default:
    name: iiot-fleet-net
```

## Backend Dockerfile Pattern

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY *.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "FleetTelemetry.dll"]
```

## Frontend Dockerfile Pattern

```dockerfile
FROM node:18-alpine AS deps
WORKDIR /app
COPY package*.json ./
RUN npm ci

FROM node:18-alpine AS builder
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .
RUN npm run build

FROM node:18-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production
COPY --from=builder /app/.next/standalone ./
COPY --from=builder /app/.next/static ./.next/static
COPY --from=builder /app/public ./public
EXPOSE 3000
ENV PORT=3000
CMD ["node", "server.js"]
```

Note: `standalone` output requires `output: 'standalone'` in `next.config.js`.

## GitHub Actions CI Pattern

```yaml
# .github/workflows/ci.yml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  backend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: cd backend && dotnet build

  frontend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'npm'
          cache-dependency-path: frontend/package-lock.json
      - run: cd frontend && npm ci
      - run: cd frontend && npm run type-check
      - run: cd frontend && npm run lint
      - run: cd frontend && npm run build
```

## Common Commands

```bash
# Start full stack
docker-compose up --build

# Start in background
docker-compose up --build -d

# View logs
docker-compose logs -f backend
docker-compose logs -f frontend
docker-compose logs -f db

# Restart single service
docker-compose restart backend

# Rebuild single service
docker-compose up --build -d backend

# Stop all
docker-compose down

# Stop and remove volumes (wipes DB)
docker-compose down -v

# Check health
docker-compose ps

# Exec into running container
docker-compose exec backend sh
docker-compose exec db psql -U postgres -d fleet_telemetry
```

## Environment Variable Rules

1. Never commit secrets — use `.env` files (gitignored) or Docker secrets
2. `NEXT_PUBLIC_*` variables are baked into the Next.js build at build time — not runtime-injectable
3. ASP.NET Core reads double-underscore env vars as nested config: `ConnectionStrings__Fleet` → `ConnectionStrings:Fleet`
4. Always provide both `FRONTEND_ORIGIN` (Docker) and `ADDITIONAL_FRONTEND_ORIGINS` (local dev) for CORS
