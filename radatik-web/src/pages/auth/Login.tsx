import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, Navigate, useLocation } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { AuthCard, AuthLayout } from '../../components/layout/AuthLayout'
import { Button } from '../../components/ui/Button'
import { FieldLabel, Input } from '../../components/ui/Input'
import { useAuthStore } from '../../store/authStore'
import { loginWithDatabase, mapLoginUser } from '../../lib/spaAuthApi'
import { ROLE_ROUTE } from '../../types'

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
    <AuthLayout
      heroTitle={t('auth.heroTitle')}
      heroSubtitle={t('auth.heroSubtitle')}
      footerHint={t('auth.dbLoginHint')}
    >
      <AuthCard title={t('auth.login')} subtitle={t('auth.loginSubtitle')}>
        <p className="mb-4 text-center text-xs text-rt-neutral-mid lg:hidden">{t('auth.dbLoginHint')}</p>
        <form className="space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
          <div>
            <FieldLabel htmlFor="login-userName">{t('auth.userNameOrEmail')}</FieldLabel>
            <Input
              id="login-userName"
              type="text"
              autoComplete="username"
              placeholder={t('auth.userNameHint')}
              disabled={submitting}
              {...register('userName', { required: true })}
            />
            {errors.userName ? (
              <p className="mt-1.5 text-xs text-rt-danger">{t('auth.userNameOrEmail')}</p>
            ) : null}
          </div>
          <div>
            <FieldLabel htmlFor="login-password">{t('auth.password')}</FieldLabel>
            <Input
              id="login-password"
              type="password"
              autoComplete="current-password"
              disabled={submitting}
              {...register('password', { required: true })}
            />
            {errors.password ? (
              <p className="mt-1.5 text-xs text-rt-danger">{t('auth.passwordRequired')}</p>
            ) : null}
          </div>
          {errors.root ? (
            <p className="rounded-xl bg-rt-danger/10 px-3 py-2 text-sm text-rt-danger">{errors.root.message}</p>
          ) : null}
          <Button type="submit" className="w-full" size="lg" disabled={submitting}>
            {submitting ? t('auth.loggingIn') : t('auth.login')}
          </Button>
        </form>

        <p className="mt-6 text-center text-sm text-rt-neutral-mid">
          <Link className="font-medium text-rt-primary transition-colors hover:underline" to="/signup">
            {t('auth.toSignup')}
          </Link>
        </p>
      </AuthCard>
    </AuthLayout>
  )
}
