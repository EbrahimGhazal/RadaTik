import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from './authStore'

describe('authStore', () => {
  beforeEach(() => {
    useAuthStore.setState({ user: null, isHydrating: false })
  })

  it('stores and clears user state', () => {
    const login = useAuthStore.getState().login
    const logout = useAuthStore.getState().logout

    login({
      id: '1',
      email: 'user@example.com',
      fullName: 'User',
      role: 'system_admin',
    })

    expect(useAuthStore.getState().user?.email).toBe('user@example.com')
    logout()
    expect(useAuthStore.getState().user).toBeNull()
  })
})
