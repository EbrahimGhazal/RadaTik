import { useTranslation } from 'react-i18next'
import { Card } from '../../components/ui/Card'
import { useMockResourceQuery } from '../../hooks/useMockResourceQuery'

interface TeamMember {
  id: string
  name: string
  role: string
}

export function ManagerTeamPage() {
  const { t } = useTranslation()
  const { data, loading, error } = useMockResourceQuery<TeamMember[]>('managerTeam')
  const team = data ?? []

  return (
    <div className="space-y-4">
      <Card title={t('nav.team')}>
        {error ? <p className="mb-4 text-sm text-red-600">{error}</p> : null}
        {loading && !team.length ? (
          <p className="mb-4 text-sm text-rt-neutral-mid">{t('common.loading')}</p>
        ) : null}
        <ul className="divide-y divide-rt-border rounded-lg border border-rt-border">
          {team.map((m) => (
            <li key={m.id} className="flex flex-col gap-1 px-4 py-4 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <p className="font-medium text-rt-neutral-text">{m.name}</p>
                <p className="text-sm text-rt-neutral-mid">{m.role}</p>
              </div>
              <span className="inline-flex w-fit rounded-full bg-rt-primary/10 px-2.5 py-1 text-xs font-medium text-rt-primary">
                Active
              </span>
            </li>
          ))}
        </ul>
      </Card>
    </div>
  )
}
