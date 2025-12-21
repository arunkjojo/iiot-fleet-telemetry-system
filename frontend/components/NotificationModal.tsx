"use client"
import React from 'react'
import { useNotificationStore } from '../store/useNotificationStore'

export default function NotificationModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const notifications = useNotificationStore((s) => s.notifications)
  const markRead = useNotificationStore((s) => s.markRead)
  const markAllRead = useNotificationStore((s) => s.markAllRead)

  if (!open) return null
  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center p-4">
      <div className="absolute inset-0 bg-black/40" onClick={onClose} />
      <div className="relative w-full max-w-md bg-surface-dark border border-border-dark rounded-lg shadow-lg overflow-hidden">
        <div className="p-4 border-b border-border-dark flex items-center justify-between">
          <h3 className="text-white font-semibold">Notifications</h3>
          <div className="flex items-center gap-2">
            <button onClick={() => markAllRead()} className="text-sm text-slate-300 hover:text-white">Mark all read</button>
            <button onClick={onClose} className="text-sm text-slate-300 hover:text-white">Close</button>
          </div>
        </div>
        <div className="max-h-96 overflow-auto">
          {notifications.length === 0 && <div className="p-4 text-slate-400">No notifications</div>}
          {notifications.map((n) => (
            <div key={n.id} className={`p-3 border-b border-border-dark flex items-start justify-between ${n.read ? 'opacity-60' : ''}`}>
              <div>
                <div className="text-xs text-slate-400">{new Date(n.ts).toLocaleString()}</div>
                <div className="text-sm text-white">{n.message}</div>
              </div>
              <div className="flex flex-col items-end gap-2">
                <div className={`text-xs px-2 py-0.5 rounded ${n.level === 'error' ? 'bg-red-600' : n.level === 'warn' ? 'bg-yellow-600' : 'bg-green-600'}`}>{n.level.toUpperCase()}</div>
                {!n.read && <button onClick={() => markRead(n.id)} className="text-xs text-slate-300 hover:text-white">Mark</button>}
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}
