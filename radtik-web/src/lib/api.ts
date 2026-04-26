import { MOCK_DB_KEYS, type MockDbKey } from './mockDbKeys'

let staticDbPromise: Promise<Record<string, unknown>> | null = null
let apiAvailable: boolean | null = null

function mockDbUrl(): string {
  const base = import.meta.env.BASE_URL || '/'
  return `${base}mock/db.json`.replace(/\/{2,}/g, '/')
}

function loadStaticDb(): Promise<Record<string, unknown>> {
  staticDbPromise ??= (async () => {
    const r = await fetch(mockDbUrl())
    if (!r.ok) throw new Error('Failed to load mock database')
    return r.json() as Promise<Record<string, unknown>>
  })()
  return staticDbPromise
}

/** Detect json-server behind Vite `/api` proxy (see `npm run dev:mock`). */
async function isApiLive(): Promise<boolean> {
  if (apiAvailable !== null) return apiAvailable
  try {
    const ctrl = new AbortController()
    const timer = window.setTimeout(() => ctrl.abort(), 1500)
    const r = await fetch('/api/stats', { signal: ctrl.signal })
    window.clearTimeout(timer)
    apiAvailable = r.ok
  } catch {
    apiAvailable = false
  }
  return apiAvailable
}

/** Full document: parallel `/api/:key` when mock API is up, otherwise single `/mock/db.json` fetch. */
export async function fetchDb(): Promise<Record<string, unknown>> {
  if (await isApiLive()) {
    try {
      const pairs = await Promise.all(
        MOCK_DB_KEYS.map(async (key) => {
          const r = await fetch(`/api/${key}`)
          if (!r.ok) throw new Error(`api ${key}`)
          return [key, await r.json()] as const
        }),
      )
      return Object.fromEntries(pairs)
    } catch {
      apiAvailable = false
    }
  }
  return loadStaticDb()
}

/** Single resource: tries `/api/:key` first, then static slice (invalidates cache on API failure after success — keep simple: static from loadStaticDb). */
export async function fetchResource<T>(key: MockDbKey | string): Promise<T> {
  if (await isApiLive()) {
    try {
      const r = await fetch(`/api/${encodeURIComponent(key)}`)
      if (r.ok) return (await r.json()) as T
    } catch {
      apiAvailable = false
    }
  }
  const db = await loadStaticDb()
  const v = db[key]
  if (v === undefined) throw new Error(`Unknown mock key: ${key}`)
  return v as T
}

/** Call after HMR or when switching servers (optional). */
export function resetApiDetection() {
  apiAvailable = null
  staticDbPromise = null
}
