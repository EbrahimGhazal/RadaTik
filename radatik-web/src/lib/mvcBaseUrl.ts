/** When developing SPA on Vite, set VITE_MVC_ORIGIN (e.g. https://localhost:7098). Otherwise same origin as the document. */
export function mvcBaseUrl(): string {
  const o = import.meta.env.VITE_MVC_ORIGIN
  if (typeof o === 'string' && o.trim().length > 0) {
    return o.replace(/\/$/, '')
  }
  if (typeof window !== 'undefined') {
    return window.location.origin
  }
  return ''
}
