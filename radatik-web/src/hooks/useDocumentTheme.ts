import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import type { Lang, ThemeMode } from '../store/uiStore'

/** Applies theme class and RTL/LTR + language on <html>. */
export function useDocumentTheme(theme: ThemeMode, lang: Lang) {
  const { i18n } = useTranslation()

  useEffect(() => {
    const root = document.documentElement
    root.classList.toggle('dark', theme === 'dark')
    root.lang = lang
    root.dir = lang === 'ar' ? 'rtl' : 'ltr'
    document.body.classList.toggle('font-ar', lang === 'ar')
    void i18n.changeLanguage(lang)
  }, [theme, lang, i18n])
}
