<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useTokenRefresh } from '@/composables/useTokenRefresh'

const authStore = useAuthStore()

// Start token expiry checking
useTokenRefresh()
</script>

<template>
  <nav class="container-fluid">
    <ul>
      <li>
        <RouterLink to="/" class="contrast">
          <strong>LilPub</strong>
        </RouterLink>
      </li>
    </ul>
    <ul v-if="authStore.isAuthenticated">
      <li>
        <small>{{ authStore.user?.me }}</small>
      </li>
      <li>
        <a href="#" @click.prevent="authStore.logout()" class="secondary"> Sign out </a>
      </li>
    </ul>
  </nav>

  <RouterView />
</template>

<style>
nav ul:last-child {
  justify-content: flex-end;
}

nav a {
  text-decoration: none;
}
</style>
