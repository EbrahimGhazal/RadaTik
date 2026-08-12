import { useTranslation } from 'react-i18next'
import { Card } from '../../components/ui/Card'
import { PageContent } from '../../components/ui/PageContent'
import { QueryStatus } from '../../components/ui/QueryStatus'
import { StatusBadge } from '../../components/ui/StatusBadge'
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
    <PageContent>
      <Card title={t('nav.team')}>
        <div className="mb-4">
          <QueryStatus loading={loading} error={error} hasData={team.length > 0} />
        </div>
        <ul className="rt-divide-list">
          {team.map((m) => (
            <li key={m.id} className="flex flex-col gap-2 px-4 py-4 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <p className="font-medium text-rt-neutral-text">{m.name}</p>
                <p className="text-sm text-rt-neutral-mid">{m.role}</p>
              </div>
              <StatusBadge label={t('status.active')} code="active" />
            </li>
          ))}
        </ul>
      </Card>
    </PageContent>
  )
}
