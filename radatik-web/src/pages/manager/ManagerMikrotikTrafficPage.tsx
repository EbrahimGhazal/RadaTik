import { useTranslation } from 'react-i18next'
import { ManagerMikrotikTrafficPanel } from '../../components/manager/ManagerMikrotikTrafficPanel'

export function ManagerMikrotikTrafficPage() {
  const { t } = useTranslation()
  return <ManagerMikrotikTrafficPanel title={t('manager.traffic.pageTitle')} />
}
