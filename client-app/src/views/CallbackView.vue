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
  <main class="container">
    <article>
      <div v-if="error">
        <h3>Authentication Failed</h3>
        <p class="error">{{ error }}</p>
        <button @click="goToLogin" class="contrast">Try Again</button>
      </div>

      <div v-else>
        <p aria-busy="true">{{ status }}</p>
      </div>
    </article>
  </main>
</template>

<style scoped>
.error {
  color: var(--pico-del-color);
  margin-bottom: 1rem;
}
</style>
