<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'

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
  <main class="mx-auto flex min-h-[calc(100vh-57px)] max-w-6xl items-center px-4 py-8 sm:px-6">
    <section class="w-full max-w-lg rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
      <div class="mb-6">
        <h1 class="text-2xl font-semibold text-slate-950">Welcome back</h1>
        <p class="mt-2 text-sm text-slate-600">
          Sign in with the website configured for this LittlePublisher instance.
        </p>
      </div>

      <div class="space-y-4">
        <div v-if="error" class="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {{ error }}
        </div>

        <button
          type="button"
          class="inline-flex w-full items-center justify-center rounded-md bg-slate-950 px-4 py-2.5 text-sm font-semibold text-white shadow-sm hover:bg-slate-800 disabled:cursor-not-allowed disabled:bg-slate-400"
          :disabled="isLoading"
          @click="handleLogin"
        >
          {{ isLoading ? 'Redirecting...' : 'Log in' }}
        </button>
      </div>
    </section>
  </main>
</template>
