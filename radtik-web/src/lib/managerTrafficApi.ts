import { mvcBaseUrl } from './mvcBaseUrl'

export interface ManagerMikrotikServerOption {
  id: number
  name: string
  host: string
  networkId: number
}

export interface InterfaceTrafficLine {
  name: string
  type: string
  running: boolean
  isBridge: boolean
  memberOfBridge?: string | null
  rxBytes: number
  txBytes: number
  rxPackets: number
  txPackets: number
  rxBps: number
  txBps: number
}

export interface TrafficSnapshotPayload {
  networkId: number
  serverId: number
  serverName: string
  utcIso: string
  interfaces: InterfaceTrafficLine[]
}

export interface TrafficPeriodStatistics {
  periodKey: 'day' | 'week' | 'month' | string
  fromUtcIso: string
  toUtcIso: string
  samples: number
  rxMinBps: number | null
  rxAvgBps: number | null
  rxMaxBps: number | null
  txMinBps: number | null
  txAvgBps: number | null
  txMaxBps: number | null
}

export interface TrafficStatisticsOverview {
  serverId: number
  serverName: string
  generatedAtUtcIso: string
  periods: TrafficPeriodStatistics[]
}

export interface TrafficTrendPoint {
  bucketUtcIso: string
  rxAvgBps: number
  txAvgBps: number
}

export interface TrafficTrendResponse {
  serverId: number
  periodKey: 'day' | 'week' | 'month' | string
  generatedAtUtcIso: string
  points: TrafficTrendPoint[]
}

export interface TrafficKpiThresholds {
  peakRxWarnBps: number
  peakRxCriticalBps: number
  peakTxWarnBps: number
  peakTxCriticalBps: number
  loadIndexWarnPercent: number
  loadIndexCriticalPercent: number
}

export async function fetchManagerMikrotikServers(): Promise<ManagerMikrotikServerOption[]> {
  const r = await fetch(`${mvcBaseUrl()}/api/manager/traffic/mikrotik-servers`, {
    credentials: 'include',
    headers: { Accept: 'application/json' },
  })
  if (r.status === 401 || r.status === 403) {
    throw new Error('forbidden')
  }
  if (!r.ok) {
    throw new Error('loadFailed')
  }
  return r.json() as Promise<ManagerMikrotikServerOption[]>
}

export async function fetchManagerMikrotikServerStats(serverId: number): Promise<TrafficStatisticsOverview> {
  const r = await fetch(`${mvcBaseUrl()}/api/manager/traffic/server-stats?serverId=${serverId}`, {
    credentials: 'include',
    headers: { Accept: 'application/json' },
  })
  if (r.status === 401 || r.status === 403) {
    throw new Error('forbidden')
  }
  if (!r.ok) {
    throw new Error('loadFailed')
  }
  return r.json() as Promise<TrafficStatisticsOverview>
}

export async function fetchManagerMikrotikServerTrend(
  serverId: number,
  period: 'day' | 'week' | 'month',
): Promise<TrafficTrendResponse> {
  const r = await fetch(`${mvcBaseUrl()}/api/manager/traffic/server-trend?serverId=${serverId}&period=${period}`, {
    credentials: 'include',
    headers: { Accept: 'application/json' },
  })
  if (r.status === 401 || r.status === 403) {
    throw new Error('forbidden')
  }
  if (!r.ok) {
    throw new Error('loadFailed')
  }
  return r.json() as Promise<TrafficTrendResponse>
}

export async function fetchManagerMikrotikKpiThresholds(): Promise<TrafficKpiThresholds> {
  const r = await fetch(`${mvcBaseUrl()}/api/manager/traffic/kpi-thresholds`, {
    credentials: 'include',
    headers: { Accept: 'application/json' },
  })
  if (r.status === 401 || r.status === 403) {
    throw new Error('forbidden')
  }
  if (!r.ok) {
    throw new Error('loadFailed')
  }
  return r.json() as Promise<TrafficKpiThresholds>
}

export function formatBps(bps: number): string {
  if (!Number.isFinite(bps) || bps < 0) return '—'
  if (bps < 1000) return `${Math.round(bps)} bps`
  if (bps < 1e6) return `${(bps / 1e3).toFixed(1)} Kbps`
  return `${(bps / 1e6).toFixed(2)} Mbps`
}

export function formatBytes(n: number): string {
  if (!Number.isFinite(n) || n < 0) return '—'
  if (n < 1024) return `${n} B`
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(1)} KB`
  if (n < 1024 * 1024 * 1024) return `${(n / (1024 * 1024)).toFixed(2)} MB`
  return `${(n / (1024 * 1024 * 1024)).toFixed(2)} GB`
}

export function formatPacketCount(n: number): string {
  if (!Number.isFinite(n) || n < 0) return '—'
  return n.toLocaleString()
}
