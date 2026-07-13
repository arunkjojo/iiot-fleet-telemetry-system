"use client"
import React, { useMemo, useRef, useState, useCallback, useEffect } from 'react'
import { useVirtualizer } from '@tanstack/react-virtual'
import { Vehicle } from '../types/vehicle'
import { Search } from 'lucide-react'
import { useFilterStore, VehicleStatus } from '../store/useFilterStore'

type Props = {
  vehicles: Vehicle[]
  onSelect: (v: Vehicle) => void
  selectedId?: string
}

function Sidebar({ vehicles, onSelect, selectedId }: Props) {
  const [inputValue, setInputValue] = useState('')
  const [query, setQuery] = useState('')
  const [focusedIdx, setFocusedIdx] = useState<number>(-1)

  // Build a token -> id set index for near O(1) lookups (built once per vehicles change)
  // Time: O(N * T) to build where T is tokens per vehicle; done on vehicles change only.
  const { idMap, tokenIndex } = useMemo(() => {
    const idMap = new Map<string, Vehicle>()
    const tokenIndex: Record<string, Set<string>> = {}

    for (const v of vehicles) {
      idMap.set(v.id, v)
      // normalize searchable fields
      const tokens = `${v.id} ${v.driver} ${v.model}`.toLowerCase().split(/\s+/).filter(Boolean)
      for (const t of tokens) {
        if (!tokenIndex[t]) tokenIndex[t] = new Set()
        tokenIndex[t].add(v.id)
      }
    }

    return { idMap, tokenIndex }
  }, [vehicles])

  // Use the precomputed tokenIndex to perform fast lookups. Avoids filtering the full array on each keystroke.
  const selectedStatuses = useFilterStore((s) => s.selectedStatuses)
  const toggleStatus = useFilterStore((s) => s.toggleStatus)
  const hideInactive = useFilterStore((s) => s.hideInactive)
  const toggleHideInactive = useFilterStore((s) => s.toggleHideInactive)

  // status priority used across the component (lower = higher priority)
  const STATUS_PRIORITY = ['danger', 'warning', 'offline', 'active'] as const
  const statusRank: Record<string, number> = {
    danger: 0,
    warning: 1,
    offline: 2,
    active: 3
  }

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase()
    // sort priority: danger -> warning -> offline -> active
    const compare = (a: Vehicle, b: Vehicle) => {
      const r = (statusRank[a.status] ?? 3) - (statusRank[b.status] ?? 3)
      if (r !== 0) return r
      return a.id.localeCompare(b.id)
    }

    // Start with either the full sorted list (when no query) or the token-indexed results
    let out: Vehicle[] = []
    if (!q) {
      out = [...vehicles].slice().sort(compare)
    } else {
      const tokens = q.split(/\s+/).filter(Boolean)
      if (tokens.length === 0) {
        out = [...vehicles].slice().sort(compare)
      } else {
        // intersect sets for tokens
        let resultSet: Set<string> | null = null
        for (const t of tokens) {
          const set = tokenIndex[t]
          if (!set) {
            // no matches for token
            resultSet = new Set()
            break
          }
          if (resultSet === null) {
            // first token: clone set
            resultSet = new Set(set)
          } else {
            // intersect
            for (const id of Array.from(resultSet)) {
              if (!set.has(id)) resultSet.delete(id)
            }
          }
        }

        const ids = resultSet ? Array.from(resultSet) : []
        // map ids to vehicles using idMap — O(k) where k is number of matches
        out = ids.map((id) => idMap.get(id)!).filter(Boolean)

        // If token-index returns nothing, or to support character-level substrings,
        // fall back to a linear substring scan for queries longer than 2 chars.
        // This is debounced (160ms) so it won't run on every keystroke.
        if ((out.length === 0 && q.length > 0) || q.length >= 3) {
          const substr = q
          const fallback = vehicles.filter((v) => {
            const hay = `${v.id} ${v.driver} ${v.model}`.toLowerCase()
            return hay.includes(substr)
          })
          // merge uniques: ensure we include both token-index results and substring matches
          const seen = new Set(out.map((v) => v.id))
          for (const v of fallback) {
            if (!seen.has(v.id)) {
              out.push(v)
              seen.add(v.id)
            }
          }
        }
      }
    }

    // apply status filter from global store
    // - If exactly one non-'all' status is selected, show only that status.
    // - If 'all' is selected, show everything (no filtering).
    // - If multiple statuses are selected, show the union of those statuses (ordered by priority below).
    {
      const sel = selectedStatuses as VehicleStatus[]
      if (sel.length === 1 && sel[0] !== 'all') {
        out = out.filter((v) => v.status === sel[0])
      } else if (sel.includes('all')) {
        // no-op: show all vehicles (out currently contains search results)
      } else {
        const setSel = new Set(sel)
        out = out.filter((v) => setSel.has(v.status as VehicleStatus))
      }
    }

    // apply "Hide Inactive" filter (client-side display concept, opt-in, default off)
    if (hideInactive) {
      out = out.filter((v) => !v.inactive)
    }

    // apply 24h-activity filter — only when a search query is active (Sprint 04 UI-013,
    // decision #3). Browsing the unfiltered/status-filtered list is unaffected. A missing
    // lastSeenAtUtc is treated as "don't exclude" (defensive default).
    if (q) {
      const DAY_MS = 24 * 60 * 60 * 1000
      out = out.filter((v) => !v.lastSeenAtUtc || (Date.now() - new Date(v.lastSeenAtUtc).getTime()) <= DAY_MS)
    }

    // sort matches by status priority so warnings are shown at top
    return out.sort(compare)
  }, [query, tokenIndex, idMap, vehicles, JSON.stringify(selectedStatuses), hideInactive])


  // debounce inputValue -> query so heavy lookups don't run on every keystroke
  useEffect(() => {
    const t = setTimeout(() => setQuery(inputValue.trim().toLowerCase()), 160)
    return () => clearTimeout(t)
  }, [inputValue])

  // Highlight the first matching substring for the provided query.
  const highlight = useCallback((text: string, q: string) => {
    if (!q) return text
    const l = text.toLowerCase()
    const qi = l.indexOf(q)
    if (qi === -1) return text
    return (
      <>
        {text.slice(0, qi)}
        <span className="match">{text.slice(qi, qi + q.length)}</span>
        {text.slice(qi + q.length)}
      </>
    )
  }, [])

  // Small inline "last seen" relative-time formatter — no new date library dependency.
  // Only rendered on rows when a search query is active (Sprint 04 UI-013).
  const formatLastSeen = useCallback((iso?: string) => {
    if (!iso) return null
    const diffMs = Date.now() - new Date(iso).getTime()
    if (Number.isNaN(diffMs)) return null
    if (diffMs < 60_000) return 'just now'
    const mins = Math.floor(diffMs / 60_000)
    if (mins < 60) return `${mins}m ago`
    const hours = Math.floor(mins / 60)
    if (hours < 24) return `${hours}h ago`
    const days = Math.floor(hours / 24)
    return `${days}d ago`
  }, [])

  const statusColor = (s: string) => {
    if (s === 'active') return 'bg-[#0bda54]'
    if (s === 'warning') return 'bg-[#f59e0b]'
    if (s === 'danger') return 'bg-[#ef4444]'
    return 'bg-[#9ca3af]'
  }

  const parentRef = useRef<HTMLDivElement | null>(null)

  // Virtualizer: only render visible rows. This keeps memory and paint costs stable for 10k+ items.
  const rowHeight = 72
  const virtualizer = useVirtualizer({
    count: filtered.length,
    getScrollElement: () => parentRef.current,
    estimateSize: () => rowHeight,
    overscan: 6,
  })

  const virtualItems = virtualizer.getVirtualItems()

  const handleKeyDown = useCallback((e: React.KeyboardEvent) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      setFocusedIdx((s) => Math.min(filtered.length - 1, Math.max(0, s + 1)))
      return
    }
    if (e.key === 'ArrowUp') {
      e.preventDefault()
      setFocusedIdx((s) => Math.max(0, s - 1))
      return
    }
    if (e.key === 'Enter') {
      e.preventDefault()
      if (focusedIdx >= 0 && focusedIdx < filtered.length) onSelect(filtered[focusedIdx])
    }
  }, [filtered, focusedIdx, onSelect])

  return (
    <aside className="w-80 flex flex-col border-r border-border-dark bg-surface-dark z-10 shrink-0">
      <div className="p-4 border-b border-border-dark">
        <div className="flex flex-col gap-1 mb-3">
          <h1 className="text-sm font-bold tracking-widest text-primary uppercase">Fleet Inventory</h1>
          <p className="text-slate-400 text-xs">{vehicles.length} Units</p>
        </div>
        <div className="flex items-center gap-2 bg-[#1c2527] rounded-lg p-2">
          <Search className="text-slate-400" size={16} />
          <input className="bg-transparent outline-none text-sm flex-1 placeholder:text-[#9db4b9]" placeholder="Search VIN or Driver" value={inputValue} onChange={(e)=>setInputValue(e.target.value)} />
        </div>
        {/* Status filter buttons */}
        <div className="mt-3">
          <div className="flex flex-wrap gap-2 items-center">
            {/* compute counts once for performance */}
            {(() => {
              const counts: Record<string, number> = {}
              for (const v of vehicles) counts[v.status] = (counts[v.status] || 0) + 1
              const groups: [VehicleStatus, string][] = [['all', 'All'], ...STATUS_PRIORITY.map((s): [VehicleStatus, string] => [s as VehicleStatus, `${s.charAt(0).toUpperCase()}${s.slice(1)}`])]
              return groups.map(([key, label]) => {
                const count = key === 'all' ? vehicles.length : (counts[key] || 0)
                const isSel = selectedStatuses.includes(key)
                const statusDot = key === 'all' ? '' : (key === 'active' ? 'bg-[#0bda54]' : key === 'warning' ? 'bg-[#f59e0b]' : key === 'danger' ? 'bg-[#ef4444]' : 'bg-[#9ca3af]')
                return (
                  <label key={key} className={`flex items-center gap-2 text-sm px-3 py-1 rounded-md transition-colors ${isSel ? 'bg-white/10 text-white font-semibold' : 'text-slate-300 hover:bg-white/5'}`}>
                    <input
                      type="checkbox"
                      checked={isSel}
                      onChange={() => toggleStatus(key)}
                      className="accent-primary"
                    />
                    {key !== 'all' && <span className={`w-2.5 h-2.5 rounded-full ${statusDot}`} />}
                    <span className="select-none">{label}</span>
                    <span className={`ml-1 text-xs inline-flex items-center justify-center px-2 py-0.5 rounded ${isSel ? 'bg-white/20 text-white' : 'bg-[#0b1112] text-slate-300'}`}>{count}</span>
                  </label>
                )
              })
            })()}
          </div>
          <label className="flex items-center gap-2 text-sm mt-2 px-3 py-1 rounded-md text-slate-300 hover:bg-white/5 w-fit cursor-pointer select-none">
            <input
              type="checkbox"
              checked={hideInactive}
              onChange={() => toggleHideInactive()}
              className="accent-primary"
            />
            Hide Inactive
          </label>
        </div>
      </div>
      <div ref={parentRef} tabIndex={0} onKeyDown={handleKeyDown} className="flex-1 hide-scrollbar" style={{ overflowY: 'auto' }}>
        <div style={{ height: `${virtualizer.getTotalSize()}px`, position: 'relative' }}>
          {virtualItems.map((virtualRow: { index: number; start: any }) => {
            const v = filtered[virtualRow.index]
            const isSelected = selectedId === v.id
            const isFocused = focusedIdx === virtualRow.index
            return (
              <div key={v.id} onClick={() => onSelect(v)} style={{ position: 'absolute', top: 0, left: 0, width: '100%', transform: `translateY(${virtualRow.start}px)` }} className={`vt-enter group flex items-center justify-between p-3 border-b border-border-dark hover:bg-white/5 cursor-pointer transition-all ${isSelected? 'bg-white/5 border-l-primary border-l-2':'border-l-transparent'} ${isFocused? 'bg-white/3': ''} ${v.inactive ? 'opacity-50' : ''}`}>
                <div className="flex items-center gap-3">
                  <div className="relative">
                    <div className={`${statusColor(v.status)} w-2.5 h-2.5 rounded-full`} />
                  </div>
                  <div className="flex flex-col">
                    <span className="text-sm font-bold text-white flex items-center gap-2">
                      {highlight(v.id, query)}
                      {v.inactive && (
                        <span className="text-[10px] font-bold tracking-wide text-slate-400 bg-white/10 px-1.5 py-0.5 rounded">INACTIVE</span>
                      )}
                    </span>
                    <span className="text-xs text-slate-400">{highlight(`${v.model} • ${v.driver}`, query)}</span>
                    {query && v.lastSeenAtUtc && (
                      <span className="text-[10px] text-slate-500">Last seen {formatLastSeen(v.lastSeenAtUtc)}</span>
                    )}
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  <span className="text-xs font-mono text-primary">{v.fuel}%</span>
                </div>
              </div>
            )
          })}
        </div>
      </div>
    </aside>
  )
}

export default React.memo(Sidebar)
