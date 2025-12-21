import create from 'zustand'

export type NotificationLevel = 'info' | 'warn' | 'error'

export type AppNotification = {
  id: string
  ts: string
  level: NotificationLevel
  message: string
  read?: boolean
}

type NotificationState = {
  notifications: AppNotification[]
  add: (n: Omit<AppNotification, 'id' | 'ts' | 'read'>) => AppNotification
  markRead: (id: string) => void
  markAllRead: () => void
  clear: () => void
}

export const useNotificationStore = create<NotificationState>((set, get) => ({
  notifications: [],
  add: (n) => {
    const notif: AppNotification = { id: Math.random().toString(36).slice(2), ts: new Date().toISOString(), read: false, ...n }
    set((s) => ({ notifications: [notif, ...s.notifications] }))
    return notif
  },
  markRead: (id) => set((s) => ({ notifications: s.notifications.map(n => n.id === id ? { ...n, read: true } : n) })),
  markAllRead: () => set((s) => ({ notifications: s.notifications.map(n => ({ ...n, read: true })) })),
  clear: () => set({ notifications: [] })
}))
