<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { AppButton, AppSurface } from '@/components'
import logoUrl from '@/assets/brand/littlepublisher-logo.png'

const route = useRoute()
const error = ref('')
const isLoading = ref(false)

onMounted(() => {
  // Check for error from redirect
  const errorParam = route.query.error as string
  if (errorParam) {
    error.value = decodeURIComponent(errorParam.replace(/\+/g, ' '))
  }
})

function handleLogin() {
  error.value = ''
  isLoading.value = true

  // The API owns the configured IndieAuth "me" URL. The UI never supplies a domain.
  window.location.href = '/api/auth/login'
}
</script>

<template>
  <main class="lp-shell flex min-h-screen items-center py-12 sm:py-16">
    <div class="mx-auto w-full max-w-md">
      <section class="text-center">
        <img
          :src="logoUrl"
          alt="LittlePublisher"
          class="mx-auto h-auto w-full max-w-[13.5rem] object-contain sm:max-w-[15rem]"
          width="1254"
          height="1254"
        />
      </section>

      <AppSurface as="section" padding="lg" class="mt-8 w-full text-center sm:mt-10">
        <div class="mx-auto max-w-sm">
          <p class="lp-kicker">Welcome back</p>
          <h1 class="lp-heading mt-3 text-3xl sm:text-4xl">Sign in to LittlePublisher</h1>
          <p class="lp-copy mt-3 text-sm sm:text-base">
            Continue with the website configured for this LittlePublisher instance.
          </p>
        </div>

        <div v-if="error" class="mt-6 rounded-md border border-lp-danger/20 bg-lp-danger-soft px-3 py-2 text-left text-sm font-semibold text-lp-danger">
          {{ error }}
        </div>

        <AppButton full-width size="lg" class="mt-6" :loading="isLoading" @click="handleLogin">
          {{ isLoading ? 'Redirecting...' : 'Log in' }}
        </AppButton>
      </AppSurface>
    </div>
  </main>
</template>
