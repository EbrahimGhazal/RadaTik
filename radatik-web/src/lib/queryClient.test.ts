import { describe, expect, it, vi } from 'vitest'
import { fetchQuery, invalidateQuery } from './queryClient'

describe('queryClient', () => {
  it('deduplicates in-flight requests for same key', async () => {
    invalidateQuery('k')
    const fetcher = vi.fn(async () => {
      await new Promise((resolve) => setTimeout(resolve, 10))
      return 42
    })

    const [a, b] = await Promise.all([fetchQuery('k', fetcher), fetchQuery('k', fetcher)])
    expect(a).toBe(42)
    expect(b).toBe(42)
    expect(fetcher).toHaveBeenCalledTimes(1)
  })
})
