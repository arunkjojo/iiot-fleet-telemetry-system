"use client"
import React, { useState } from 'react'
import Link from 'next/link'
import { Satellite, Bell } from 'lucide-react'
import NotificationModal from './NotificationModal'
import ConnectionStatus, { type SignalRConnectionStatus } from './ConnectionStatus'
import { useNotificationStore } from '../store/useNotificationStore'

type Props = {
  connectionStatus?: SignalRConnectionStatus
}

export default function Header({ connectionStatus = 'disconnected' }: Props) {
  const [notifOpen, setNotifOpen] = useState(false)

  const unread = useNotificationStore.getState().notifications.filter(n => !n.read).length

  return (
    <>
      <header className="flex flex-wrap md:flex-nowrap items-center justify-between gap-y-2 md:whitespace-nowrap border-b border-solid border-border-dark bg-surface-dark px-3 sm:px-6 py-3 shrink-0 z-20">
        <Link href="/" className="flex items-center gap-2 sm:gap-4 text-white min-w-0">
          <div className="size-8 flex items-center justify-center text-primary shrink-0">
            <Satellite size={28} />
          </div>
          <h2 className="text-white text-base sm:text-xl font-bold leading-tight tracking-[0.03em] sm:tracking-[0.1em] uppercase truncate max-w-[60vw] sm:max-w-none">IIOT Fleet Telemetry Dashboard</h2>
        </Link>

        <div className="flex gap-2 sm:gap-3 items-center shrink-0">
          <Link href="/system-design" className="hidden md:inline text-sm text-primary">System Design</Link>

          <ConnectionStatus status={connectionStatus} />

          <div className="relative">
            <button aria-label="notifications" onClick={() => { setNotifOpen(true); }} className="text-white p-2 rounded hover:bg-white/5">
              <Bell size={18} />
            </button>
            {unread > 0 && (
              <div className="absolute -top-1 -right-1 bg-red-500 text-[10px] text-white rounded-full px-1.5 py-0.5 font-bold">{unread}</div>
            )}
          </div>

          <div className="h-9 w-9 rounded-full bg-gradient-to-br from-primary to-yellow-600 flex items-center justify-center text-black font-bold text-xs shrink-0">AJ</div>
        </div>
      </header>

      <NotificationModal open={notifOpen} onClose={() => setNotifOpen(false)} />
    </>
  )
}
