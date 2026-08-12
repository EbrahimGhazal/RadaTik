import type { ReactNode } from 'react'
import { Moon, Sun } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Logo } from '../brand/Logo'
import { useUiStore } from '../../store/uiStore'
import { cn } from '../../lib/cn'

/** Shared auth shell — hero panel (desktop) + form column + theme toggle. */
export function AuthLayout({
  children,
  heroTitle,
  heroSubtitle,
  footerHint,
}: {
  children: ReactNode
  heroTitle: string
  heroSubtitle: string
  footerHint?: string
}) {
  const { t } = useTranslation()
  const theme = useUiStore((s) => s.theme)
  const setTheme = useUiStore((s) => s.setTheme)

  return (
    <div className="rt-app-bg relative flex min-h-dvh">
      <button
        type="button"
        className={cn(
          'absolute end-4 top-4 z-10 inline-flex items-center gap-2 rounded-xl border border-rt-border',
          'bg-rt-surface/80 p-2.5 backdrop-blur-xl transition-colors hover:bg-rt-surface sm:end-6 sm:top-6',
        )}
        title={theme === 'dark' ? t('header.lightMode') : t('header.darkMode')}
        onClick={() => setTheme(theme === 'dark' ? 'light' : 'dark')}
      >
        {theme === 'dark' ? (
          <Sun className="size-4 text-rt-accent-orange" aria-hidden />
        ) : (
          <Moon className="size-4 text-rt-primary" aria-hidden />
        )}
      </button>

      <aside className="relative hidden w-[min(44%,520px)] flex-col justify-between overflow-hidden border-e border-rt-border bg-rt-surface p-8 xl:p-10 lg:flex">
        <div className="pointer-events-none absolute inset-0 bg-gradient-to-br from-rt-primary/12 via-transparent to-rt-secondary/10" />
        <div
          className="pointer-events-none absolute -start-24 top-1/3 size-64 rounded-full bg-rt-primary/10 blur-3xl"
          aria-hidden
        />
        <div className="relative">
          <Logo />
          <p className="mt-8 max-w-sm text-xl font-semibold leading-snug tracking-tight text-rt-neutral-text xl:text-2xl">
            {heroTitle}
          </p>
          <p className="mt-3 max-w-sm text-sm leading-relaxed text-rt-neutral-mid">{heroSubtitle}</p>
        </div>
        {footerHint ? <p className="relative text-xs leading-relaxed text-rt-neutral-mid">{footerHint}</p> : null}
      </aside>

      <div className="flex flex-1 items-center justify-center px-4 py-14 sm:px-6 lg:py-10">
        <div className="w-full max-w-md">{children}</div>
      </div>
    </div>
  )
}

export function AuthCard({ title, subtitle, children }: { title: string; subtitle?: string; children: ReactNode }) {
  return (
    <div className="rt-auth-card">
      <div className="mb-6 flex flex-col items-center gap-3 lg:hidden">
        <Logo />
      </div>
      <h1 className="text-xl font-bold tracking-tight text-rt-neutral-text sm:text-2xl">{title}</h1>
      {subtitle ? <p className="mt-1.5 text-sm text-rt-neutral-mid">{subtitle}</p> : null}
      <div className="mt-6">{children}</div>
    </div>
  )
}
