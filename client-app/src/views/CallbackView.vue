<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const route = useRoute()
const authStore = useAuthStore()
const error = ref('')
const status = ref('Processing authentication...')

onMounted(async () => {
  // Token might be in query string or hash
  const token =
    (route.query.token as string) ||
    new URLSearchParams(window.location.hash.slice(1)).get('token')

  const errorParam = route.query.error as string

  if (errorParam) {
    error.value = decodeURIComponent(errorParam)
    return
  }

  if (token) {
    status.value = 'Verifying token...'

    try {
      authStore.setToken(token)
      await authStore.fetchUser()

      status.value = 'Success! Redirecting...'

      // Clean up URL and redirect
      router.replace('/')
    } catch {
      error.value = 'Failed to verify authentication. Please try again.'
      authStore.logout()
    }
  } else {
    error.value = 'No authentication token received.'
  }
})

function goToLogin() {
  router.push('/login')
}
</script>

<template>
  <main class="mx-auto flex min-h-[calc(100vh-57px)] max-w-6xl items-center px-4 py-8 sm:px-6">
    <section class="w-full max-w-lg rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
      <div v-if="error" class="space-y-4">
        <div>
          <h1 class="text-xl font-semibold text-slate-950">Authentication failed</h1>
          <p class="mt-2 text-sm text-red-700">{{ error }}</p>
        </div>
        <button
          type="button"
          class="rounded-md bg-slate-950 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-slate-800"
          @click="goToLogin"
        >
          Try again
        </button>
      </div>

      <div v-else class="flex items-center gap-3 text-slate-600">
        <div class="h-4 w-4 animate-spin rounded-full border-2 border-slate-300 border-t-slate-800"></div>
        <p>{{ status }}</p>
      </div>
    </section>
  </main>
</template>
