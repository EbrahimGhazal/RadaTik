import { useEffect, useState } from 'react'
import { fetchDb } from '../lib/api'
import { fetchQuery } from '../lib/queryClient'

export interface MockDbQueryState {
  data: Record<string, unknown> | null
  loading: boolean
  error: string | null
}

export function useMockDbQuery(): MockDbQueryState {
  const [state, setState] = useState<MockDbQueryState>({
    data: null,
    loading: true,
    error: null,
  })

  useEffect(() => {
    let cancelled = false

    void (async () => {
      try {
        const data = await fetchQuery('mock-db:full', fetchDb, 10_000)
        if (!cancelled) {
          setState({ data, loading: false, error: null })
        }
      } catch (error) {
        if (!cancelled) {
          const message = error instanceof Error ? error.message : 'Unknown error'
          setState({ data: null, loading: false, error: message })
        }
      }
    })()

    return () => {
      cancelled = true
    }
  }, [])

  return state
}
