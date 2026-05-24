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
  <main class="lp-shell flex min-h-screen items-center">
    <div class="mx-auto grid w-full max-w-5xl gap-8 lg:grid-cols-[minmax(0,1fr)_28rem] lg:items-center">
      <section class="text-center lg:text-left">
        <img
          :src="logoUrl"
          alt="LittlePublisher"
          class="mx-auto h-auto w-full max-w-[17.5rem] object-contain lg:mx-0 lg:max-w-sm"
          width="1254"
          height="1254"
        />
        <p class="lp-kicker mt-8">LittlePublisher</p>
        <h1 class="lp-heading mt-3 text-4xl leading-none sm:text-5xl">Welcome back</h1>
        <p class="lp-copy mx-auto mt-4 max-w-xl text-base lg:mx-0">
          Use the website configured for this LittlePublisher instance.
        </p>
      </section>

      <AppSurface as="section" padding="lg" class="w-full">
        <div class="mb-6">
          <p class="lp-kicker">Sign in</p>
          <h2 class="lp-heading mt-2 text-2xl">Continue to your dashboard</h2>
        </div>

        <div v-if="error" class="mb-4 rounded-md border border-lp-danger/20 bg-lp-danger-soft px-3 py-2 text-sm font-semibold text-lp-danger">
          {{ error }}
        </div>

        <AppButton full-width size="lg" :loading="isLoading" @click="handleLogin">
          {{ isLoading ? 'Redirecting...' : 'Log in' }}
        </AppButton>
      </AppSurface>
    </div>
  </main>
</template>
