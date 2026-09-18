<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useDeskStore } from '@/stores/desk'
import { safeUrl } from '@/utils/contentTypes'
import AccountOperations from './AccountOperations.vue'
import bookMark from '@/assets/brand/littlepublisher-book-mark.png'
const emit = defineEmits<{ signOut: [] }>()
const auth = useAuthStore(),
  desk = useDeskStore(),
  route = useRoute()
const open = ref<'activity' | 'profile' | null>(null)
const root = ref<HTMLElement>(),
  activityButton = ref<HTMLButtonElement>(),
  profileButton = ref<HTMLButtonElement>()
const identity = computed(() => {
  try {
    return new URL(auth.user?.me || '').hostname
  } catch {
    return auth.user?.me || 'Your account'
  }
})
function toggle(menu: 'activity' | 'profile') {
  open.value = open.value === menu ? null : menu
  if (open.value === 'activity') void desk.refresh()
}
function outside(event: PointerEvent) {
  if (!root.value?.contains(event.target as Node)) open.value = null
}
function escape(event: KeyboardEvent) {
  if (event.key !== 'Escape' || !open.value) return
  ;(open.value === 'activity' ? activityButton.value : profileButton.value)?.focus()
  open.value = null
}
onMounted(() => {
  document.addEventListener('pointerdown', outside)
  document.addEventListener('keydown', escape)
})
onBeforeUnmount(() => {
  desk.reset()
  document.removeEventListener('pointerdown', outside)
  document.removeEventListener('keydown', escape)
})
watch(
  () => route.fullPath,
  () => {
    open.value = null
  },
)
</script>
<template>
  <header ref="root" class="writer-header">
    <div class="header-brand">
      <RouterLink to="/" class="writer-brand"
        ><img :src="bookMark" alt="" width="32" height="32" />LittlePublisher</RouterLink
      ><RouterLink class="header-webmentions" to="/webmentions">Webmentions</RouterLink>
    </div>
    <nav class="header-actions" aria-label="Account and activity">
      <div class="header-dropdown">
        <button
          ref="activityButton"
          :aria-expanded="open === 'activity'"
          aria-controls="activity-panel"
          @click="toggle('activity')"
        >
          <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M3 12h4l3-7 4 14 3-7h4" /></svg
          ><span>Activity</span><span aria-hidden="true">⌄</span>
        </button>
        <section
          v-if="open === 'activity'"
          id="activity-panel"
          class="header-panel"
          aria-label="Publishing activity"
        >
          <div class="menu-heading">
            <h2>Publishing activity</h2>
            <button :disabled="desk.loading" @click="desk.refresh">
              {{ desk.loading ? 'Refreshing…' : 'Refresh' }}
            </button>
          </div>
          <p v-if="desk.error" role="alert" class="writer-error menu-copy">{{ desk.error }}</p>
          <p v-if="!desk.loading && !desk.error && !desk.dashboard?.jobs.length" class="menu-copy">
            No publishing activity yet.
          </p>
          <article v-for="job in desk.dashboard?.jobs" :key="job.id" class="activity-item">
            <div>
              <strong>{{ job.action }}</strong
              ><span :class="{ 'writer-error': job.status === 'failed' }">{{ job.status }}</span>
            </div>
            <p>{{ job.clientId || job.userMe }}</p>
            <a
              v-if="safeUrl(job.publishedUrl)"
              :href="safeUrl(job.publishedUrl)"
              target="_blank"
              rel="noopener"
              >View published post ↗</a
            >
            <p v-if="job.error" class="writer-error">{{ job.error }}</p>
            <time>{{ new Date(job.updatedUtc).toLocaleString() }}</time>
          </article>
        </section>
      </div>
      <div class="header-dropdown">
        <button
          ref="profileButton"
          aria-label="Profile and settings"
          :aria-expanded="open === 'profile'"
          aria-controls="profile-panel"
          @click="toggle('profile')"
        >
          <span class="profile-avatar">{{ identity.slice(0, 2).toUpperCase() }}</span
          ><span aria-hidden="true">⌄</span>
        </button>
        <section
          v-show="open === 'profile'"
          id="profile-panel"
          class="header-panel"
          aria-label="Profile and settings"
        >
          <div class="account-identity">
            <strong>{{ identity }}</strong>
            <p>{{ auth.user?.me }}</p>
          </div>
          <RouterLink class="account-link" to="/settings"
            >Site configuration <span>→</span></RouterLink
          ><a
            v-if="safeUrl(auth.user?.me)"
            class="account-link"
            :href="safeUrl(auth.user?.me)"
            target="_blank"
            rel="noopener"
            >Visit your site <span>↗</span></a
          ><RouterLink class="account-link mobile-webmentions" to="/webmentions"
            >Webmentions <span>→</span></RouterLink
          >
          <AccountOperations /><button class="account-link signout" @click="emit('signOut')">
            Sign out <span>→</span>
          </button>
        </section>
      </div>
    </nav>
  </header>
</template>
