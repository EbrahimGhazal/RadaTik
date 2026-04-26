import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, Navigate, useLocation } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Logo } from '../../components/brand/Logo'
import { useAuthStore } from '../../store/authStore'
import { loginWithDatabase, mapLoginUser } from '../../lib/spaAuthApi'
import { ROLE_ROUTE } from '../../types'
import { cn } from '../../lib/cn'

interface FormValues {
  userName: string
  password: string
}

export function Login() {
  const { t } = useTranslation()
  const user = useAuthStore((s) => s.user)
  const login = useAuthStore((s) => s.login)
  const location = useLocation()
  const from = (location.state as { from?: string } | null)?.from
  const [submitting, setSubmitting] = useState(false)

  const {
    register,
    handleSubmit,
    formState: { errors },
    setError,
  } = useForm<FormValues>({ defaultValues: { userName: '', password: '' } })

  if (user) {
    return <Navigate to={from && from !== '/login' ? from : ROLE_ROUTE[user.role]} replace />
  }

  const onSubmit = async (values: FormValues) => {
    setSubmitting(true)
    try {
      const result = await loginWithDatabase(values.userName.trim(), values.password)
      if (result.ok) {
        login(mapLoginUser(result.user))
        return
      }
      const msg = result.message
      if (msg === 'networkError') {
        setError('root', { message: t('auth.networkError') })
      } else if (msg === 'badResponse') {
        setError('root', { message: t('auth.serverError') })
      } else if (msg && msg !== 'invalidCredentials') {
        setError('root', { message: msg })
      } else {
        setError('root', { message: t('auth.invalidCredentials') })
      }
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="flex min-h-dvh items-center justify-center bg-rt-page px-4 py-10">
      <div className="w-full max-w-md rounded-2xl border border-rt-border bg-rt-surface p-8 shadow-lg">
        <div className="mb-8 flex flex-col items-center gap-2">
          <Logo />
          <p className="text-center text-sm text-rt-neutral-mid">{t('auth.dbLoginHint')}</p>
        </div>
        <form className="space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
          <div>
            <label className="mb-1 block text-sm font-medium text-rt-neutral-text" htmlFor="login-userName">
              {t('auth.userNameOrEmail')}
            </label>
            <input
              id="login-userName"
              type="text"
              autoComplete="username"
              placeholder={t('auth.userNameHint')}
              disabled={submitting}
              className={cn(
                'w-full rounded-lg border border-rt-border bg-rt-page px-3 py-2.5 text-sm outline-none ring-rt-primary/30 focus:ring-2',
                'disabled:opacity-60',
              )}
              {...register('userName', { required: true })}
            />
            {errors.userName ? (
              <p className="mt-1 text-xs text-red-600 dark:text-red-400">{t('auth.userNameOrEmail')}</p>
            ) : null}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-rt-neutral-text" htmlFor="login-password">
              {t('auth.password')}
            </label>
            <input
              id="login-password"
              type="password"
              autoComplete="current-password"
              disabled={submitting}
              className={cn(
                'w-full rounded-lg border border-rt-border bg-rt-page px-3 py-2.5 text-sm outline-none ring-rt-primary/30 focus:ring-2',
                'disabled:opacity-60',
              )}
              {...register('password', { required: true })}
            />
            {errors.password ? (
              <p className="mt-1 text-xs text-red-600 dark:text-red-400">{t('auth.passwordRequired')}</p>
            ) : null}
          </div>
          {errors.root ? (
            <p className="text-sm text-red-600 dark:text-red-400">{errors.root.message}</p>
          ) : null}
          <button
            type="submit"
            disabled={submitting}
            className="w-full rounded-lg bg-rt-primary px-4 py-2.5 text-sm font-semibold text-white shadow-md transition hover:opacity-95 active:scale-[0.99] disabled:opacity-60"
          >
            {submitting ? t('auth.loggingIn') : t('auth.login')}
          </button>
        </form>
        <p className="mt-6 text-center text-sm text-rt-neutral-mid">
          <Link className="font-medium text-rt-primary hover:underline" to="/signup">
            {t('auth.toSignup')}
          </Link>
        </p>
      </div>
    </div>
  )
}
