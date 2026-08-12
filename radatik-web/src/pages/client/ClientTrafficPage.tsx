import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { Alert } from '../../components/ui/Alert'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { PageContent } from '../../components/ui/PageContent'
import {
  fetchClientLiveTraffic,
  fetchClientTrafficTestStatus,
  startClientTrafficTest,
  type ClientLiveTraffic,
  type ClientTrafficTestStatus,
} from '../../lib/clientTrafficApi'
import { chartAxisTick, chartColors, chartTooltipStyle } from '../../lib/chartTheme'
import { formatBps, formatBytes, formatPacketCount } from '../../lib/managerTrafficApi'
import { toneSurface } from '../../lib/tone'
import { cn } from '../../lib/cn'

type TrendPoint = {
  label: string
  rx: number
  tx: number
}

export function ClientTrafficPage() {
  const { t } = useTranslation()
  const [live, setLive] = useState<ClientLiveTraffic | null>(null)
  const [testStatus, setTestStatus] = useState<ClientTrafficTestStatus | null>(null)
  const [loading, setLoading] = useState(true)
  const [startingTest, setStartingTest] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [history, setHistory] = useState<TrendPoint[]>([])

  useEffect(() => {
    let alive = true

    const loadStatus = async () => {
      try {
        const status = await fetchClientTrafficTestStatus()
        if (!alive) return
        setTestStatus(status)
        setError(null)
      } catch {
        if (!alive) return
        setError(t('client.traffic.statusLoadError'))
      }
    }

    const loadLive = async () => {
      try {
        if (alive && !live) setLoading(true)
        const payload = await fetchClientLiveTraffic()
        if (!alive) return
        setLive(payload)
        const label = new Date(payload.utcIso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })
        setHistory((prev) => [...prev, { label, rx: payload.rxBps, tx: payload.txBps }].slice(-24))
      } catch {
        if (!alive) return
        setLive(null)
      } finally {
        if (alive) setLoading(false)
      }
    }

    void loadStatus()
    const statusTimer = setInterval(() => void loadStatus(), 2000)
    const liveTimer = setInterval(() => {
      if (testStatus?.testActive) {
        void loadLive()
      }
    }, 1000)

    return () => {
      alive = false
      clearInterval(statusTimer)
      clearInterval(liveTimer)
    }
  }, [t, testStatus?.testActive])

  const startTestNow = async () => {
    if (!testStatus) return
    const warning = t('client.traffic.startWarning', {
      amount: testStatus.chargeAmount.toLocaleString(),
      duration: testStatus.durationSeconds,
      cooldown: testStatus.cooldownHours,
    })
    if (!window.confirm(warning)) return

    try {
      setStartingTest(true)
      const status = await startClientTrafficTest()
      setTestStatus(status)
      setError(null)
    } catch (e) {
      const reason = e instanceof Error ? e.message : ''
      if (reason === 'insufficientBalance') setError(t('client.traffic.insufficientBalance'))
      else if (reason === 'cooldown') setError(t('client.traffic.cooldownActive'))
      else setError(t('client.traffic.startFailed'))
    } finally {
      setStartingTest(false)
    }
  }

  const statusClass = useMemo(
    () => toneSurface(live?.connected ? 'success' : 'warning'),
    [live?.connected],
  )

  return (
    <PageContent>
      <Card title={t('client.traffic.pageTitle')}>
        <div className="space-y-3">
          {error ? <Alert variant="danger">{error}</Alert> : null}
          {testStatus ? (
            <div className="rt-kpi">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="text-sm text-rt-neutral-mid">
                  {t('client.traffic.chargeLine', { amount: testStatus.chargeAmount.toLocaleString() })}
                </div>
                <Button
                  size="sm"
                  disabled={!testStatus.canStartTest || startingTest}
                  onClick={() => void startTestNow()}
                >
                  {startingTest ? t('common.loading') : t('client.traffic.startTest')}
                </Button>
              </div>
              {testStatus.testActive ? (
                <p className={cn('mt-2 text-xs', 'text-rt-green')}>
                  {t('client.traffic.activeUntil')}:{' '}
                  {testStatus.activeUntilUtcIso
                    ? new Date(testStatus.activeUntilUtcIso).toLocaleTimeString()
                    : '—'}
                </p>
              ) : (
                <p className={cn('mt-2 text-xs', 'text-rt-accent-orange')}>
                  {t('client.traffic.nextEligible')}:{' '}
                  {testStatus.nextEligibleUtcIso
                    ? new Date(testStatus.nextEligibleUtcIso).toLocaleString()
                    : t('client.traffic.availableNow')}
                </p>
              )}
            </div>
          ) : null}
          {loading && !live && testStatus?.testActive ? <p className="text-sm text-rt-neutral-mid">{t('common.loading')}</p> : null}
          {testStatus?.testActive && live ? (
            <>
              <div className={cn('rounded-xl border px-3 py-2 text-sm', statusClass)}>
                {live.connected ? t('client.traffic.connected') : t('client.traffic.disconnected')}
              </div>
              <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                <div className="rt-kpi">
                  <p className="text-xs text-rt-neutral-mid">{t('client.traffic.rxRate')}</p>
                  <p className="mt-1 text-lg font-semibold text-rt-neutral-text">{formatBps(live.rxBps)}</p>
                </div>
                <div className="rt-kpi">
                  <p className="text-xs text-rt-neutral-mid">{t('client.traffic.txRate')}</p>
                  <p className="mt-1 text-lg font-semibold text-rt-neutral-text">{formatBps(live.txBps)}</p>
                </div>
                <div className="rt-kpi">
                  <p className="text-xs text-rt-neutral-mid">{t('client.traffic.rxTotal')}</p>
                  <p className="mt-1 text-lg font-semibold text-rt-neutral-text">{formatBytes(live.rxBytes)}</p>
                </div>
                <div className="rt-kpi">
                  <p className="text-xs text-rt-neutral-mid">{t('client.traffic.txTotal')}</p>
                  <p className="mt-1 text-lg font-semibold text-rt-neutral-text">{formatBytes(live.txBytes)}</p>
                </div>
              </div>

              <div className="rt-kpi">
                <div className="grid gap-2 text-sm text-rt-neutral-mid md:grid-cols-2">
                  <p>
                    {t('client.traffic.userName')}:{' '}
                    <span className="font-medium text-rt-neutral-text">{live.userName}</span>
                  </p>
                  <p>
                    {t('client.traffic.serverName')}:{' '}
                    <span className="font-medium text-rt-neutral-text">{live.serverName}</span>
                  </p>
                  <p>
                    {t('client.traffic.uptime')}:{' '}
                    <span className="font-medium text-rt-neutral-text">{live.uptime || '—'}</span>
                  </p>
                  <p>
                    {t('client.traffic.address')}:{' '}
                    <span className="font-medium text-rt-neutral-text">{live.address || '—'}</span>
                  </p>
                  <p>
                    {t('client.traffic.rxPackets')}:{' '}
                    <span className="font-medium text-rt-neutral-text">
                      {formatPacketCount(live.rxPackets)}
                    </span>
                  </p>
                  <p>
                    {t('client.traffic.txPackets')}:{' '}
                    <span className="font-medium text-rt-neutral-text">
                      {formatPacketCount(live.txPackets)}
                    </span>
                  </p>
                </div>
              </div>

              <div className="rt-kpi">
                <p className="mb-2 text-sm font-semibold text-rt-neutral-text">{t('client.traffic.trendTitle')}</p>
                <div className="h-52">
                  <ResponsiveContainer width="100%" height="100%">
                    <LineChart data={history}>
                      <XAxis dataKey="label" tick={chartAxisTick} minTickGap={20} />
                      <YAxis
                        tick={chartAxisTick}
                        tickFormatter={(v) => formatBps(Number(v))}
                        width={84}
                      />
                      <Tooltip
                        contentStyle={chartTooltipStyle}
                        formatter={(value) =>
                          formatBps(typeof value === 'number' ? value : Number(value ?? 0))
                        }
                      />
                      <Line
                        type="monotone"
                        dataKey="rx"
                        stroke={chartColors.secondary}
                        strokeWidth={2}
                        dot={false}
                        name={t('client.traffic.rxRate')}
                      />
                      <Line
                        type="monotone"
                        dataKey="tx"
                        stroke={chartColors.green}
                        strokeWidth={2}
                        dot={false}
                        name={t('client.traffic.txRate')}
                      />
                    </LineChart>
                  </ResponsiveContainer>
                </div>
              </div>
            </>
          ) : (
            <p className="text-sm text-rt-neutral-mid">{t('client.traffic.needStartTest')}</p>
          )}
        </div>
      </Card>
    </PageContent>
  )
}
