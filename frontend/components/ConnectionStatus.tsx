"use client"
import React from 'react'

export type SignalRConnectionStatus = 'connected' | 'reconnecting' | 'disconnected'

type Props = {
  status: SignalRConnectionStatus
}

const dotColor = (status: SignalRConnectionStatus) => {
  if (status === 'connected') return 'bg-[#0bda54]'
  if (status === 'reconnecting') return 'bg-[#f59e0b]'
  return 'bg-[#ef4444]'
}

const label = (status: SignalRConnectionStatus) => {
  if (status === 'connected') return 'Connected'
  if (status === 'reconnecting') return 'Reconnecting'
  return 'Disconnected'
}

export default function ConnectionStatus({ status }: Props) {
  return (
    <div
      className="flex items-center gap-2 text-xs text-white/80"
      role="status"
      aria-label={`SignalR connection status: ${label(status)}`}
    >
      <span
        className={`w-2 h-2 rounded-full ${dotColor(status)} ${status !== 'disconnected' ? 'animate-pulse' : ''}`}
      />
      <span className="uppercase tracking-wide">{label(status)}</span>
    </div>
  )
}
