import create from 'zustand'

export type VehicleStatus = 'all' | 'active' | 'warning' | 'danger' | 'offline'

type FilterState = {
  selectedStatuses: VehicleStatus[]
  setSelectedStatuses: (s: VehicleStatus[]) => void
  toggleStatus: (s: VehicleStatus) => void
}

export const useFilterStore = create<FilterState>((set, get) => ({
  selectedStatuses: ['all'],
  setSelectedStatuses: (s) => set({ selectedStatuses: s }),
  toggleStatus: (s) => {
    const cur = get().selectedStatuses
    if (s === 'all') {
      // selecting 'all' clears other selections
      set({ selectedStatuses: ['all'] })
      return
    }
    const next = new Set(cur.filter((x): x is Exclude<VehicleStatus, 'all'> => x !== 'all'))
    if (next.has(s)) next.delete(s)
    else next.add(s)
    const arr = Array.from(next) as VehicleStatus[]
    if (arr.length === 0) arr.push('all')
    set({ selectedStatuses: arr })
  }
}))
