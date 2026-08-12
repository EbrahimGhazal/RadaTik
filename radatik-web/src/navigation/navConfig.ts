import type { LucideIcon } from 'lucide-react'
import {
  CreditCard,
  LayoutDashboard,
  Network,
  Receipt,
  Settings,
  Users,
  Headphones,
  ListTodo,
  BarChart3,
  Building2,
  Wifi,
  LifeBuoy,
  ArrowRightLeft,
  Activity,
} from 'lucide-react'
import type { UserRole } from '../types'

export interface NavItem {
  to: string
  labelKey: string
  icon: LucideIcon
  /** Show on compact mobile bottom bar */
  primary?: boolean
}

const p = (prefix: string) => (path: string) =>
  path === '' ? prefix : `${prefix}/${path}`

export const navByRole: Record<UserRole, NavItem[]> = {
  system_admin: (() => {
    const r = p('/admin')
    return [
      { to: r(''), labelKey: 'nav.dashboard', icon: LayoutDashboard, primary: true },
      { to: r('users'), labelKey: 'nav.users', icon: Users, primary: true },
      { to: r('networks'), labelKey: 'nav.networks', icon: Network, primary: true },
      { to: r('payments'), labelKey: 'nav.payments', icon: CreditCard, primary: true },
      { to: r('reports'), labelKey: 'nav.reports', icon: BarChart3 },
      { to: r('settings'), labelKey: 'nav.settings', icon: Settings },
    ]
  })(),
  employee: (() => {
    const r = p('/employee')
    return [
      { to: r(''), labelKey: 'nav.dashboard', icon: LayoutDashboard, primary: true },
      { to: r('support'), labelKey: 'nav.customerSupport', icon: Headphones, primary: true },
      { to: r('tasks'), labelKey: 'nav.tasks', icon: ListTodo, primary: true },
      { to: r('payments'), labelKey: 'nav.payments', icon: CreditCard, primary: true },
    ]
  })(),
  company_manager: (() => {
    const r = p('/manager')
    return [
      { to: r(''), labelKey: 'nav.dashboard', icon: LayoutDashboard, primary: true },
      { to: r('mikrotik-traffic'), labelKey: 'nav.mikrotikTraffic', icon: Activity },
      { to: r('billing'), labelKey: 'nav.billing', icon: Receipt, primary: true },
      { to: r('team'), labelKey: 'nav.team', icon: Building2, primary: true },
      { to: r('reports'), labelKey: 'nav.reports', icon: BarChart3, primary: true },
      { to: r('settings'), labelKey: 'nav.settings', icon: Settings },
    ]
  })(),
  client: (() => {
    const r = p('/client')
    return [
      { to: r(''), labelKey: 'nav.dashboard', icon: LayoutDashboard, primary: true },
      { to: r('traffic'), labelKey: 'nav.myTraffic', icon: Activity, primary: true },
      { to: r('network'), labelKey: 'nav.myNetwork', icon: Wifi, primary: true },
      { to: r('billing'), labelKey: 'nav.billing', icon: CreditCard, primary: true },
      { to: r('support'), labelKey: 'nav.support', icon: LifeBuoy, primary: true },
    ]
  })(),
  collection_point: (() => {
    const r = p('/collection')
    return [
      { to: r(''), labelKey: 'nav.dashboard', icon: LayoutDashboard, primary: true },
      { to: r('transactions'), labelKey: 'nav.transactions', icon: ArrowRightLeft, primary: true },
      { to: r('payments'), labelKey: 'nav.payments', icon: Receipt, primary: true },
      { to: r('settings'), labelKey: 'nav.settings', icon: Settings },
    ]
  })(),
}
