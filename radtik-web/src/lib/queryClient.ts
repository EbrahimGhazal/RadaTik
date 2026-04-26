type QueryFetcher<T> = () => Promise<T>

interface QueryEntry<T> {
  value?: T
  expiresAt: number
  inFlight?: Promise<T>
}

const cache = new Map<string, QueryEntry<unknown>>()

export async function fetchQuery<T>(key: string, fetcher: QueryFetcher<T>, staleMs = 15_000): Promise<T> {
  const now = Date.now()
  const existing = cache.get(key) as QueryEntry<T> | undefined

  if (existing?.value !== undefined && existing.expiresAt > now) {
    return existing.value
  }

  if (existing?.inFlight) {
    return existing.inFlight
  }

  const inFlight = fetcher().then((value) => {
    cache.set(key, { value, expiresAt: Date.now() + staleMs })
    return value
  })

  cache.set(key, { value: existing?.value, expiresAt: existing?.expiresAt ?? 0, inFlight })

  try {
    return await inFlight
  } finally {
    const settled = cache.get(key) as QueryEntry<T> | undefined
    if (settled) {
      delete settled.inFlight
      cache.set(key, settled)
    }
  }
}

export function invalidateQuery(key: string): void {
  cache.delete(key)
}
