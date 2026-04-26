import { useEffect, useRef, useState } from 'react'
import { fetchResource } from '../lib/api'
import { fetchQuery } from '../lib/queryClient'

export interface ResourceQueryState<T> {
  data: T | null
  loading: boolean
  error: string | null
}

export function useMockResourceQuery<T>(resourceKey: string): ResourceQueryState<T> {
  const [state, setState] = useState<ResourceQueryState<T>>({
    data: null,
    loading: true,
    error: null,
  })

  useEffect(() => {
    let cancelled = false

    void (async () => {
      try {
        const data = await fetchQuery(
          `mock-resource:${resourceKey}`,
          () => fetchResource<T>(resourceKey),
          10_000,
        )
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
  }, [resourceKey])

  return state
}

/** Parallel resources with one cache key; `fetcher` may be recreated each render — latest is used via ref. */
export function useMockResourceBundleQuery<T>(cacheKey: string, fetcher: () => Promise<T>): ResourceQueryState<T> {
  const [state, setState] = useState<ResourceQueryState<T>>({
    data: null,
    loading: true,
    error: null,
  })
  const fetcherRef = useRef(fetcher)

  /* Bundle keyed by cacheKey; inline fetcher changes each render — ref synced at effect start. */
  /* eslint-disable react-hooks/exhaustive-deps */
  useEffect(() => {
    fetcherRef.current = fetcher
    let cancelled = false

    void (async () => {
      try {
        const data = await fetchQuery(`mock-bundle:${cacheKey}`, () => fetcherRef.current(), 10_000)
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
  }, [cacheKey])
  /* eslint-enable react-hooks/exhaustive-deps */

  return state
}
