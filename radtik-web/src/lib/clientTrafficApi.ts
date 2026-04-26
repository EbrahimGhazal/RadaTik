import { mvcBaseUrl } from './mvcBaseUrl'

export interface ClientLiveTraffic {
  connected: boolean
  utcIso: string
  userName: string
  clientName: string
  serverName: string
  address?: string | null
  macAddress?: string | null
  uptime?: string | null
  rxBytes: number
  txBytes: number
  rxPackets: number
  txPackets: number
  rxBps: number
  txBps: number
}

export interface ClientTrafficTestStatus {
  testActive: boolean
  canStartTest: boolean
  activeUntilUtcIso?: string | null
  nextEligibleUtcIso?: string | null
  durationSeconds: number
  cooldownHours: number
  chargeAmount: number
  currentBalance: number
  secondsRemaining: number
}

export async function fetchClientLiveTraffic(): Promise<ClientLiveTraffic> {
  const r = await fetch(`${mvcBaseUrl()}/api/client/traffic/live`, {
    credentials: 'include',
    headers: { Accept: 'application/json' },
  })
  if (r.status === 401 || r.status === 403) {
    throw new Error('forbidden')
  }
  if (!r.ok) {
    throw new Error('loadFailed')
  }
  return r.json() as Promise<ClientLiveTraffic>
}

export async function fetchClientTrafficTestStatus(): Promise<ClientTrafficTestStatus> {
  const r = await fetch(`${mvcBaseUrl()}/api/client/traffic/test-status`, {
    credentials: 'include',
    headers: { Accept: 'application/json' },
  })
  if (r.status === 401 || r.status === 403) throw new Error('forbidden')
  if (!r.ok) throw new Error('loadFailed')
  return r.json() as Promise<ClientTrafficTestStatus>
}

export async function startClientTrafficTest(): Promise<ClientTrafficTestStatus> {
  const r = await fetch(`${mvcBaseUrl()}/api/client/traffic/start-test`, {
    method: 'POST',
    credentials: 'include',
    headers: { Accept: 'application/json' },
  })
  if (r.status === 401 || r.status === 403) throw new Error('forbidden')
  if (r.status === 402) throw new Error('insufficientBalance')
  if (r.status === 409) throw new Error('cooldown')
  if (!r.ok) throw new Error('startFailed')
  return r.json() as Promise<ClientTrafficTestStatus>
}
