import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { AuthUser } from '../types'
import { getCurrentSpaUser } from '../lib/spaAuthApi'

interface AuthState {
  user: AuthUser | null
  isHydrating: boolean
  login: (user: AuthUser) => void
  logout: () => void
  hydrateFromServer: () => Promise<void>
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      isHydrating: false,
      login: (user) => set({ user }),
      logout: () => set({ user: null }),
      hydrateFromServer: async () => {
        set({ isHydrating: true })
        const user = await getCurrentSpaUser()
        set({ user, isHydrating: false })
      },
    }),
    { name: 'radtik-auth' },
  ),
)
