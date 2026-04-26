import { useForm } from 'react-hook-form'
import { Link, Navigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Logo } from '../../components/brand/Logo'
import { useAuthStore } from '../../store/authStore'
import { SIGNUP_ROLE_OPTIONS, saveRegistered, findUserByEmail } from '../../auth/userDirectory'
import { ROLE_ROUTE, type UserRole } from '../../types'
import { cn } from '../../lib/cn'

interface FormValues {
  fullName: string
  email: string
  password: string
  confirmPassword: string
  role: UserRole
}

export function Signup() {
  const { t } = useTranslation()
  const user = useAuthStore((s) => s.user)
  const login = useAuthStore((s) => s.login)

  const {
    register,
    handleSubmit,
    getValues,
    formState: { errors },
    setError,
  } = useForm<FormValues>({
    defaultValues: {
      fullName: '',
      email: '',
      password: '',
      confirmPassword: '',
      role: 'client',
    },
  })

  if (user) {
    return <Navigate to={ROLE_ROUTE[user.role]} replace />
  }

  const onSubmit = (values: FormValues) => {
    if (values.password !== values.confirmPassword) {
      setError('confirmPassword', { message: 'Passwords must match' })
      return
    }
    if (findUserByEmail(values.email)) {
      setError('email', { message: 'Email already registered — log in instead.' })
      return
    }
    const newUser = {
      id: crypto.randomUUID(),
      fullName: values.fullName.trim(),
      email: values.email.trim(),
      role: values.role,
    }
    saveRegistered(newUser)
    login(newUser)
  }

  return (
    <div className="flex min-h-dvh items-center justify-center bg-rt-page px-4 py-10">
      <div className="w-full max-w-md rounded-2xl border border-rt-border bg-rt-surface p-8 shadow-lg">
        <div className="mb-8 flex justify-center">
          <Logo />
        </div>
        <form className="space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
          <div>
            <label className="mb-1 block text-sm font-medium">{t('auth.fullName')}</label>
            <input
              className="w-full rounded-lg border border-rt-border bg-rt-page px-3 py-2.5 text-sm outline-none ring-rt-primary/30 focus:ring-2"
              {...register('fullName', { required: true, minLength: 2 })}
            />
            {errors.fullName ? (
              <p className="mt-1 text-xs text-red-600 dark:text-red-400">Required</p>
            ) : null}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium">{t('auth.email')}</label>
            <input
              type="email"
              className="w-full rounded-lg border border-rt-border bg-rt-page px-3 py-2.5 text-sm outline-none ring-rt-primary/30 focus:ring-2"
              {...register('email', { required: true })}
            />
            {errors.email ? (
              <p className="mt-1 text-xs text-red-600 dark:text-red-400">
                {(errors.email.message as string) || 'Invalid'}
              </p>
            ) : null}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium">{t('auth.role')}</label>
            <select
              className="w-full rounded-lg border border-rt-border bg-rt-page px-3 py-2.5 text-sm outline-none ring-rt-primary/30 focus:ring-2"
              {...register('role', { required: true })}
            >
              {SIGNUP_ROLE_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {t(o.labelKey)}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium">{t('auth.password')}</label>
            <input
              type="password"
              className="w-full rounded-lg border border-rt-border bg-rt-page px-3 py-2.5 text-sm outline-none ring-rt-primary/30 focus:ring-2"
              {...register('password', { required: true, minLength: 6 })}
            />
            {errors.password ? (
              <p className="mt-1 text-xs text-red-600 dark:text-red-400">Min 6 characters</p>
            ) : null}
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium">{t('auth.confirmPassword')}</label>
            <input
              type="password"
              className="w-full rounded-lg border border-rt-border bg-rt-page px-3 py-2.5 text-sm outline-none ring-rt-primary/30 focus:ring-2"
              {...register('confirmPassword', {
                required: true,
                validate: (v) => v === getValues('password') || 'Must match password',
              })}
            />
            {errors.confirmPassword ? (
              <p className="mt-1 text-xs text-red-600 dark:text-red-400">
                {(errors.confirmPassword.message as string) || 'Invalid'}
              </p>
            ) : null}
          </div>
          <button
            type="submit"
            className={cn(
              'w-full rounded-lg bg-rt-primary px-4 py-2.5 text-sm font-semibold text-white shadow-md transition hover:opacity-95',
            )}
          >
            {t('auth.signup')}
          </button>
        </form>
        <p className="mt-6 text-center text-sm text-rt-neutral-mid">
          <Link className="font-medium text-rt-primary hover:underline" to="/login">
            {t('auth.toLogin')}
          </Link>
        </p>
      </div>
    </div>
  )
}
