import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Card } from '../../components/ui/Card'
import { cn } from '../../lib/cn'

const LS = 'radtik-admin-settings-mock'

interface Prefs {
  notifyEmail: boolean
  compactTables: boolean
}

function readPrefs(): Prefs {
  try {
    const raw = localStorage.getItem(LS)
    if (!raw) return { notifyEmail: true, compactTables: false }
    return JSON.parse(raw) as Prefs
  } catch {
    return { notifyEmail: true, compactTables: false }
  }
}

export function AdminSettingsPage() {
  const { t } = useTranslation()
  const [prefs, setPrefs] = useState<Prefs>(readPrefs)
  const [toast, setToast] = useState(false)

  useEffect(() => {
    localStorage.setItem(LS, JSON.stringify(prefs))
  }, [prefs])

  const toggle = (key: keyof Prefs) => {
    setPrefs((p) => ({ ...p, [key]: !p[key] }))
    setToast(true)
    window.setTimeout(() => setToast(false), 2000)
  }

  return (
    <div className="space-y-4">
      <Card title={t('pages.settings')}>
        <p className="mb-6 text-sm text-rt-neutral-mid">{t('settingsPage.title')}</p>
        <ul className="space-y-3">
          {(
            [
              ['notifyEmail', t('settingsPage.notifyEmail')],
              ['compactTables', t('settingsPage.compactTables')],
            ] as const
          ).map(([key, label]) => (
            <li
              key={key}
              className="flex items-center justify-between gap-4 rounded-lg border border-rt-border px-4 py-3"
            >
              <span className="text-sm font-medium text-rt-neutral-text">{label}</span>
              <button
                type="button"
                role="switch"
                aria-checked={prefs[key]}
                onClick={() => toggle(key)}
                className={cn(
                  'flex h-7 w-12 items-center rounded-full p-0.5 transition-colors',
                  prefs[key] ? 'bg-rt-primary' : 'bg-rt-neutral-mid/40',
                )}
              >
                <span
                  className={cn(
                    'h-6 w-6 rounded-full bg-white shadow ring-1 ring-black/5 transition-[margin]',
                    prefs[key] ? 'ms-auto' : '',
                  )}
                />
              </button>
            </li>
          ))}
        </ul>
        {toast ? (
          <p className="mt-4 text-sm text-rt-secondary">{t('settingsPage.saved')}</p>
        ) : null}
      </Card>
    </div>
  )
}
