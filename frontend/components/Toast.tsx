"use client"
import React, { useEffect } from 'react'
import { useNotificationStore } from '../store/useNotificationStore'

export default function Toast({ item, onDone }: { item: { id: string; message: string; level: string } | null; onDone: () => void }) {
  useEffect(() => {
    if (!item) return
    const t = setTimeout(() => onDone(), 2000)
    return () => clearTimeout(t)
  }, [item, onDone])

  if (!item) return null
  return (
    <div className="fixed left-4 bottom-6 z-50 animate-slide-up">
      <div className={`px-4 py-3 rounded shadow-lg ${item.level === 'error' ? 'bg-red-600' : item.level === 'warn' ? 'bg-yellow-600' : 'bg-slate-800'} text-white`}>
        {item.message}
      </div>
    </div>
  )
}
