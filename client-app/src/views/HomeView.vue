<script setup lang="ts">
import { useAuthStore } from '@/stores/auth'
import { useRouter } from 'vue-router'

const authStore = useAuthStore()
const router = useRouter()

function handleLogout() {
  authStore.logout()
  router.push('/login')
}
</script>

<template>
  <main class="mx-auto max-w-6xl px-4 py-8 sm:px-6">
    <section class="rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
      <div class="mb-6">
        <p class="text-sm font-medium uppercase tracking-wide text-slate-500">Micropub endpoint</p>
        <h1 class="mt-2 text-2xl font-semibold text-slate-950">Welcome to LilPub</h1>
      </div>

      <div v-if="authStore.user" class="space-y-4">
        <div class="rounded-md border border-slate-200 bg-slate-50 p-4">
          <p class="text-sm text-slate-500">Signed in as</p>
          <p class="mt-1 break-all font-medium text-slate-900">{{ authStore.user.me }}</p>
        </div>

        <p class="text-slate-600">
          LittlePublisher is being configured as a Micropub endpoint for your website.
        </p>

        <button
          type="button"
          class="rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 shadow-sm hover:bg-slate-100"
          @click="handleLogout"
        >
          Sign out
        </button>
      </div>

      <div v-else class="flex items-center gap-3 text-slate-600">
        <div class="h-4 w-4 animate-spin rounded-full border-2 border-slate-300 border-t-slate-800"></div>
        <p>Loading...</p>
      </div>
    </section>
  </main>
</template>
