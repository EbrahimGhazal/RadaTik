import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert } from '../../components/ui/Alert'
import { Card } from '../../components/ui/Card'
import { PageContent } from '../../components/ui/PageContent'
import { SettingsRow, Toggle } from '../../components/ui/Toggle'

const LS = 'radatik-admin-settings-mock'

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
    <PageContent>
      <Card title={t('pages.settings')}>
        <p className="mb-6 text-sm text-rt-neutral-mid">{t('settingsPage.title')}</p>
        <ul className="space-y-3">
          {(
            [
              ['notifyEmail', t('settingsPage.notifyEmail'), t('settingsPage.notifyEmailDesc')],
              ['compactTables', t('settingsPage.compactTables'), t('settingsPage.compactTablesDesc')],
            ] as const
          ).map(([key, label, description]) => (
            <SettingsRow key={key} label={label} description={description}>
              <Toggle checked={prefs[key]} onChange={() => toggle(key)} label={label} />
            </SettingsRow>
          ))}
        </ul>
        {toast ? <Alert variant="success" className="mt-4">{t('settingsPage.saved')}</Alert> : null}
      </Card>
    </PageContent>
  )
}
