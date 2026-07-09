---
name: nextjs
description: Next.js 15 App Router patterns and conventions for the IIoT Fleet Telemetry frontend. Activates for frontend component work, state management, SignalR client integration, and performance optimization tasks.
---

# Next.js 15 Skill — IIoT Fleet Telemetry Frontend

## App Router Fundamentals

```
app/
├── layout.tsx      # Server component — wraps all pages
├── page.tsx        # Client component ('use client') — main dashboard
└── api/            # Route Handlers (if added) — server-side API endpoints
```

- `layout.tsx` is a **server component** — no hooks, no browser APIs
- `page.tsx` is a **client component** (`'use client'` at top) — owns SignalR connection and vehicle state
- Add `'use client'` to any file that uses `useState`, `useEffect`, `useRef`, or browser APIs

## SignalR Client Pattern

```typescript
// frontend/app/page.tsx — one connection per session
import * as signalR from '@microsoft/signalr';

const conn = new signalR.HubConnectionBuilder()
  .withUrl(`${process.env.NEXT_PUBLIC_API_URL}/fleethub`)
  .withAutomaticReconnect()
  .build();

await conn.start();
conn.on('ReceiveFleetUpdate', (updates: VehicleUpdate[]) => {
  // mutate in-place, then flush to state
  updates.forEach(u => {
    const v = vehiclesMap.current.get(u.id);
    if (v) Object.assign(v, u);
  });
  setVehicles(prev => Array.from(vehiclesMap.current.values()));
});
```

Never create more than one SignalR connection. Store it in `useRef<HubConnection | null>`.

## State Management (Zustand)

```typescript
// frontend/store/useFilterStore.ts
import { create } from 'zustand';

interface FilterStore {
  selectedStatuses: VehicleStatus[];
  toggleStatus: (status: VehicleStatus) => void;
}

export const useFilterStore = create<FilterStore>((set) => ({
  selectedStatuses: ['all'],
  toggleStatus: (status) => set((state) => {
    if (status === 'all') return { selectedStatuses: ['all'] };
    const next = state.selectedStatuses.filter(s => s !== 'all');
    return next.includes(status)
      ? { selectedStatuses: next.filter(s => s !== status) || ['all'] }
      : { selectedStatuses: [...next, status] };
  }),
}));
```

Never use React Context for cross-component state. Zustand only.

## Virtualized List Pattern

```typescript
import { useVirtualizer } from '@tanstack/react-virtual';

// in Sidebar.tsx — renders only visible rows of 10k vehicle list
const virtualizer = useVirtualizer({
  count: filteredVehicles.length,
  getScrollElement: () => parentRef.current,
  estimateSize: () => 56,   // row height in px
  overscan: 5,
});

return (
  <div ref={parentRef} style={{ overflow: 'auto', height: '100%' }}>
    <div style={{ height: `${virtualizer.getTotalSize()}px`, position: 'relative' }}>
      {virtualizer.getVirtualItems().map(row => (
        <div key={row.key} style={{ position: 'absolute', top: row.start }}>
          <VehicleRow vehicle={filteredVehicles[row.index]} />
        </div>
      ))}
    </div>
  </div>
);
```

## Performance Patterns

```typescript
// Memoize expensive computations
const alerts = useMemo(() =>
  vehicles.filter(v => v.fuel < 20 || v.temp > 65 || v.speedKph > 80),
  [vehicles]
);

// Memoize components receiving stable props
const MemoSidebar = React.memo(Sidebar);
const MemoMapView = React.memo(MapView);

// Debounce search
const [query, setQuery] = useState('');
const debouncedQuery = useDebounce(query, 160);
```

## Tailwind Conventions

```typescript
// Use theme tokens, not raw values
className="bg-[#121212]"    // WRONG — use CSS var
className="bg-surface"       // RIGHT — if configured in tailwind.config.js

// Status color mapping
const statusColors = {
  active:  'text-green-400',
  warning: 'text-yellow-400',
  danger:  'text-red-500',
  offline: 'text-gray-500',
};
```

## API Fetch Pattern

```typescript
// frontend/app/page.tsx — initial vehicle load
const res = await fetch(`${process.env.NEXT_PUBLIC_API_URL}/api/vehicles`);
if (!res.ok) throw new Error(`API error: ${res.status}`);
const data: ApiVehicle[] = await res.json();

// Normalize field name aliases (backend may use PascalCase or camelCase)
const normalized: Vehicle[] = data.map(v => ({
  id: v.id ?? v.Id,
  fuel: v.fuel ?? v.FuelPercent ?? 100,
  temp: v.temp ?? v.Temp ?? 60,
  speedKph: v.speedKph ?? v.SpeedKph ?? 0,
  // ...
}));
```

## Type Conventions

```typescript
// frontend/types/vehicle.ts
export type VehicleStatus = 'active' | 'warning' | 'danger' | 'offline';

export interface Vehicle {
  id: string;
  model: string;
  driver: string;
  status: VehicleStatus;
  fuel: number;
  temp: number;
  speedKph: number;
  cargoLoad: number;
  engineHealth: number;
  lat: number;
  lng: number;
}

// Subset received via SignalR
export interface VehicleUpdate {
  id: string;
  lat: number;
  lng: number;
  fuel: number;
  speedKph: number;
  engineHealth: number;
  status: VehicleStatus;
  temp: number;
}
```

## Verification

```bash
cd frontend
npm run type-check   # must pass with zero errors
npm run lint         # must pass with zero warnings
npm run build        # must succeed
```
