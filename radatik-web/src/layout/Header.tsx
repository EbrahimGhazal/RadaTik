import { LogOut, Menu, Moon, Search, Sun, Languages } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { Logo } from '../components/brand/Logo'
import { Button } from '../components/ui/Button'
import { useAuthStore } from '../store/authStore'
import { useUiStore } from '../store/uiStore'
import { ROLE_LABEL_KEY, ROLE_ROUTE, type UserRole } from '../types'
import { cn } from '../lib/cn'
import { logoutSpaSession } from '../lib/spaAuthApi'

export function Header({
  onMenuClick,
  onQuickNavOpen,
}: {
  onMenuClick?: () => void
  onQuickNavOpen?: () => void
}) {
  const { t } = useTranslation()
  const user = useAuthStore((s) => s.user)
  const logout = useAuthStore((s) => s.logout)
  const theme = useUiStore((s) => s.theme)
  const setTheme = useUiStore((s) => s.setTheme)
  const lang = useUiStore((s) => s.lang)
  const setLang = useUiStore((s) => s.setLang)

  const roleKey = user ? ROLE_LABEL_KEY[user.role as UserRole] : ''

  return (
    <header className="sticky top-0 z-40 border-b border-rt-border bg-rt-surface/85 backdrop-blur-xl dark:shadow-[var(--shadow-rt-glow)]">
      <div className="flex items-center justify-between gap-2 px-3 py-2.5 sm:gap-3 sm:px-4 md:px-6 md:py-3">
        <div className="flex min-w-0 flex-1 items-center gap-2 sm:gap-3">
          <button
            type="button"
            className="inline-flex rounded-xl border border-rt-border p-2 transition-colors hover:bg-rt-neutral-bg md:hidden"
            aria-label={t('common.openMenu')}
            onClick={onMenuClick}
          >
            <Menu className="size-5 text-rt-primary" />
          </button>
          <Link
            to={user ? ROLE_ROUTE[user.role as UserRole] : '/login'}
            className="min-w-0 shrink md:hidden"
          >
            <Logo compact />
          </Link>

          {onQuickNavOpen ? (
            <button
              type="button"
              onClick={onQuickNavOpen}
              className="flex min-w-0 flex-1 items-center gap-2 rounded-xl border border-rt-border bg-rt-neutral-bg/50 px-3 py-2 text-sm text-rt-neutral-mid transition-colors hover:border-rt-primary/30 hover:text-rt-neutral-text sm:max-w-xs md:max-w-sm lg:max-w-md"
            >
              <Search className="size-4 shrink-0" aria-hidden />
              <span className="truncate">{t('quickNav.open')}</span>
              <kbd className="ms-auto hidden rounded-md border border-rt-border bg-rt-surface px-1.5 py-0.5 text-[10px] font-medium lg:inline">
                ⌘K
              </kbd>
            </button>
          ) : null}
        </div>

        <div className="flex shrink-0 items-center gap-1.5 sm:gap-2">
          {user ? (
            <div className="hidden items-center gap-2 lg:flex">
              <div className="size-9 rounded-full bg-gradient-to-br from-rt-primary to-rt-secondary p-0.5 shadow-sm">
                <div className="flex size-full items-center justify-center rounded-full bg-rt-surface text-xs font-bold text-rt-primary">
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

          <div className="flex items-center gap-0.5 rounded-xl border border-rt-border bg-rt-neutral-bg/40 p-0.5">
            <button
              type="button"
              className={cn(
                'inline-flex items-center gap-1 rounded-lg px-2 py-1.5 text-xs font-medium transition-colors',
                'hover:bg-rt-surface',
              )}
              onClick={() => setLang(lang === 'en' ? 'ar' : 'en')}
              title={t('header.language')}
            >
              <Languages className="size-4" />
              <span className="hidden sm:inline">{lang === 'en' ? 'EN' : 'عربي'}</span>
            </button>
            <button
              type="button"
              className="inline-flex rounded-lg p-1.5 transition-colors hover:bg-rt-surface"
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
            <Button
              variant="secondary"
              size="sm"
              className="hidden sm:inline-flex"
              leftIcon={<LogOut className="size-4 rtl:rotate-180" />}
              onClick={async () => {
                await logoutSpaSession()
                logout()
              }}
            >
              <span className="hidden md:inline">{t('auth.logout')}</span>
            </Button>
          ) : null}
        </div>
      </div>
    </header>
  )
}
