import { onMounted, onUnmounted } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { refreshSession, tokenNeedsRefresh } from '@/services/session'

export function useTokenRefresh() {
  const authStore = useAuthStore()
  let refreshInterval: number | undefined

  async function checkTokenExpiry() {
    const token = localStorage.getItem('token')
    if (!token || !tokenNeedsRefresh(token)) return
    try {
      authStore.setToken(await refreshSession())
    } catch {
      // Offline or expired renewal session: keep the editor and its unsaved work open.
      // Saving can retry renewal, or the user can sign in in another tab.
    }
  }

  onMounted(() => {
    refreshInterval = window.setInterval(checkTokenExpiry, 60000)
    window.addEventListener('focus', checkTokenExpiry)
    void checkTokenExpiry()
  })
  onUnmounted(() => {
    clearInterval(refreshInterval)
    window.removeEventListener('focus', checkTokenExpiry)
  })
}
