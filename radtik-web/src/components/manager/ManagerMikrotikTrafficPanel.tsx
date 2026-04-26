import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import * as signalR from '@microsoft/signalr'
import { Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { Card } from '../ui/Card'
import { cn } from '../../lib/cn'
import { mvcBaseUrl } from '../../lib/mvcBaseUrl'
import {
  fetchManagerMikrotikKpiThresholds,
  fetchManagerMikrotikServerStats,
  fetchManagerMikrotikServerTrend,
  fetchManagerMikrotikServers,
  formatBps,
  formatBytes,
  formatPacketCount,
  type ManagerMikrotikServerOption,
  type TrafficPeriodStatistics,
  type TrafficStatisticsOverview,
  type TrafficSnapshotPayload,
  type TrafficKpiThresholds,
  type TrafficTrendResponse,
} from '../../lib/managerTrafficApi'

export function ManagerMikrotikTrafficPanel({
  compact,
  title,
}: {
  /** Slightly tighter layout when embedded on the dashboard tab. */
  compact?: boolean
  /** Optional heading override (full page supplies its own Card title). */
  title?: string
}) {
  const { t } = useTranslation()
  const [servers, setServers] = useState<ManagerMikrotikServerOption[]>([])
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [serversLoading, setServersLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [hubError, setHubError] = useState<string | null>(null)
  const [snapshot, setSnapshot] = useState<TrafficSnapshotPayload | null>(null)
  const [stats, setStats] = useState<TrafficStatisticsOverview | null>(null)
  const [statsLoading, setStatsLoading] = useState(false)
  const [statsError, setStatsError] = useState<string | null>(null)
  const [trend, setTrend] = useState<TrafficTrendResponse | null>(null)
  const [trendLoading, setTrendLoading] = useState(false)
  const [trendError, setTrendError] = useState<string | null>(null)
  const [trendPeriod, setTrendPeriod] = useState<'day' | 'week' | 'month'>('day')
  const [kpiThresholds, setKpiThresholds] = useState<TrafficKpiThresholds>({
    peakRxWarnBps: 150_000_000,
    peakRxCriticalBps: 300_000_000,
    peakTxWarnBps: 100_000_000,
    peakTxCriticalBps: 200_000_000,
    loadIndexWarnPercent: 70,
    loadIndexCriticalPercent: 85,
  })

  const selected = servers.find((s) => s.id === selectedId) ?? null

  useEffect(() => {
    let alive = true
    setServersLoading(true)
    void (async () => {
      try {
        const list = await fetchManagerMikrotikServers()
        if (!alive) return
        setServers(list)
        setSelectedId((prev) => (prev === null && list.length > 0 ? list[0].id : prev))
        setLoadError(null)
      } catch {
        if (!alive) return
        setLoadError(t('manager.traffic.loadError'))
      } finally {
        if (alive) setServersLoading(false)
      }
    })()
    return () => {
      alive = false
    }
  }, [t])

  useEffect(() => {
    let alive = true
    void (async () => {
      try {
        const payload = await fetchManagerMikrotikKpiThresholds()
        if (!alive) return
        setKpiThresholds(payload)
      } catch {
        /* keep defaults */
      }
    })()
    return () => {
      alive = false
    }
  }, [])

  useEffect(() => {
    if (!selected) {
      return
    }

    let cancelled = false
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${mvcBaseUrl()}/hubs/traffic`, {
        withCredentials: true,
        transport:
          signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .build()

    connection.on('trafficUpdate', (payload: TrafficSnapshotPayload) => {
      if (!cancelled) {
        setSnapshot(payload)
        setHubError(null)
      }
    })

    connection.on('trafficError', (err: { message?: string }) => {
      if (!cancelled) {
        setHubError(err?.message ?? t('manager.traffic.hubError'))
      }
    })

    void (async () => {
      try {
        await connection.start()
        if (cancelled) {
          await connection.stop()
          return
        }
        await connection.invoke('JoinTraffic', selected.networkId, selected.id)
      } catch {
        if (!cancelled) {
          setHubError(t('manager.traffic.connectError'))
        }
      }
    })()

    return () => {
      cancelled = true
      void (async () => {
        try {
          await connection.invoke('LeaveTraffic', selected.networkId, selected.id)
        } catch {
          /* ignore */
        }
        await connection.stop()
      })()
    }
  }, [selected, t])

  useEffect(() => {
    if (!selected) {
      setTrend(null)
      setTrendError(null)
      setTrendLoading(false)
      return
    }

    let alive = true
    let timer: ReturnType<typeof setInterval> | null = null

    const loadTrend = async () => {
      try {
        if (alive) setTrendLoading(true)
        const payload = await fetchManagerMikrotikServerTrend(selected.id, trendPeriod)
        if (!alive) return
        setTrend(payload)
        setTrendError(null)
      } catch {
        if (!alive) return
        setTrendError(t('manager.traffic.trendLoadError'))
      } finally {
        if (alive) setTrendLoading(false)
      }
    }

    void loadTrend()
    timer = setInterval(() => {
      void loadTrend()
    }, 60_000)

    return () => {
      alive = false
      if (timer) clearInterval(timer)
    }
  }, [selected, trendPeriod, t])

  useEffect(() => {
    if (!selected) {
      setStats(null)
      setStatsError(null)
      setStatsLoading(false)
      return
    }

    let alive = true
    let timer: ReturnType<typeof setInterval> | null = null

    const loadStats = async () => {
      try {
        if (alive) setStatsLoading(true)
        const payload = await fetchManagerMikrotikServerStats(selected.id)
        if (!alive) return
        setStats(payload)
        setStatsError(null)
      } catch {
        if (!alive) return
        setStatsError(t('manager.traffic.statsLoadError'))
      } finally {
        if (alive) setStatsLoading(false)
      }
    }

    void loadStats()
    timer = setInterval(() => {
      void loadStats()
    }, 60_000)

    return () => {
      alive = false
      if (timer) clearInterval(timer)
    }
  }, [selected, t])

  const periodLabel = (key: string): string => {
    if (key === 'day') return t('manager.traffic.periodDay')
    if (key === 'week') return t('manager.traffic.periodWeek')
    if (key === 'month') return t('manager.traffic.periodMonth')
    return key
  }

  const statValue = (n: number | null): string => {
    if (typeof n !== 'number' || !Number.isFinite(n)) return '—'
    return formatBps(n)
  }

  const trendPoints = trend?.points ?? []
  const peakRxPoint =
    trendPoints.length > 0
      ? trendPoints.reduce((best, point) => (point.rxAvgBps > best.rxAvgBps ? point : best), trendPoints[0])
      : null
  const peakTxPoint =
    trendPoints.length > 0
      ? trendPoints.reduce((best, point) => (point.txAvgBps > best.txAvgBps ? point : best), trendPoints[0])
      : null

  const loadIndexPercent = (() => {
    if (trendPoints.length === 0) return null
    const totals = trendPoints.map((p) => p.rxAvgBps + p.txAvgBps)
    const max = Math.max(...totals)
    if (!Number.isFinite(max) || max <= 0) return null
    const avg = totals.reduce((acc, v) => acc + v, 0) / totals.length
    return Math.round((avg / max) * 100)
  })()

  const kpiTimeLabel = (utcIso: string | null): string => {
    if (!utcIso) return '—'
    const d = new Date(utcIso)
    return trendPeriod === 'day'
      ? d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
      : d.toLocaleDateString()
  }

  type RiskLevel = 'normal' | 'warn' | 'critical'
  const riskLevelByBps = (value: number | null, warn: number, critical: number): RiskLevel => {
    if (value === null || !Number.isFinite(value)) return 'normal'
    if (value >= critical) return 'critical'
    if (value >= warn) return 'warn'
    return 'normal'
  }
  const riskLevelByPercent = (value: number | null, warn: number, critical: number): RiskLevel => {
    if (value === null || !Number.isFinite(value)) return 'normal'
    if (value >= critical) return 'critical'
    if (value >= warn) return 'warn'
    return 'normal'
  }
  const riskClass = (level: RiskLevel): string => {
    if (level === 'critical') return 'border-rose-500/40 bg-rose-500/10'
    if (level === 'warn') return 'border-amber-500/40 bg-amber-500/10'
    return 'border-emerald-500/30 bg-emerald-500/10'
  }
  const riskLabel = (level: RiskLevel): string => {
    if (level === 'critical') return t('manager.traffic.riskHigh')
    if (level === 'warn') return t('manager.traffic.riskMedium')
    return t('manager.traffic.riskNormal')
  }

  const rxRisk = riskLevelByBps(
    peakRxPoint?.rxAvgBps ?? null,
    kpiThresholds.peakRxWarnBps,
    kpiThresholds.peakRxCriticalBps,
  )
  const txRisk = riskLevelByBps(
    peakTxPoint?.txAvgBps ?? null,
    kpiThresholds.peakTxWarnBps,
    kpiThresholds.peakTxCriticalBps,
  )
  const loadRisk = riskLevelByPercent(
    loadIndexPercent,
    kpiThresholds.loadIndexWarnPercent,
    kpiThresholds.loadIndexCriticalPercent,
  )

  const riskScore = (level: RiskLevel): number => {
    if (level === 'critical') return 2
    if (level === 'warn') return 1
    return 0
  }

  const overallRisk: RiskLevel = (() => {
    const score = Math.max(riskScore(rxRisk), riskScore(txRisk), riskScore(loadRisk))
    if (score >= 2) return 'critical'
    if (score >= 1) return 'warn'
    return 'normal'
  })()

  const recommendations: string[] = []
  if (rxRisk === 'critical') recommendations.push(t('manager.traffic.recPeakRxHigh'))
  else if (rxRisk === 'warn') recommendations.push(t('manager.traffic.recPeakRxWarn'))

  if (txRisk === 'critical') recommendations.push(t('manager.traffic.recPeakTxHigh'))
  else if (txRisk === 'warn') recommendations.push(t('manager.traffic.recPeakTxWarn'))

  if (loadRisk === 'critical') recommendations.push(t('manager.traffic.recLoadHigh'))
  else if (loadRisk === 'warn') recommendations.push(t('manager.traffic.recLoadWarn'))

  if (recommendations.length === 0) {
    recommendations.push(t('manager.traffic.recHealthy'))
  }

  const kpiCards = (
    <div className="grid gap-3 md:grid-cols-3">
      <div className={cn('rounded-xl border p-3', riskClass(rxRisk))}>
        <div className="flex items-center justify-between gap-2">
          <p className="text-xs font-medium text-rt-neutral-mid">{t('manager.traffic.kpiPeakRx')}</p>
          <span className="rounded-full bg-white/70 px-2 py-0.5 text-[11px] font-medium text-rt-neutral-mid dark:bg-black/20">
            {riskLabel(rxRisk)}
          </span>
        </div>
        <p className="mt-1 text-base font-semibold text-rt-neutral-text">
          {peakRxPoint ? formatBps(peakRxPoint.rxAvgBps) : '—'}
        </p>
        <p className="mt-1 text-xs text-rt-neutral-mid">
          {t('manager.traffic.kpiAt')}: {kpiTimeLabel(peakRxPoint?.bucketUtcIso ?? null)}
        </p>
      </div>
      <div className={cn('rounded-xl border p-3', riskClass(txRisk))}>
        <div className="flex items-center justify-between gap-2">
          <p className="text-xs font-medium text-rt-neutral-mid">{t('manager.traffic.kpiPeakTx')}</p>
          <span className="rounded-full bg-white/70 px-2 py-0.5 text-[11px] font-medium text-rt-neutral-mid dark:bg-black/20">
            {riskLabel(txRisk)}
          </span>
        </div>
        <p className="mt-1 text-base font-semibold text-rt-neutral-text">
          {peakTxPoint ? formatBps(peakTxPoint.txAvgBps) : '—'}
        </p>
        <p className="mt-1 text-xs text-rt-neutral-mid">
          {t('manager.traffic.kpiAt')}: {kpiTimeLabel(peakTxPoint?.bucketUtcIso ?? null)}
        </p>
      </div>
      <div className={cn('rounded-xl border p-3', riskClass(loadRisk))}>
        <div className="flex items-center justify-between gap-2">
          <p className="text-xs font-medium text-rt-neutral-mid">{t('manager.traffic.kpiLoadIndex')}</p>
          <span className="rounded-full bg-white/70 px-2 py-0.5 text-[11px] font-medium text-rt-neutral-mid dark:bg-black/20">
            {riskLabel(loadRisk)}
          </span>
        </div>
        <p className="mt-1 text-base font-semibold text-rt-neutral-text">
          {loadIndexPercent !== null ? `${loadIndexPercent}%` : '—'}
        </p>
        <p className="mt-1 text-xs text-rt-neutral-mid">{periodLabel(trendPeriod)}</p>
      </div>
    </div>
  )

  const statsCards = (
    <div className="space-y-2">
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-rt-neutral-text">{t('manager.traffic.statsTitle')}</h3>
        {stats?.generatedAtUtcIso ? (
          <span className="text-xs text-rt-neutral-mid">
            {t('manager.traffic.lastUpdate')}: {new Date(stats.generatedAtUtcIso).toLocaleString()}
          </span>
        ) : null}
      </div>
      {statsError ? (
        <p className="rounded-lg border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-sm text-amber-800 dark:text-amber-200">
          {statsError}
        </p>
      ) : null}
      {statsLoading && !stats ? <p className="text-sm text-rt-neutral-mid">{t('common.loading')}</p> : null}
      {!statsLoading && stats && stats.periods.length > 0 ? (
        <div className="grid gap-3 md:grid-cols-3">
          {stats.periods.map((period: TrafficPeriodStatistics) => (
            <div key={period.periodKey} className="rounded-xl border border-rt-border bg-rt-page/70 p-3">
              <div className="mb-2 flex items-center justify-between">
                <span className="text-sm font-semibold text-rt-neutral-text">{periodLabel(period.periodKey)}</span>
                <span className="text-xs text-rt-neutral-mid">
                  {t('manager.traffic.samples')}: {period.samples}
                </span>
              </div>
              <div className="space-y-1 text-xs">
                <p className="font-medium text-rt-neutral-text">{t('manager.traffic.rx')}</p>
                <p className="text-rt-neutral-mid">
                  {t('manager.traffic.min')}: <span className="font-semibold text-rt-neutral-text">{statValue(period.rxMinBps)}</span>
                </p>
                <p className="text-rt-neutral-mid">
                  {t('manager.traffic.avg')}: <span className="font-semibold text-rt-neutral-text">{statValue(period.rxAvgBps)}</span>
                </p>
                <p className="text-rt-neutral-mid">
                  {t('manager.traffic.max')}: <span className="font-semibold text-rt-neutral-text">{statValue(period.rxMaxBps)}</span>
                </p>
                <p className="pt-2 font-medium text-rt-neutral-text">{t('manager.traffic.tx')}</p>
                <p className="text-rt-neutral-mid">
                  {t('manager.traffic.min')}: <span className="font-semibold text-rt-neutral-text">{statValue(period.txMinBps)}</span>
                </p>
                <p className="text-rt-neutral-mid">
                  {t('manager.traffic.avg')}: <span className="font-semibold text-rt-neutral-text">{statValue(period.txAvgBps)}</span>
                </p>
                <p className="text-rt-neutral-mid">
                  {t('manager.traffic.max')}: <span className="font-semibold text-rt-neutral-text">{statValue(period.txMaxBps)}</span>
                </p>
              </div>
            </div>
          ))}
        </div>
      ) : null}
    </div>
  )

  const trendChart = (
    <div className="space-y-2 rounded-xl border border-rt-border bg-rt-page/70 p-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-sm font-semibold text-rt-neutral-text">{t('manager.traffic.trendTitle')}</h3>
        <div className="flex items-center gap-1 rounded-lg border border-rt-border bg-rt-surface p-1">
          {(['day', 'week', 'month'] as const).map((p) => (
            <button
              key={p}
              type="button"
              onClick={() => setTrendPeriod(p)}
              className={cn(
                'rounded-md px-2 py-1 text-xs font-medium',
                trendPeriod === p ? 'bg-rt-primary text-white' : 'text-rt-neutral-mid hover:bg-rt-page',
              )}
            >
              {periodLabel(p)}
            </button>
          ))}
        </div>
      </div>
      <div
        className={cn(
          'rounded-lg border px-3 py-2 text-sm',
          overallRisk === 'critical'
            ? 'border-rose-500/40 bg-rose-500/10 text-rose-800 dark:text-rose-200'
            : overallRisk === 'warn'
              ? 'border-amber-500/40 bg-amber-500/10 text-amber-800 dark:text-amber-200'
              : 'border-emerald-500/40 bg-emerald-500/10 text-emerald-800 dark:text-emerald-200',
        )}
      >
        <span className="font-semibold">{t('manager.traffic.overallStatus')}:</span> {riskLabel(overallRisk)}
      </div>
      {trendError ? (
        <p className="rounded-lg border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-sm text-amber-800 dark:text-amber-200">
          {trendError}
        </p>
      ) : null}
      {kpiCards}
      <div className="rounded-xl border border-rt-border bg-rt-surface/80 p-3">
        <p className="mb-2 text-sm font-semibold text-rt-neutral-text">{t('manager.traffic.recommendationsTitle')}</p>
        <ul className="space-y-1 text-sm text-rt-neutral-mid">
          {recommendations.map((rec, idx) => (
            <li key={`${idx}-${rec}`} className="flex items-start gap-2">
              <span className="mt-1 inline-block h-1.5 w-1.5 rounded-full bg-rt-primary" />
              <span>{rec}</span>
            </li>
          ))}
        </ul>
      </div>
      {trendLoading && !trend ? <p className="text-sm text-rt-neutral-mid">{t('common.loading')}</p> : null}
      {trend && trend.points.length > 0 ? (
        <div className="h-56">
          <ResponsiveContainer width="100%" height="100%">
            <LineChart
              data={trend.points.map((p) => ({
                label:
                  trend.periodKey === 'day'
                    ? new Date(p.bucketUtcIso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
                    : new Date(p.bucketUtcIso).toLocaleDateString(),
                rx: p.rxAvgBps,
                tx: p.txAvgBps,
              }))}
            >
              <XAxis dataKey="label" tick={{ fontSize: 11, fill: 'var(--color-rt-neutral-mid)' }} minTickGap={24} />
              <YAxis tick={{ fontSize: 11, fill: 'var(--color-rt-neutral-mid)' }} tickFormatter={(v) => formatBps(Number(v))} width={84} />
              <Tooltip
                contentStyle={{
                  background: 'var(--color-rt-surface)',
                  border: '1px solid var(--color-rt-border)',
                }}
                formatter={(value) =>
                  formatBps(typeof value === 'number' ? value : Number(value ?? 0))
                }
              />
              <Line type="monotone" dataKey="rx" stroke="#0ea5e9" strokeWidth={2} dot={false} name={t('manager.traffic.rx')} />
              <Line type="monotone" dataKey="tx" stroke="#22c55e" strokeWidth={2} dot={false} name={t('manager.traffic.tx')} />
            </LineChart>
          </ResponsiveContainer>
        </div>
      ) : !trendLoading ? (
        <p className="text-sm text-rt-neutral-mid">{t('common.noData')}</p>
      ) : null}
    </div>
  )

  const table = (
    <div
      className={cn(
        'overflow-x-auto',
        compact ? 'max-h-[min(28rem,55vh)] overflow-y-auto' : 'max-h-[min(32rem,60vh)] overflow-y-auto',
      )}
    >
      <table className="w-full min-w-[52rem] border-collapse text-left text-sm">
        <thead className="sticky top-0 z-10 bg-rt-surface">
          <tr className="border-b border-rt-border text-rt-neutral-mid">
            <th className="py-2 pe-3 font-medium">{t('manager.traffic.colInterface')}</th>
            <th className="py-2 pe-3 font-medium">{t('manager.traffic.colType')}</th>
            <th className="py-2 pe-3 font-medium">{t('manager.traffic.colStatus')}</th>
            <th className="py-2 pe-3 font-medium">{t('manager.traffic.colBridge')}</th>
            <th className="py-2 pe-3 font-medium tabular-nums">{t('manager.traffic.colRx')}</th>
            <th className="py-2 pe-3 font-medium tabular-nums">{t('manager.traffic.colTx')}</th>
            <th className="py-2 pe-3 font-medium tabular-nums">{t('manager.traffic.colRxPacket')}</th>
            <th className="py-2 pe-3 font-medium tabular-nums">{t('manager.traffic.colTxPacket')}</th>
            <th className="py-2 pe-3 font-medium tabular-nums">{t('manager.traffic.colRxRate')}</th>
            <th className="py-2 font-medium tabular-nums">{t('manager.traffic.colTxRate')}</th>
          </tr>
        </thead>
        <tbody>
          {(snapshot?.interfaces ?? []).map((row) => (
            <tr key={row.name} className="border-b border-rt-border/80">
              <td className="py-2 pe-3 font-medium text-rt-neutral-text">{row.name}</td>
              <td className="py-2 pe-3 text-rt-neutral-mid">{row.type || '—'}</td>
              <td className="py-2 pe-3">
                <span
                  className={cn(
                    'rounded-full px-2 py-0.5 text-xs font-medium',
                    row.running ? 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-400' : 'bg-rt-border text-rt-neutral-mid',
                  )}
                >
                  {row.running ? t('manager.traffic.statusUp') : t('manager.traffic.statusDown')}
                </span>
                {row.isBridge ? (
                  <span className="ms-1 rounded-full bg-sky-500/15 px-2 py-0.5 text-xs font-medium text-sky-700 dark:text-sky-400">
                    {t('manager.traffic.badgeBridge')}
                  </span>
                ) : null}
              </td>
              <td className="py-2 pe-3 text-rt-neutral-mid">{row.memberOfBridge ?? '—'}</td>
              <td className="py-2 pe-3 tabular-nums text-rt-neutral-text">{formatBytes(row.rxBytes)}</td>
              <td className="py-2 pe-3 tabular-nums text-rt-neutral-text">{formatBytes(row.txBytes)}</td>
              <td className="py-2 pe-3 tabular-nums text-rt-neutral-text">{formatPacketCount(row.rxPackets)}</td>
              <td className="py-2 pe-3 tabular-nums text-rt-neutral-text">{formatPacketCount(row.txPackets)}</td>
              <td className="py-2 pe-3 tabular-nums text-rt-neutral-text">{formatBps(row.rxBps)}</td>
              <td className="py-2 tabular-nums text-rt-neutral-text">{formatBps(row.txBps)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      {snapshot && snapshot.interfaces.length === 0 ? (
        <p className="p-4 text-sm text-rt-neutral-mid">{t('manager.traffic.emptyInterfaces')}</p>
      ) : null}
    </div>
  )

  const body = (
    <div className="space-y-3">
      {loadError ? (
        <p className="rounded-lg border border-amber-500/40 bg-amber-500/10 px-3 py-2 text-sm text-amber-800 dark:text-amber-200">
          {loadError}
        </p>
      ) : null}
      {serversLoading ? (
        <p className="text-sm text-rt-neutral-mid">{t('common.loading')}</p>
      ) : servers.length === 0 && !loadError ? (
        <p className="text-sm text-rt-neutral-mid">{t('manager.traffic.noServers')}</p>
      ) : null}
      {servers.length > 0 ? (
        <div className="flex flex-wrap items-end gap-3">
          <label className="flex flex-col gap-1 text-xs font-medium text-rt-neutral-mid">
            {t('manager.traffic.serverLabel')}
            <select
              className="rounded-lg border border-rt-border bg-rt-page px-3 py-2 text-sm text-rt-neutral-text outline-none ring-rt-primary/30 focus:ring-2"
              value={selectedId ?? ''}
              onChange={(e) => {
                setHubError(null)
                setSnapshot(null)
                setSelectedId(Number(e.target.value))
              }}
            >
              {servers.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name} ({s.host})
                </option>
              ))}
            </select>
          </label>
          {snapshot?.utcIso ? (
            <span className="text-xs text-rt-neutral-mid">
              {t('manager.traffic.lastUpdate')}: {new Date(snapshot.utcIso).toLocaleString()}
            </span>
          ) : null}
        </div>
      ) : null}
      {selected && hubError ? (
        <p className="rounded-lg border border-rose-500/40 bg-rose-500/10 px-3 py-2 text-sm text-rose-800 dark:text-rose-200">
          {hubError}
        </p>
      ) : null}
      {selected ? trendChart : null}
      {selected ? statsCards : null}
      {selected ? table : null}
    </div>
  )

  if (title !== undefined) {
    return <Card title={title}>{body}</Card>
  }

  return body
}
