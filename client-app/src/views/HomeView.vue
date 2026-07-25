<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { AppButton, AppSurface, EmptyState, LoadingState, StatusBadge } from '@/components'
import { adminService } from '@/services/adminService'
import type { AdminCheck, AdminDashboard, ImportRepositoryResult } from '@/types/admin'

const authStore = useAuthStore()
const dashboard = ref<AdminDashboard | null>(null)
const loadingDashboard = ref(true)
const dashboardError = ref<string | null>(null)
const storageCheck = ref<AdminCheck | null>(null)
const githubCheck = ref<AdminCheck | null>(null)
const checkingStorage = ref(false)
const checkingGitHub = ref(false)
const syncingRepository = ref(false)
const syncResult = ref<ImportRepositoryResult | null>(null)
const syncError = ref<string | null>(null)

onMounted(async () => {
  await loadDashboard()
})

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

async function syncRepository(dryRun: boolean) {
  syncingRepository.value = true
  syncResult.value = null
  syncError.value = null

  try {
    syncResult.value = await adminService.importRepository({
      dryRun,
      overwrite: false,
    })

    if (!dryRun) {
      await loadDashboard()
    }
  } catch (error) {
    syncError.value = readError(error)
  } finally {
    syncingRepository.value = false
  }
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value))
}

function statusTone(status: string) {
  if (status === 'succeeded') return 'success'
  if (status === 'failed') return 'danger'
  return 'warning'
}

