import { useEffect, useState } from 'react'

/** Slightly varies a numeric stat to simulate live telemetry. */
export function useLiveStat(base: number, intervalMs = 4000) {
  const [value, setValue] = useState(base)

  useEffect(() => {
    setValue(base)
  }, [base])

  useEffect(() => {
    const id = window.setInterval(() => {
      setValue((v) => {
        const delta = Math.round((Math.random() - 0.5) * (base * 0.002))
        return Math.max(0, v + delta)
      })
    }, intervalMs)
    return () => window.clearInterval(id)
  }, [base, intervalMs])

  return value
}
