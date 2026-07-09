# Frontend Subsystem — Agent Guide (NEXT)

Read this file in full before touching any file under `frontend/`.

---

## Stack

| Technology | Version | Purpose |
|-----------|---------|---------|
| Next.js | 15 (App Router) | React framework, routing, SSR/CSR |
| React | 18 | UI rendering |
| TypeScript | 5.5+ | Type safety |
| Tailwind CSS | 3.4 | Utility-first styling |
| Zustand | 4.4 | Global state management |
| @microsoft/signalr | 7.0 | Real-time WebSocket updates |
| @tanstack/react-virtual | 3.0 | Virtualized list rendering |
| Framer Motion | 10 | Animations |
| Lucide React | 0.268 | Icon library |

---

## Directory Map

```
frontend/
├── app/
│   ├── layout.tsx              # Root layout — wraps Header, loads fonts
│   ├── page.tsx                # Main dashboard — SignalR connection + vehicle state
│   ├── globals.css             # Tailwind directives + custom animations
│   └── system-design/
│       └── page.tsx            # Architecture documentation page
├── components/
│   ├── Header.tsx              # Top nav bar, notification bell
│   ├── Sidebar.tsx             # Virtualized vehicle list, search, status filters
│   ├── MapView.tsx             # Map visualization of vehicle positions
│   ├── DetailPanel.tsx         # Right panel — telemetry gauges + logs
│   ├── NotificationModal.tsx   # Persistent notification list modal
│   ├── Toast.tsx               # Auto-dismiss alert toast (2s)
│   └── SignalRPipeline.tsx     # Architecture visualization component
├── store/
│   ├── useFilterStore.ts       # Selected vehicle status filters
│   └── useNotificationStore.ts # Notification add/read/clear state
├── types/
│   └── vehicle.ts              # Vehicle TypeScript interface
├── data/
│   ├── vehicleListDummy.json   # 5-vehicle sample for offline testing
│   └── DUMMY.json              # Large dummy dataset (~3.5 MB)
├── public/                     # Static assets
├── .env                        # NEXT_PUBLIC_API_URL=http://localhost:8080
├── next.config.js
├── tailwind.config.js
├── tsconfig.json
└── Dockerfile
```

---

## Vehicle Type Contract

```typescript
// frontend/types/vehicle.ts
interface Vehicle {
  id: string;           // e.g. "VEH-00001"
  model: string;        // "NV Cargo" | "Apex Hauler"
  driver: string;       // Driver name
  status: 'active' | 'warning' | 'danger' | 'offline';
  fuel: number;         // 0-100 (%)
  temp: number;         // degrees Celsius
  speedKph: number;     // km/h
  cargoLoad: number;    // kg
  engineHealth: number; // 0-100
  lat: number;          // Latitude
  lng: number;          // Longitude
}
```

---

## State Management Rules

1. **Zustand stores** for all cross-component state — never use React Context or prop drilling across 2+ levels.
2. **`useRef<Map<string, Vehicle>>`** in `page.tsx` for the live vehicle map — enables O(1) SignalR updates without triggering re-renders on every tick.
3. **`useState<Vehicle[]>`** for the rendered list — only updated on a debounced flush, not on every SignalR message.
4. Never import Zustand stores inside server components.

---

## SignalR Integration Pattern

```typescript
// Established in frontend/app/page.tsx
const conn = new HubConnectionBuilder()
  .withUrl(`${process.env.NEXT_PUBLIC_API_URL}/fleethub`)
  .withAutomaticReconnect()
  .build();

conn.on('ReceiveFleetUpdate', (updates: VehicleUpdate[]) => {
  updates.forEach(u => {
    const existing = vehiclesMap.current.get(u.id);
    if (existing) Object.assign(existing, u);
  });
  // flush to state at most once per animation frame
  setVehicles(Array.from(vehiclesMap.current.values()));
});
```

- One connection per session (stored in `useRef`)
- `ReceiveFleetUpdate` receives batch array of partial updates
- Map mutation is in-place to avoid GC pressure from spreading 10k objects

---

## Performance Rules

1. **Virtualize every list > 100 items** using `@tanstack/react-virtual`. The Sidebar already implements this for the 10k vehicle list.
2. **Memoize components** that receive stable props — use `React.memo` on Sidebar, MapView, DetailPanel.
3. **Debounce search input** — minimum 160ms delay before filtering the vehicle list.
4. **Never call `setVehicles` on every SignalR message** — batch updates into a single state flush.
5. **Token-based search index** — build a lookup Map on initial load; do not `.filter()` a 10k array on every keystroke.

---

## Tailwind Theme

```js
// tailwind.config.js key tokens
// primary: '#f9f506'  (bright yellow — highlights, active states)
// CSS variables in globals.css:
// --bg-dark: #0a0a0a
// --surface: #121212
// --border: #283639
```

Font: Space Grotesk (loaded via Google Fonts in `app/layout.tsx`).

---

## Alert Threshold Logic

Alerts fire when any of these conditions are true (checked in `page.tsx`):

| Condition | Threshold |
|-----------|-----------|
| Low fuel | `fuel < 20` |
| High temperature | `temp > 65` |
| High speed | `speedKph > 80` |
| Low engine health | `engineHealth < 15` |
| Status | `status === 'danger'` |

On threshold breach AND status change: add to `useNotificationStore` + show `<Toast />`.

---

## Coding Conventions

- **File naming:** PascalCase for components (`Sidebar.tsx`), camelCase for stores (`useFilterStore.ts`), kebab-case for page directories (`system-design/page.tsx`)
- **No `any` types** — use `unknown` + type guard if shape is uncertain
- **JSON API normalization:** handle both camelCase and PascalCase field aliases from backend responses
- **No hardcoded URLs** — always use `process.env.NEXT_PUBLIC_API_URL`
- **No `console.log`** in committed code

---

## Environment Variables

| Variable | Used In | Value (dev) |
|----------|---------|-------------|
| `NEXT_PUBLIC_API_URL` | `page.tsx`, `DetailPanel.tsx` | `http://localhost:8080` |

In Docker Compose, `NEXT_PUBLIC_API_URL=http://backend:8080` is injected at build time.

---

## Development Commands

```bash
cd frontend
npm install          # install dependencies
npm run dev          # dev server at http://localhost:3000
npm run build        # production build
npm run type-check   # tsc --noEmit — must pass with zero errors
npm run lint         # ESLint — must pass with zero warnings
```

---

## API Endpoints Consumed

| Method | URL | Purpose |
|--------|-----|---------|
| GET | `{API_URL}/api/vehicles` | Initial fleet load (10k vehicles) |
| GET | `{API_URL}/api/vehicles/{id}` | Vehicle detail + recent logs |
| GET | `{API_URL}/api/vehicles/{id}/logs` | Vehicle telemetry log history |
| WS | `{API_URL}/fleethub` | SignalR hub for live updates |

---

## Do NOT Touch

- `frontend/data/DUMMY.json` — large test fixture; do not modify
- `frontend/scripts/` — data generation utilities; not part of the app runtime
