import React from 'react'
import SignalRPipeline from '../../components/SignalRPipeline'
import Header from '../../components/Header'

export const metadata = {
  title: 'System Design',
}

export default function Page() {
  return (
    <>
      <Header />
      <main className="max-w-6xl mx-auto p-8 space-y-12">
      <section>
        <h2 className="text-white text-xl font-bold mb-4 border-l-4 border-primary pl-4 uppercase tracking-widest">Architectural Vision</h2>
        <p className="leading-relaxed">
          The <span className="text-primary">IIOT Fleet Telemetry Engine</span> is a high-throughput telemetry platform designed to solve the "Real-Time Density" problem. 
          Handling 10,000 concurrent assets requires a move away from traditional polling to a <span className="text-white font-semibold">Reactive Stream-and-Broadcast</span> pattern.
        </p>
      </section>

      <section className="bg-surface border border-border rounded-xl p-8 flex flex-col items-center">
         <h3 className="text-slate-500 text-xs font-bold uppercase mb-8 tracking-widest">Logic Flow: Rest-to-Stream Hydration</h3>
         <div className="w-full max-w-3xl aspect-video border border-border/50 rounded flex items-center justify-center bg-black/50 relative">
            <SignalRPipeline />
         </div>
      </section>

      <section className="grid md:grid-cols-3 gap-6">
          <div className="bg-surface border border-border p-6 rounded-lg">
              <div className="text-primary font-bold text-sm mb-2">01 / VIRTUALIZATION</div>
              <h4 className="text-white font-bold mb-2">O(1) DOM Management</h4>
              <p className="text-xs text-slate-400">Utilizing TanStack Virtual to maintain 60FPS. The DOM only renders ~25 units regardless of the 10,000 items in memory.</p>
          </div>
          <div className="bg-surface border border-border p-6 rounded-lg">
              <div className="text-primary font-bold text-sm mb-2">02 / GPU ACCELERATION</div>
              <h4 className="text-white font-bold mb-2">WebGL Map Layers</h4>
              <p className="text-xs text-slate-400">Bypassing React DOM for map markers. 10k positions are offloaded to Mapbox GeoJSON layers for native hardware acceleration.</p>
          </div>
          <div className="bg-surface border border-border p-6 rounded-lg">
              <div className="text-primary font-bold text-sm mb-2">03 / .NET CONCURRENCY</div>
              <h4 className="text-white font-bold mb-2">Parallel Simulation</h4>
              <p className="text-xs text-slate-400">Non-blocking BackgroundService using Parallel.ForEach to mutate 10k telemetry points per 500ms cycle.</p>
          </div>
      </section>

      <section>
          <h3 className="text-white text-lg font-bold mb-4 uppercase">Technical Decision Matrix</h3>
          <div className="overflow-hidden border border-border rounded-lg">
              <table className="w-full text-left text-sm">
                  <thead className="bg-black text-slate-500 font-bold text-[10px] uppercase tracking-widest">
                      <tr>
                          <th className="p-4 border-b border-border">Component</th>
                          <th className="p-4 border-b border-border">Technology</th>
                          <th className="p-4 border-b border-border">Rationale</th>
                      </tr>
                  </thead>
                  <tbody className="divide-y divide-border bg-surface">
                      <tr>
                          <td className="p-4 font-mono text-primary">State Management</td>
                          <td className="p-4">Zustand (Transient)</td>
                          <td className="p-4 text-xs">Avoids React Context "re-render hell" for high-frequency updates.</td>
                      </tr>
                      <tr>
                          <td className="p-4 font-mono text-primary">Data Protocol</td>
                          <td className="p-4">SignalR (Binary ready)</td>
                          <td className="p-4 text-xs">Sub-500ms broadcast latency with WebSocket persistence.</td>
                      </tr>
                      <tr>
                          <td className="p-4 font-mono text-primary">Containerization</td>
                          <td className="p-4">Docker Desktop</td>
                          <td className="p-4 text-xs">Simulates network topology and environment parity.</td>
                      </tr>
                  </tbody>
              </table>
          </div>
      </section>

      <footer className="pt-12 border-t border-border text-center">
          <p className="text-slate-500 text-sm">Designated for technical recruitment review.</p>
          <div className="mt-4 flex justify-center gap-4">
              <span className="text-primary text-xs font-mono tracking-tighter">BUILD_SUCCESSFUL</span>
              <span className="text-slate-700 text-xs font-mono">NODE_V20.x</span>
              <span className="text-slate-700 text-xs font-mono">DOTNET_8.0</span>
          </div>
      </footer>
      </main>
    </>
  )
}
