import { onMounted, onUnmounted } from 'vue'
import { useAuthStore } from '@/stores/auth'

export function useTokenRefresh() {
  const authStore = useAuthStore()
  let refreshInterval: number | null = null

  function parseJwt(token: string) {
    try {
      const parts = token.split('.')
      if (parts.length !== 3) return null

      const base64Url = parts[1]
      if (!base64Url) return null

      const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/')
      const jsonPayload = decodeURIComponent(
        atob(base64)
          .split('')
          .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
          .join(''),
      )
      return JSON.parse(jsonPayload)
    } catch {
      return null
    }
  }

  function checkTokenExpiry() {
    if (!authStore.token) return

    const payload = parseJwt(authStore.token)
    if (!payload?.exp) return

    const expiresAt = payload.exp * 1000
    const now = Date.now()
    const fiveMinutes = 5 * 60 * 1000

    // If token expires in less than 5 minutes, logout
    // (In a full implementation, you'd refresh the token here)
    if (expiresAt - now < fiveMinutes) {
      authStore.logout()
    }
  }

  onMounted(() => {
    // Check every minute
    refreshInterval = window.setInterval(checkTokenExpiry, 60000)
    checkTokenExpiry()
  })

  onUnmounted(() => {
    if (refreshInterval) {
      clearInterval(refreshInterval)
    }
  })
}
