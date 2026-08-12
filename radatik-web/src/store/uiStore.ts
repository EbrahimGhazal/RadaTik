import { create } from 'zustand'
import { persist } from 'zustand/middleware'

export type ThemeMode = 'light' | 'dark'
export type Lang = 'en' | 'ar'

interface UiState {
  theme: ThemeMode
  lang: Lang
  sidebarOpen: boolean
  setTheme: (t: ThemeMode) => void
  setLang: (l: Lang) => void
  toggleSidebar: () => void
  setSidebarOpen: (v: boolean) => void
}

export const useUiStore = create<UiState>()(
  persist(
    (set) => ({
      theme: 'light',
      lang: 'en',
      sidebarOpen: false,
      setTheme: (theme) => set({ theme }),
      setLang: (lang) => set({ lang }),
      toggleSidebar: () => set((s) => ({ sidebarOpen: !s.sidebarOpen })),
      setSidebarOpen: (sidebarOpen) => set({ sidebarOpen }),
    }),
    {
      name: 'radatik-ui',
      partialize: (s) => ({ theme: s.theme, lang: s.lang }),
    },
  ),
)
