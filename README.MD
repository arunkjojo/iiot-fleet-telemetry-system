
# IIoT Fleet Telemetry System

**Full documentation:** [docs/PROJECT_OVERVIEW.md](docs/PROJECT_OVERVIEW.md) — architecture, key
design decisions, DevOps/infrastructure, the AI-assisted development workflow, project history,
and getting started, all in one onboarding document.

**Process:** [docs/SDD_WORKFLOW.md](docs/SDD_WORKFLOW.md) — the Spec-Driven Development loop this
project follows: requirements → sprint files → task execution → verification → changelog.

**Application Overview:** [docs/APPLICATION_OVERVIEW.md](docs/APPLICATION_OVERVIEW.md) — what this
system is, its frontend/backend/db/emitter topology, and the dummy-mode vs. live-mode data flows.

**DevOps Learning Guides:** [docs/devops-learn/](docs/devops-learn/) — Docker Compose, Helm, and
Kubernetes concepts explained, then mapped onto this project's actual stack.

**Project Overview:**
- **What:** A high-performance real-time fleet telemetry system consisting of a .NET 8 SignalR backend simulator and a Next.js 15 frontend dashboard for visualizing and interacting with large-scale vehicle telemetry.
- **Why:** To demonstrate a production-ready pipeline capable of tracking 10,000+ vehicles with low-latency updates, GPU-accelerated map rendering, and efficient UI virtualization.
- **Who:** Backend engineers (simulation, SignalR), frontend engineers (Mapbox/Deck.gl, React/Next.js), and system architects evaluating real-time telemetry designs.

**Architecture**
- **Backend:** [backend](backend) — an ASP.NET Core 8 app that simulates vehicle telemetry, hosts a SignalR hub (`/fleethub`) and exposes REST metadata/log endpoints. It supports MessagePack for efficient payloads.
- **Frontend:** [frontend](frontend) — a Next.js 15 app (App Router) that connects to the SignalR hub, renders 10k+ vehicles using GPU-accelerated map layers and a virtualized sidebar list.

**System Design (high level)**
- **Data Flow:** Backend simulation loop mutates in-memory vehicle state → broadcasts updates via SignalR (MessagePack optional) → Frontend buffers/throttles updates and commits to state every ~100ms.
- **Scalability Techniques:** ConcurrentDictionary and object reuse on the backend to reduce GC pressure; Mapbox symbol layers and virtualized lists on the frontend to avoid DOM bloat; throttled state commits to prevent React thrashing.
- **Real-time Considerations:** Designed for sub-500ms broadcast loops and frontend commit windows tuned for 60FPS UI responsiveness under heavy update rates.

**Quickstart**
- **Backend** (from `backend`):

```bash
cd backend
dotnet restore
dotnet run
```

- **Frontend** (from `frontend`):

```bash
cd frontend
npm install
npm run dev
```

**Docker**
- See [docs/DOCKER_README.md](docs/DOCKER_README.md) for full Docker instructions and notes.
- See [docs/HELM_GUIDE.md](docs/HELM_GUIDE.md) for deploying to Kubernetes via Helm.
- Quick Docker compose (builds images and starts both services):

```bash
docker compose -f containers/docker-compose.yml up --build
```

After startup:
- Frontend UI: http://localhost:3000
- Backend API: http://localhost:8080

**Useful Files & Paths**
- **Backend README:** [backend/README.md](backend/README.md)
- **Frontend README:** [frontend/README.md](frontend/README.md)
- **SignalR Hub:** [backend/Hubs/FleetHub.cs](backend/Hubs/FleetHub.cs)
- **Frontend Map + Components:** [frontend/components](frontend/components)

**Next Steps / Tips**
- Toggle MessagePack in client and server to compare bandwidth and latency.
- Adjust frontend throttling interval (`SignalRPipeline`/buffer commit) to balance latency vs. UI stability.
- Use the included dummy data in `frontend/data` for offline UI testing.

---
Maintained by the project authors — use this file as the canonical project entry for developers and reviewers.

