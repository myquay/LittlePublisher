<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'

const route = useRoute()
const me = ref('')
const error = ref('')
const isLoading = ref(false)

onMounted(() => {
  // Check for error from redirect
  const errorParam = route.query.error as string
  if (errorParam) {
    error.value = decodeURIComponent(errorParam.replace(/\+/g, ' '))
  }
})

function handleSubmit() {
  error.value = ''

  if (!me.value) {
    error.value = 'Please enter your domain'
    return
  }

  isLoading.value = true

  // Redirect browser to login endpoint - this will trigger the IndieAuth flow
  const loginUrl = `/api/auth/login?me=${encodeURIComponent(me.value)}`
  window.location.href = loginUrl
}
</script>

<template>
  <main class="container">
    <article>
      <header>
        <hgroup>
          <h2>LilPub</h2>
          <p>Self-hosted publishing platform for your personal website</p>
        </hgroup>
      </header>

      <div>
        <p><small>Enter the domain of your IndieAuth enabled website to continue.</small></p>

        <form @submit.prevent="handleSubmit">
          <div v-if="error" class="error">{{ error }}</div>

          <input
            v-model="me"
            type="text"
            placeholder="https://your-domain.com"
            aria-label="Domain"
            autocomplete="url"
            required
            :disabled="isLoading"
          />

          <button type="submit" class="contrast" :disabled="isLoading">
            {{ isLoading ? 'Redirecting...' : 'Sign in with IndieAuth' }}
          </button>
        </form>
      </div>
    </article>
  </main>
</template>

<style scoped>
.error {
  color: var(--pico-del-color);
  margin-bottom: 1rem;
  padding: 0.5rem;
  background: var(--pico-del-color);
  background-opacity: 0.1;
  border-radius: var(--pico-border-radius);
}
</style>
