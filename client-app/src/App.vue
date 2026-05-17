<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { RouterLink, RouterView, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useTokenRefresh } from '@/composables/useTokenRefresh'
import { setupService } from '@/services/setupService'
import SetupView from '@/views/SetupView.vue'
import bookMarkUrl from '@/assets/brand/littlepublisher-book-mark.png'
import type { SetupStatus } from '@/types/setup'

const authStore = useAuthStore()
const router = useRouter()
const setupStatus = ref<SetupStatus | null>(null)
const isSetupLoading = ref(true)
const setupError = ref('')

// Start token expiry checking
useTokenRefresh()

onMounted(async () => {
  try {
    setupStatus.value = await setupService.getStatus()
  } catch {
    setupError.value = 'Setup status could not be loaded.'
  } finally {
    isSetupLoading.value = false
  }
})

function signOut() {
  authStore.logout()
  router.push('/login')
}
</script>

<template>
  <div class="min-h-screen bg-slate-50 text-slate-950">
    <header v-if="!setupStatus || setupStatus.ready" class="border-b border-slate-200 bg-white">
      <nav class="mx-auto flex max-w-6xl items-center justify-between px-4 py-3 sm:px-6">
        <div class="flex items-center gap-5">
          <RouterLink to="/" class="flex items-center gap-2 text-base font-semibold text-slate-950">
            <img
              :src="bookMarkUrl"
              alt=""
              class="h-9 w-9 shrink-0 object-contain"
              width="36"
              height="36"
              aria-hidden="true"
            />
            <span>LittlePublisher</span>
          </RouterLink>
        </div>

        <div v-if="authStore.isAuthenticated" class="flex items-center gap-3">
          <span class="hidden max-w-72 truncate text-sm text-slate-500 sm:inline">
            {{ authStore.user?.me }}
          </span>
          <button
            type="button"
            class="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm font-medium text-slate-700 shadow-sm hover:bg-slate-100"
            @click="signOut"
          >
            Sign out
          </button>
        </div>
      </nav>
    </header>

    <main
      v-if="isSetupLoading"
      class="mx-auto flex min-h-[calc(100vh-57px)] max-w-6xl items-center px-4 py-8 sm:px-6"
    >
      <section class="flex items-center gap-3 rounded-lg border border-slate-200 bg-white p-6 text-slate-600 shadow-sm">
        <div class="h-4 w-4 animate-spin rounded-full border-2 border-slate-300 border-t-slate-800"></div>
        <p>Checking setup...</p>
      </section>
    </main>

    <main
      v-else-if="setupError"
      class="mx-auto flex min-h-[calc(100vh-57px)] max-w-6xl items-center px-4 py-8 sm:px-6"
    >
      <section class="rounded-lg border border-red-200 bg-red-50 p-6 text-sm text-red-700 shadow-sm">
        {{ setupError }}
      </section>
    </main>

    <SetupView v-else-if="setupStatus && !setupStatus.ready" :initial-status="setupStatus" />

    <RouterView v-else />
  </div>
</template>