function checkTone(check: AdminCheck | null) {
  if (!check) return 'neutral'
  return check.ok ? 'success' : 'danger'
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
  <main class="lp-shell">
    <div v-if="authStore.user" class="grid gap-6 lg:grid-cols-[minmax(0,1fr)_22rem]">
      <section class="space-y-6">
        <AppSurface padding="lg">
          <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <p class="lp-kicker">Dashboard</p>
              <h1 class="lp-heading mt-2 text-3xl">Publishing activity</h1>
              <p class="lp-copy mt-2 text-sm">Azure-backed drafts, unpublished changes, and publication activity.</p>
            </div>
            <AppButton variant="secondary" :loading="loadingDashboard" @click="loadDashboard">
              Refresh
            </AppButton>
          </div>

          <LoadingState v-if="loadingDashboard" class="mt-6" label="Loading activity..." />

          <p v-else-if="dashboardError" class="mt-6 rounded-md border border-lp-danger/20 bg-lp-danger-soft p-3 text-sm font-semibold text-lp-danger">
            {{ dashboardError }}
          </p>

          <div v-else class="mt-6 space-y-4">
            <EmptyState
              v-if="!dashboard?.jobs.length"
              title="No publish jobs yet."
              description="New Micropub requests will appear here after LittlePublisher starts receiving posts."
            />

            <article
              v-for="job in dashboard?.jobs"
              :key="job.id"
              class="rounded-md border border-lp-border bg-lp-surface-soft p-4"
            >
              <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div class="min-w-0">
                  <p class="text-sm font-black text-lp-ink">{{ job.action }}</p>
                  <p class="mt-1 truncate text-sm text-lp-muted">{{ job.clientId || job.userMe }}</p>
                </div>
                <StatusBadge :tone="statusTone(job.status)">
                  {{ job.status }}
                </StatusBadge>
              </div>

              <a
                v-if="job.publishedUrl"
                :href="job.publishedUrl"
                class="mt-3 block break-all text-sm font-bold text-lp-info hover:text-lp-ink"
              >
                {{ job.publishedUrl }}
              </a>
              <p v-if="job.error" class="mt-3 text-sm font-semibold text-lp-danger">{{ job.error }}</p>
              <p class="mt-3 text-xs font-semibold text-lp-subtle">{{ formatDate(job.updatedUtc) }}</p>
            </article>
          </div>
        </AppSurface>

        <AppSurface padding="lg">
          <div>
            <p class="lp-kicker">Posts</p>
            <h2 class="lp-heading mt-2 text-2xl">Writing desk</h2>
          </div>

          <EmptyState
            v-if="!loadingDashboard && !dashboard?.posts.length"
            class="mt-5"
            title="No posts have been stored."
            description="Create a draft or import the existing repository to establish the Azure post library."
          />

          <div v-else-if="dashboard?.posts.length" class="mt-5 divide-y divide-lp-border">
            <article v-for="post in dashboard?.posts" :key="post.id" class="py-4 first:pt-0 last:pb-0">
              <RouterLink :to="`/posts/${post.id}`" class="break-all text-sm font-black text-lp-ink hover:text-lp-info">
                {{ post.title || post.slug || 'Untitled note' }}
              </RouterLink>
              <p class="mt-1 break-all text-xs font-semibold text-lp-subtle">{{ post.filePath || 'Azure only' }}</p>
              <div class="mt-2 flex flex-wrap items-center gap-2">
                <StatusBadge v-if="post.state === 'publish-failed'" tone="danger">
                  Publish failed
                </StatusBadge>
                <StatusBadge v-else-if="post.hasUnpublishedChanges && post.publishedRevision" tone="warning">
                  Unpublished changes
                </StatusBadge>
                <StatusBadge v-else-if="!post.publishedRevision" tone="warning">
                  Draft
                </StatusBadge>
                <p v-else-if="post.publishedUtc" class="text-sm text-lp-muted">{{ formatDate(post.publishedUtc) }}</p>
              </div>
            </article>
          </div>
        </AppSurface>
      </section>

      <aside class="space-y-6">
        <AppSurface>
          <p class="lp-kicker">Identity</p>
          <h2 class="lp-heading mt-2 text-xl">Signed in</h2>
          <p class="mt-3 break-all text-sm text-lp-muted">{{ authStore.user.me }}</p>
        </AppSurface>

        <AppSurface>
          <div class="flex items-start justify-between gap-3">
            <div>
              <p class="lp-kicker">Operations</p>
              <h2 class="lp-heading mt-2 text-xl">Health checks</h2>
            </div>
          </div>
          <div class="mt-5 space-y-4">
            <div class="rounded-md border border-lp-border bg-lp-surface-soft p-3">
              <div class="flex items-center justify-between gap-3">
                <span class="text-sm font-black text-lp-ink">Storage</span>
                <StatusBadge :tone="checkTone(storageCheck)">
                  {{ storageCheck ? (storageCheck.ok ? 'OK' : 'Issue') : 'Unchecked' }}
                </StatusBadge>
              </div>
              <AppButton class="mt-3" full-width :loading="checkingStorage" @click="runStorageCheck">
                {{ checkingStorage ? 'Checking storage...' : 'Check storage' }}
              </AppButton>
              <p
                v-if="storageCheck"
                class="mt-2 text-sm font-semibold"
                :class="storageCheck.ok ? 'text-lp-success' : 'text-lp-danger'"
              >
                {{ storageCheck.message }}
              </p>
            </div>

            <div class="rounded-md border border-lp-border bg-lp-surface-soft p-3">
              <div class="flex items-center justify-between gap-3">
                <span class="text-sm font-black text-lp-ink">GitHub</span>
                <StatusBadge :tone="checkTone(githubCheck)">
                  {{ githubCheck ? (githubCheck.ok ? 'OK' : 'Issue') : 'Unchecked' }}
                </StatusBadge>
              </div>
              <AppButton class="mt-3" full-width :loading="checkingGitHub" @click="runGitHubCheck">
                {{ checkingGitHub ? 'Checking GitHub...' : 'Check GitHub' }}
              </AppButton>
              <p
                v-if="githubCheck"
                class="mt-2 text-sm font-semibold"
                :class="githubCheck.ok ? 'text-lp-success' : 'text-lp-danger'"
              >
                {{ githubCheck.message }}
              </p>
            </div>
          </div>
        </AppSurface>

        <AppSurface>
          <p class="lp-kicker">Repository</p>
          <h2 class="lp-heading mt-2 text-xl">Initial import</h2>
          <div class="mt-5 space-y-3">
            <AppButton full-width variant="secondary" :loading="syncingRepository" @click="syncRepository(true)">
              {{ syncingRepository ? 'Syncing...' : 'Preview sync' }}
            </AppButton>
            <AppButton full-width :loading="syncingRepository" @click="syncRepository(false)">
              {{ syncingRepository ? 'Syncing...' : 'Import from repository' }}
            </AppButton>

            <p v-if="syncError" class="rounded-md border border-lp-danger/20 bg-lp-danger-soft p-3 text-sm font-semibold text-lp-danger">{{ syncError }}</p>

            <div v-if="syncResult" class="rounded-md border border-lp-border bg-lp-surface-soft p-3 text-sm text-lp-muted">
              <dl class="grid grid-cols-2 gap-x-4 gap-y-2">
                <div>
                  <dt class="text-xs font-semibold text-lp-subtle">Scanned</dt>
                  <dd class="font-black text-lp-ink">{{ syncResult.scanned }}</dd>
                </div>
                <div>
                  <dt class="text-xs font-semibold text-lp-subtle">Imported</dt>
                  <dd class="font-black text-lp-ink">{{ syncResult.imported }}</dd>
                </div>
                <div>
                  <dt class="text-xs font-semibold text-lp-subtle">Skipped</dt>
                  <dd class="font-black text-lp-ink">{{ syncResult.skipped }}</dd>
                </div>
                <div>
                  <dt class="text-xs font-semibold text-lp-subtle">Failed</dt>
                  <dd class="font-black text-lp-ink">{{ syncResult.failed }}</dd>
                </div>
                <div>
                  <dt class="text-xs font-semibold text-lp-subtle">Ambiguous</dt>
                  <dd class="font-black text-lp-ink">{{ syncResult.ambiguous }}</dd>
                </div>
                <div>
                  <dt class="text-xs font-semibold text-lp-subtle">Draft files</dt>
                  <dd class="font-black text-lp-ink">{{ syncResult.draftsInRepository }}</dd>
                </div>
              </dl>

              <ul v-if="syncResult.errors.length" class="mt-3 space-y-2 border-t border-lp-border pt-3">
                <li v-for="error in syncResult.errors" :key="`${error.filePath}:${error.message}`">
                  <p class="break-all font-bold text-lp-danger">{{ error.filePath }}</p>
                  <p class="text-lp-danger">{{ error.message }}</p>
                </li>
              </ul>
              <div v-if="syncResult.draftFiles?.length" class="mt-3 border-t border-lp-border pt-3">
                <p class="text-xs font-black uppercase tracking-wide text-lp-subtle">Draft files to remove after verification</p>
                <ul class="mt-2 space-y-1">
                  <li v-for="file in syncResult.draftFiles" :key="file" class="break-all font-semibold text-lp-ink">{{ file }}</li>
                </ul>
              </div>
            </div>
          </div>
        </AppSurface>
      </aside>
    </div>

    <AppSurface v-else>
      <LoadingState label="Loading..." />
    </AppSurface>
  </main>
</template>
