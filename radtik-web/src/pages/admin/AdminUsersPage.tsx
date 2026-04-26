import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Card } from '../../components/ui/Card'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { useMockResourceQuery } from '../../hooks/useMockResourceQuery'
import { translateRole } from '../../lib/roleI18n'
import type { UserRole } from '../../types'
import { SIGNUP_ROLE_OPTIONS } from '../../auth/userDirectory'
import { cn } from '../../lib/cn'

interface DirectoryUser {
  id: string
  fullName: string
  email: string
  role: UserRole | string
  status: string
  lastActive: string
}

export function AdminUsersPage() {
  const { t } = useTranslation()
  const { data, loading, error } = useMockResourceQuery<DirectoryUser[]>('users')
  const [q, setQ] = useState('')
  const [role, setRole] = useState<string>('')

  const filtered = useMemo(() => {
    const rows = data ?? []
    const needle = q.trim().toLowerCase()
    return rows.filter((r) => {
      const matchRole = !role || r.role === role
      if (!needle) return matchRole
      const blob = `${r.fullName} ${r.email}`.toLowerCase()
      return matchRole && blob.includes(needle)
    })
  }, [data, q, role])

  const columns: Column<DirectoryUser>[] = [
    { key: 'n', header: t('auth.fullName'), cell: (r) => r.fullName },
    { key: 'e', header: t('auth.email'), cell: (r) => r.email },
    { key: 'r', header: t('auth.role'), cell: (r) => translateRole(t, String(r.role)) },
    {
      key: 's',
      header: t('table.status'),
      cell: (r) => (
        <StatusBadge
          label={r.status === 'active' ? t('status.active') : t('status.suspended')}
          code={r.status}
        />
      ),
    },
    {
      key: 'la',
      header: t('directory.lastActive'),
      cell: (r) => new Date(r.lastActive).toLocaleString(),
    },
  ]

  return (
    <div className="space-y-4">
      <Card title={t('pages.users')}>
        {error ? <p className="mb-4 text-sm text-red-600">{error}</p> : null}
        {loading && !(data?.length) ? (
          <p className="mb-4 text-sm text-rt-neutral-mid">{t('common.loading')}</p>
        ) : null}
        <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-end">
          <div className="flex-1">
            <label className="mb-1 block text-xs font-medium text-rt-neutral-mid">
              {t('directory.search')}
            </label>
            <input
              value={q}
              onChange={(e) => setQ(e.target.value)}
              className={cn(
                'w-full rounded-lg border border-rt-border bg-rt-page px-3 py-2 text-sm outline-none ring-rt-primary/30 focus:ring-2',
              )}
            />
          </div>
          <div className="sm:w-48">
            <label className="mb-1 block text-xs font-medium text-rt-neutral-mid">
              {t('directory.roleFilter')}
            </label>
            <select
              value={role}
              onChange={(e) => setRole(e.target.value)}
              className="w-full rounded-lg border border-rt-border bg-rt-page px-3 py-2 text-sm outline-none ring-rt-primary/30 focus:ring-2"
            >
              <option value="">{t('directory.allRoles')}</option>
              {SIGNUP_ROLE_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {t(o.labelKey)}
                </option>
              ))}
            </select>
          </div>
        </div>
        <DataTable columns={columns} rows={filtered} />
      </Card>
    </div>
  )
}
