import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import en from './locales/en.json'
import ar from './locales/ar.json'

function readPersistedLang(): 'en' | 'ar' {
  try {
    const raw = localStorage.getItem('radtik-ui')
    if (!raw) return 'en'
    const lang = JSON.parse(raw) as { state?: { lang?: string } }
    return lang.state?.lang === 'ar' ? 'ar' : 'en'
  } catch {
    return 'en'
  }
}

void i18n.use(initReactI18next).init({
  resources: {
    en: { translation: en },
    ar: { translation: ar },
  },
  lng: readPersistedLang(),
  fallbackLng: 'en',
  interpolation: { escapeValue: false },
})

export default i18n
