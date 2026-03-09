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
  <main class="container">
    <article>
      <header>
        <h2>Welcome to LilPub</h2>
      </header>

      <div v-if="authStore.user">
        <p>
          Signed in as: <strong>{{ authStore.user.me }}</strong>
        </p>
        <p>A MicroPub endpoint</p>

        <button @click="handleLogout" class="secondary">Sign out</button>
      </div>

      <div v-else>
        <p aria-busy="true">Loading...</p>
      </div>
    </article>
  </main>
</template>
