<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useRouter } from 'vue-router'
import { adminService } from '@/services/adminService'
import type { AdminCheck, AdminDashboard } from '@/types/admin'

const authStore = useAuthStore()
const router = useRouter()
const dashboard = ref<AdminDashboard | null>(null)
const loadingDashboard = ref(true)
const dashboardError = ref<string | null>(null)
const storageCheck = ref<AdminCheck | null>(null)
const githubCheck = ref<AdminCheck | null>(null)
const checkingStorage = ref(false)
const checkingGitHub = ref(false)

const endpointUrl = computed(() => `${window.location.origin}/micropub`)

onMounted(async () => {
  await loadDashboard()
})

function handleLogout() {
  authStore.logout()
  router.push('/login')
}

async function loadDashboard() {
  loadingDashboard.value = true
  dashboardError.value = null

  try {
    dashboard.value = await adminService.getDashboard()
  } catch (error) {
    dashboardError.value = readError(error)
  } finally {
    loadingDashboard.value = false
  }
}

async function runStorageCheck() {
  checkingStorage.value = true
  storageCheck.value = null

  try {
    storageCheck.value = await adminService.checkStorage()
  } catch (error) {
    storageCheck.value = { ok: false, message: readError(error) }
  } finally {
    checkingStorage.value = false
  }
}

async function runGitHubCheck() {
  checkingGitHub.value = true
  githubCheck.value = null

  try {
    githubCheck.value = await adminService.checkGitHub()
  } catch (error) {
    githubCheck.value = { ok: false, message: readError(error) }
  } finally {
    checkingGitHub.value = false
  }
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value))
}

function statusClass(status: string) {
  if (status === 'succeeded') return 'border-emerald-200 bg-emerald-50 text-emerald-700'
  if (status === 'failed') return 'border-rose-200 bg-rose-50 text-rose-700'
  return 'border-amber-200 bg-amber-50 text-amber-700'
}

function readError(error: unknown) {
  if (
    error &&
    typeof error === 'object' &&
    'response' in error &&
    error.response &&
    typeof error.response === 'object' &&
    'data' in error.response
  ) {
    const data = error.response.data as { message?: string }
    if (data.message) return data.message
  }

  return 'The admin API could not be reached.'
}
</script>

