import React from 'react'

const SignalRPipeline = () => {
  return (
    <div className="flex flex-col md:flex-row items-center justify-between gap-4 p-8 bg-black/40 rounded-lg border border-border-dark">
      {/* Step 1: Backend Simulation */}
      <div className="flex-1 text-center p-4 border border-primary/20 bg-surface-dark rounded">
        <div className="text-primary text-xs font-bold mb-2 uppercase tracking-tighter">Producer</div>
        <div className="text-white text-sm font-semibold italic">.NET 8 Parallel Service</div>
        <div className="text-[10px] text-slate-500 mt-2">10k Concurrent Mutations</div>
      </div>

      <div className="hidden md:block text-primary animate-pulse">→</div>

      {/* Step 2: SignalR Hub */}
      <div className="flex-1 text-center p-4 border border-yellow-500/20 bg-surface-dark rounded relative">
        <div className="text-yellow-500 text-xs font-bold mb-2 uppercase tracking-tighter">Transport</div>
        <div className="text-white text-sm font-semibold italic">SignalR Hub</div>
        <div className="text-[10px] text-slate-500 mt-2">WebSocket Delta Stream</div>
      </div>

      <div className="hidden md:block text-primary animate-pulse">→</div>

      {/* Step 3: Frontend Reconciliation */}
      <div className="flex-1 text-center p-4 border border-green-500/20 bg-surface-dark rounded">
        <div className="text-green-500 text-xs font-bold mb-2 uppercase tracking-tighter">Consumer</div>
        <div className="text-white text-sm font-semibold italic">Zustand Store (Map)</div>
        <div className="text-[10px] text-slate-500 mt-2">O(1) State Reconciliation</div>
      </div>
    </div>
  )
}

export default SignalRPipeline
