import axios from 'axios'

// Separate client: renewal failures must never recurse through the API interceptor.
const session = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
  withCredentials: true,
  headers: { 'X-Refresh-Session': '1' },
  timeout: 15000,
})
let renewal: Promise<string> | null = null
let generation = 0

export function refreshSession(): Promise<string> {
  if (renewal) return renewal
  const started = generation
  renewal = session
    .post<{ token: string }>('/auth/refresh')
    .then(({ data }) => {
      if (started !== generation) throw new Error('Session was signed out.')
      localStorage.setItem('token', data.token)
      return data.token
    })
    .finally(() => {
      renewal = null
    })
  return renewal
}

export async function endSession() {
  generation++
  await session.post('/auth/logout')
}

export function tokenNeedsRefresh(token: string): boolean {
  try {
    const payload = JSON.parse(atob(token.split('.')[1]!.replace(/-/g, '+').replace(/_/g, '/')))
    return typeof payload.exp !== 'number' || payload.exp * 1000 - Date.now() < 5 * 60 * 1000
  } catch {
    return true
  }
}
