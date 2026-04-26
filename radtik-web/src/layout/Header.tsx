import { LogOut, Menu, Moon, Sun, Languages } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { Logo } from '../components/brand/Logo'
import { useAuthStore } from '../store/authStore'
import { useUiStore } from '../store/uiStore'
import { ROLE_LABEL_KEY, ROLE_ROUTE, type UserRole } from '../types'
import { cn } from '../lib/cn'
import { logoutSpaSession } from '../lib/spaAuthApi'

export function Header({ onMenuClick }: { onMenuClick?: () => void }) {
  const { t } = useTranslation()
  const user = useAuthStore((s) => s.user)
  const logout = useAuthStore((s) => s.logout)
  const theme = useUiStore((s) => s.theme)
  const setTheme = useUiStore((s) => s.setTheme)
  const lang = useUiStore((s) => s.lang)
  const setLang = useUiStore((s) => s.setLang)

  const roleKey = user ? ROLE_LABEL_KEY[user.role as UserRole] : ''

  return (
    <header className="sticky top-0 z-40 border-b border-rt-border bg-rt-surface/90 backdrop-blur-md">
      <div className="flex items-center justify-between gap-3 px-4 py-3 md:px-6">
        <div className="flex min-w-0 items-center gap-3">
          <button
            type="button"
            className="inline-flex rounded-lg border border-rt-border p-2 md:hidden"
            aria-label={t('common.openMenu')}
            onClick={onMenuClick}
          >
            <Menu className="size-5 text-rt-primary" />
          </button>
          <Link
            to={user ? ROLE_ROUTE[user.role as UserRole] : '/login'}
            className="min-w-0 shrink"
          >
            <Logo compact />
          </Link>
        </div>

        <div className="flex items-center gap-2 sm:gap-3">
          {user ? (
            <div className="hidden items-center gap-2 sm:flex">
              <div className="size-9 rounded-full bg-gradient-to-br from-rt-primary to-rt-secondary p-0.5">
                <div className="flex size-full items-center justify-center rounded-full bg-rt-surface text-xs font-semibold text-rt-primary">
                  {user.fullName
                    .split(' ')
                    .map((p) => p[0])
                    .join('')
                    .slice(0, 2)
                    .toUpperCase()}
                </div>
              </div>
              <div className="leading-tight rtl:text-end">
                <p className="max-w-[140px] truncate text-sm font-medium text-rt-neutral-text">
                  {user.fullName}
                </p>
                <p className="text-xs text-rt-neutral-mid">{t(roleKey)}</p>
              </div>
            </div>
          ) : null}

          <div className="flex items-center gap-1 rounded-lg border border-rt-border p-1">
            <button
              type="button"
              className={cn(
                'inline-flex items-center gap-1 rounded-md px-2 py-1.5 text-xs font-medium transition-colors',
                'hover:bg-rt-neutral-bg',
              )}
              onClick={() => setLang(lang === 'en' ? 'ar' : 'en')}
              title={t('header.language')}
            >
              <Languages className="size-4" />
              <span className="hidden sm:inline">{lang === 'en' ? 'EN' : 'عربي'}</span>
            </button>
            <button
              type="button"
              className="inline-flex rounded-md p-1.5 hover:bg-rt-neutral-bg"
              title={theme === 'dark' ? t('header.lightMode') : t('header.darkMode')}
              onClick={() => setTheme(theme === 'dark' ? 'light' : 'dark')}
            >
              {theme === 'dark' ? (
                <Sun className="size-4 text-rt-accent-orange" />
              ) : (
                <Moon className="size-4 text-rt-primary" />
              )}
            </button>
          </div>

          {user ? (
            <button
              type="button"
              onClick={async () => {
                await logoutSpaSession()
                logout()
              }}
              className="inline-flex items-center gap-1 rounded-lg border border-rt-border px-3 py-2 text-sm font-medium hover:bg-rt-neutral-bg"
            >
              <LogOut className="size-4 rtl:rotate-180" />
              <span className="hidden md:inline">{t('auth.logout')}</span>
            </button>
          ) : null}
        </div>
      </div>
    </header>
  )
}