<template>
  <main class="mx-auto max-w-7xl px-4 py-8 sm:px-6">
    <div class="mb-8 flex flex-col gap-4 border-b border-slate-200 pb-6 sm:flex-row sm:items-end sm:justify-between">
      <div>
        <p class="text-sm font-medium uppercase tracking-wide text-slate-500">Micropub endpoint</p>
        <h1 class="mt-2 text-3xl font-semibold text-slate-950">Welcome back</h1>
        <p class="mt-2 break-all text-sm text-slate-600">{{ endpointUrl }}</p>
      </div>

      <button
        type="button"
        class="w-fit rounded-md border border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 shadow-sm hover:bg-slate-100"
        @click="handleLogout"
      >
        Sign out
      </button>
    </div>

    <div v-if="authStore.user" class="grid gap-6 lg:grid-cols-[minmax(0,1fr)_22rem]">
      <section class="space-y-6">
        <div class="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
          <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <h2 class="text-lg font-semibold text-slate-950">Publishing activity</h2>
              <p class="mt-1 text-sm text-slate-500">Recent Micropub jobs and generated Hugo files.</p>
            </div>
            <button
              type="button"
              class="w-fit rounded-md border border-slate-300 bg-white px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100"
              @click="loadDashboard"
            >
              Refresh
            </button>
          </div>

          <div v-if="loadingDashboard" class="mt-6 flex items-center gap-3 text-slate-600">
            <div class="h-4 w-4 animate-spin rounded-full border-2 border-slate-300 border-t-slate-800"></div>
            <p>Loading activity...</p>
          </div>

          <p v-else-if="dashboardError" class="mt-6 rounded-md border border-rose-200 bg-rose-50 p-3 text-sm text-rose-700">
            {{ dashboardError }}
          </p>

          <div v-else class="mt-6 space-y-4">
            <div v-if="!dashboard?.jobs.length" class="rounded-md border border-dashed border-slate-300 p-5 text-sm text-slate-500">
              No publish jobs yet.
            </div>

            <article
              v-for="job in dashboard?.jobs"
              :key="job.id"
              class="rounded-md border border-slate-200 p-4"
            >
              <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div class="min-w-0">
                  <p class="text-sm font-medium text-slate-950">{{ job.action }}</p>
                  <p class="mt-1 truncate text-sm text-slate-500">{{ job.clientId || job.userMe }}</p>
                </div>
                <span class="w-fit rounded-full border px-2.5 py-1 text-xs font-medium" :class="statusClass(job.status)">
                  {{ job.status }}
                </span>
              </div>

              <a
                v-if="job.publishedUrl"
                :href="job.publishedUrl"
                class="mt-3 block break-all text-sm font-medium text-sky-700 hover:text-sky-900"
              >
                {{ job.publishedUrl }}
              </a>
              <p v-if="job.error" class="mt-3 text-sm text-rose-700">{{ job.error }}</p>
              <p class="mt-3 text-xs text-slate-500">{{ formatDate(job.updatedUtc) }}</p>
            </article>
          </div>
        </div>

        <div class="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
          <h2 class="text-lg font-semibold text-slate-950">Published items</h2>

          <div v-if="!loadingDashboard && !dashboard?.items.length" class="mt-5 rounded-md border border-dashed border-slate-300 p-5 text-sm text-slate-500">
            No published items have been recorded.
          </div>

          <div v-else class="mt-5 divide-y divide-slate-200">
            <article v-for="item in dashboard?.items" :key="item.id" class="py-4 first:pt-0 last:pb-0">
              <a :href="item.url" class="break-all text-sm font-semibold text-slate-950 hover:text-sky-800">
                {{ item.title || item.url }}
              </a>
              <p class="mt-1 break-all text-xs text-slate-500">{{ item.filePath }}</p>
              <p class="mt-2 text-sm text-slate-600">{{ formatDate(item.publishedUtc) }}</p>
            </article>
          </div>
        </div>
      </section>

      <aside class="space-y-6">
        <section class="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
          <h2 class="text-lg font-semibold text-slate-950">Signed in</h2>
          <p class="mt-3 break-all text-sm text-slate-600">{{ authStore.user.me }}</p>
        </section>

        <section class="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
          <h2 class="text-lg font-semibold text-slate-950">Health checks</h2>
          <div class="mt-5 space-y-4">
            <div>
              <button
                type="button"
                class="w-full rounded-md bg-slate-950 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:cursor-wait disabled:bg-slate-500"
                :disabled="checkingStorage"
                @click="runStorageCheck"
              >
                {{ checkingStorage ? 'Checking storage...' : 'Check storage' }}
              </button>
              <p v-if="storageCheck" class="mt-2 text-sm" :class="storageCheck.ok ? 'text-emerald-700' : 'text-rose-700'">
                {{ storageCheck.message }}
              </p>
            </div>

            <div>
              <button
                type="button"
                class="w-full rounded-md bg-slate-950 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:cursor-wait disabled:bg-slate-500"
                :disabled="checkingGitHub"
                @click="runGitHubCheck"
              >
                {{ checkingGitHub ? 'Checking GitHub...' : 'Check GitHub' }}
              </button>
              <p v-if="githubCheck" class="mt-2 text-sm" :class="githubCheck.ok ? 'text-emerald-700' : 'text-rose-700'">
                {{ githubCheck.message }}
              </p>
            </div>
          </div>
        </section>
      </aside>
    </div>

    <div v-else class="flex items-center gap-3 text-slate-600">
      <div class="h-4 w-4 animate-spin rounded-full border-2 border-slate-300 border-t-slate-800"></div>
      <p>Loading...</p>
    </div>
  </main>
</template>
