import { useForm } from 'react-hook-form'
import { Link, Navigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { AuthCard, AuthLayout } from '../../components/layout/AuthLayout'
import { Button } from '../../components/ui/Button'
import { FieldLabel, Input, Select } from '../../components/ui/Input'
import { useAuthStore } from '../../store/authStore'
import { SIGNUP_ROLE_OPTIONS, saveRegistered, findUserByEmail } from '../../auth/userDirectory'
import { ROLE_ROUTE, type UserRole } from '../../types'

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
      setError('confirmPassword', { message: t('auth.passwordMismatch') })
      return
    }
    if (findUserByEmail(values.email)) {
      setError('email', { message: t('auth.emailTaken') })
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
    <AuthLayout
      heroTitle={t('auth.signupHeroTitle')}
      heroSubtitle={t('auth.signupHeroSubtitle')}
      footerHint={t('auth.signupHint')}
    >
      <AuthCard title={t('auth.signup')} subtitle={t('auth.signupSubtitle')}>
        <form className="space-y-4" onSubmit={handleSubmit(onSubmit)} noValidate>
          <div>
            <FieldLabel htmlFor="signup-fullName">{t('auth.fullName')}</FieldLabel>
            <Input
              id="signup-fullName"
              {...register('fullName', { required: true, minLength: 2 })}
            />
            {errors.fullName ? (
              <p className="mt-1.5 text-xs text-rt-danger">{t('auth.fullNameRequired')}</p>
            ) : null}
          </div>
          <div>
            <FieldLabel htmlFor="signup-email">{t('auth.email')}</FieldLabel>
            <Input
              id="signup-email"
              type="email"
              autoComplete="email"
              {...register('email', { required: true })}
            />
            {errors.email ? (
              <p className="mt-1.5 text-xs text-rt-danger">
                {(errors.email.message as string) || t('auth.emailInvalid')}
              </p>
            ) : null}
          </div>
          <div>
            <FieldLabel htmlFor="signup-role">{t('auth.role')}</FieldLabel>
            <Select id="signup-role" {...register('role', { required: true })}>
              {SIGNUP_ROLE_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {t(o.labelKey)}
                </option>
              ))}
            </Select>
          </div>
          <div>
            <FieldLabel htmlFor="signup-password">{t('auth.password')}</FieldLabel>
            <Input
              id="signup-password"
              type="password"
              autoComplete="new-password"
              {...register('password', { required: true, minLength: 6 })}
            />
            {errors.password ? (
              <p className="mt-1.5 text-xs text-rt-danger">{t('auth.passwordMin')}</p>
            ) : null}
          </div>
          <div>
            <FieldLabel htmlFor="signup-confirm">{t('auth.confirmPassword')}</FieldLabel>
            <Input
              id="signup-confirm"
              type="password"
              autoComplete="new-password"
              {...register('confirmPassword', {
                required: true,
                validate: (v) => v === getValues('password') || t('auth.passwordMismatch'),
              })}
            />
            {errors.confirmPassword ? (
              <p className="mt-1.5 text-xs text-rt-danger">
                {(errors.confirmPassword.message as string) || t('auth.passwordMismatch')}
              </p>
            ) : null}
          </div>
          <Button type="submit" className="w-full" size="lg">
            {t('auth.signup')}
          </Button>
        </form>

        <p className="mt-6 text-center text-sm text-rt-neutral-mid">
          <Link className="font-medium text-rt-primary transition-colors hover:underline" to="/login">
            {t('auth.toLogin')}
          </Link>
        </p>
      </AuthCard>
    </AuthLayout>
  )
}
