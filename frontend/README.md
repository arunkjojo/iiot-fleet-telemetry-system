# 🛰️ Fleet-Command-UI
**High-Performance Real-Time Asset Tracking Dashboard**

[![Next.js](https://img.shields.io/badge/Next.js-15-black?logo=next.js)](https://nextjs.org/)
[![Mapbox](https://img.shields.io/badge/Mapbox-SDK-blue?logo=mapbox)](https://mapbox.com/)

## 📖 The "What"
A mission-critical visualization layer built to monitor 10,000+ active vehicle units. It provides a "Single Pane of Glass" view for fleet managers, combining geospatial data with high-fidelity mechanical telemetry logs.

## 🎯 The "Why"
Traditional dashboards fail at high scale (10k+ items). We built this to prove that a modern React environment can maintain **60FPS** while processing high-frequency (500ms) data streams. It solves the "DOM Bloat" problem via advanced virtualization and GPU-accelerated map layers.

## 💻 Tech Stack
- **Next.js 15 (App Router):** For streaming SSR and routing.
- **TanStack Virtual:** To handle the 10,000-unit sidebar list efficiently.
- **Mapbox GL JS / Deck.gl:** GPU-accelerated rendering for 10k+ markers.
- **Zustand:** Low-boilerplate, high-performance global state management.
- **Tailwind CSS:** For the "Industrial Dark" aerospace aesthetic.

## 🚀 Implementation Highlights
1. **The 10k List:** Using `@tanstack/react-virtual`, the DOM only ever contains ~20 rows, despite the 10k data points in memory.
2. **GPU Rendering:** Markers are rendered using a Mapbox **Symbol Layer** (GeoJSON) rather than individual React components, offloading movement math to the GPU.
3. **Throttled Updates:** Incoming SignalR packets are buffered and committed to state every 100ms to prevent React "thrashing."

## 🛠️ Setup
```bash
cd frontend
npm install
npm run dev