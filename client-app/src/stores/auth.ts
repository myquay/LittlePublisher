import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { authService } from '@/services/authService'
import type { User } from '@/types/auth'

export const useAuthStore = defineStore('auth', () => {
  // State
  const user = ref<User | null>(null)
  const token = ref<string | null>(localStorage.getItem('token'))
  const isLoading = ref(false)
  const isInitialized = ref(false)

  // Getters
  const isAuthenticated = computed(() => !!token.value && !!user.value)

  // Actions
  function setToken(newToken: string) {
    token.value = newToken
    localStorage.setItem('token', newToken)
  }

  async function fetchUser() {
    if (!token.value) {
      isInitialized.value = true
      return
    }

    isLoading.value = true
    try {
      user.value = await authService.getMe()
    } catch {
      logout()
    } finally {
      isLoading.value = false
      isInitialized.value = true
    }
  }

  function logout() {
    user.value = null
    token.value = null
    localStorage.removeItem('token')
  }

  // Initialize on store creation
  fetchUser()

  return {
    user,
    token,
    isLoading,
    isInitialized,
    isAuthenticated,
    setToken,
    fetchUser,
    logout,
  }
})
