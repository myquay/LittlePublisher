<script setup lang="ts">
import { ref } from 'vue'
import { AppButton, AppSurface, StatusBadge } from '@/components'
import { adminService } from '@/services/adminService'
import { useDeskStore } from '@/stores/desk'
import { readError } from '@/utils/contentTypes'
import type { AdminCheck, ImportRepositoryResult } from '@/types/admin'
const desk = useDeskStore()
const storageCheck = ref<AdminCheck | null>(null)
const githubCheck = ref<AdminCheck | null>(null)
const checkingStorage = ref(false)
const checkingGitHub = ref(false)
const syncingRepository = ref(false)
const syncResult = ref<ImportRepositoryResult | null>(null)
const syncError = ref<string | null>(null)

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
      await desk.refresh()
    }
  } catch (error) {
    syncError.value = readError(error)
  } finally {
    syncingRepository.value = false
  }
}

function checkTone(check: AdminCheck | null) {
  return !check ? 'neutral' : check.ok ? 'success' : 'danger'
}
</script>
<template>
  <div class="account-operations">
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
        <AppButton
          full-width
          variant="secondary"
          :loading="syncingRepository"
          @click="syncRepository(true)"
        >
          {{ syncingRepository ? 'Syncing...' : 'Preview sync' }}
        </AppButton>
        <AppButton full-width :loading="syncingRepository" @click="syncRepository(false)">
          {{ syncingRepository ? 'Syncing...' : 'Import from repository' }}
        </AppButton>

        <p
          v-if="syncError"
          class="rounded-md border border-lp-danger/20 bg-lp-danger-soft p-3 text-sm font-semibold text-lp-danger"
        >
          {{ syncError }}
        </p>

        <div
          v-if="syncResult"
          class="rounded-md border border-lp-border bg-lp-surface-soft p-3 text-sm text-lp-muted"
        >
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
            <p class="text-xs font-black uppercase tracking-wide text-lp-subtle">
              Draft files to remove after verification
            </p>
            <ul class="mt-2 space-y-1">
              <li
                v-for="file in syncResult.draftFiles"
                :key="file"
                class="break-all font-semibold text-lp-ink"
              >
                {{ file }}
              </li>
            </ul>
          </div>
        </div>
      </div>
    </AppSurface>
  </div>
</template>
<style scoped>
.account-operations :deep(.lp-surface) {
  box-shadow: none;
  border: 0;
  border-radius: 0;
  border-top: 1px solid var(--color-lp-border);
  padding: 20px;
}
.account-operations :deep(.lp-heading) {
  font-size: 16px;
  font-weight: 600;
}
.account-operations :deep(.lp-kicker) {
  display: none;
}
</style>
