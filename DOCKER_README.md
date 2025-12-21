# Running the Fleet Telemetry System with Docker

This repository includes Dockerfiles for both the backend (.NET 8) and the frontend (Next.js), plus a `docker-compose.yml` to run both together.

Quick start

1. Build and start services:

```bash
docker-compose up --build
```

2. After startup:
- Frontend UI: http://localhost:3000
- Backend API: http://localhost:8080

Environment

- The frontend uses `NEXT_PUBLIC_API_URL` to locate the backend. `docker-compose.yml` sets it to `http://backend:8080` so the frontend container talks to the backend container.

Custom ports

To change the host ports, edit `docker-compose.yml` port mappings.

Building images only

```bash
docker-compose build
```

Stopping

```bash
docker-compose down
```

Notes

- Backend: multi-stage build using the .NET 8 SDK and ASP.NET runtime. It listens on port 8080 inside the container.
- Frontend: builds a production Next.js app and runs it with `npm run start` on port 3000.
