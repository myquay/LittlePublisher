<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { RouterLink, RouterView, useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useTokenRefresh } from '@/composables/useTokenRefresh'
import { setupService } from '@/services/setupService'
import { AppButton, AppSurface, LoadingState } from '@/components'
import SetupView from '@/views/SetupView.vue'
import bookMarkUrl from '@/assets/brand/littlepublisher-book-mark.png'
import type { SetupStatus } from '@/types/setup'

const authStore = useAuthStore()
const route = useRoute()
const router = useRouter()
const publicSetupStatus = ref<SetupStatus | null>(null)
const fullSetupStatus = ref<SetupStatus | null>(null)
const isSetupLoading = ref(true)
const isFullSetupLoading = ref(false)
const setupError = ref('')
const setupStatus = computed(() => {
  return authStore.isAuthenticated && fullSetupStatus.value ? fullSetupStatus.value : publicSetupStatus.value
})
const isPublicAuthRoute = computed(() => route.meta.guest === true || route.name === 'callback')
const isAppLoading = computed(() => !isPublicAuthRoute.value && (isSetupLoading.value || !authStore.isInitialized))
const showSetupError = computed(() => !isPublicAuthRoute.value && !!setupError.value)
const showHeader = computed(() => {
  return route.meta.requiresAuth === true && authStore.isAuthenticated && setupStatus.value?.ready === true
})

// Start token expiry checking
useTokenRefresh()

onMounted(async () => {
  try {
    publicSetupStatus.value = await setupService.getPublicStatus()
  } catch {
    setupError.value = 'Setup status could not be loaded.'
  } finally {
    isSetupLoading.value = false
  }
})

watch(
  () => [authStore.isInitialized, authStore.isAuthenticated] as const,
  async ([isInitialized, isAuthenticated]) => {
    if (!isInitialized || !isAuthenticated) {
      fullSetupStatus.value = null
      return
    }

    isFullSetupLoading.value = true
    setupError.value = ''

    try {
      fullSetupStatus.value = await setupService.getStatus()
    } catch {
      setupError.value = 'Setup status could not be loaded.'
    } finally {
      isFullSetupLoading.value = false
    }
  },
  { immediate: true },
)

function signOut() {
  authStore.logout()
  router.push('/login')
}
</script>

<template>
  <div class="lp-page">
    <header v-if="showHeader" class="border-b border-lp-border bg-lp-surface/90">
      <nav class="mx-auto flex max-w-7xl items-center justify-between px-4 py-3 sm:px-6">
        <div class="flex items-center gap-5">
          <RouterLink to="/" class="flex items-center gap-2 text-base font-black text-lp-ink">
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
          <span class="hidden max-w-72 truncate text-sm text-lp-muted sm:inline">
            {{ authStore.user?.me }}
          </span>
          <AppButton variant="secondary" size="sm" @click="signOut">
            Sign out
          </AppButton>
        </div>
      </nav>
    </header>

    <main
      v-if="isAppLoading"
      class="mx-auto flex min-h-screen max-w-7xl items-center px-4 py-8 sm:px-6"
    >
      <AppSurface>
        <LoadingState label="Checking setup..." />
      </AppSurface>
    </main>

    <main
      v-else-if="showSetupError"
      class="mx-auto flex min-h-screen max-w-7xl items-center px-4 py-8 sm:px-6"
    >
      <AppSurface class="border-lp-danger/20 bg-lp-danger-soft text-sm font-semibold text-lp-danger">
        {{ setupError }}
      </AppSurface>
    </main>

    <main
      v-else-if="!isPublicAuthRoute && isFullSetupLoading"
      class="mx-auto flex min-h-screen max-w-7xl items-center px-4 py-8 sm:px-6"
    >
      <AppSurface>
        <LoadingState label="Checking setup..." />
      </AppSurface>
    </main>

    <SetupView
      v-else-if="setupStatus && !setupStatus.ready"
      :initial-status="setupStatus"
      :public-mode="!authStore.isAuthenticated"
    />

    <RouterView v-else />
  </div>
</template>
