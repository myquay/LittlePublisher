<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { AppSurface, LoadingState, StatusBadge } from '@/components'
import logoUrl from '@/assets/brand/littlepublisher-logo.png'
import { setupService } from '@/services/setupService'
import type { SetupCheck, SetupStatus } from '@/types/setup'

const props = defineProps<{
  initialStatus?: SetupStatus
  publicMode?: boolean
  isRefreshing?: boolean
}>()

const status = ref<SetupStatus | null>(props.initialStatus ?? null)
const isLoading = ref(!props.initialStatus)
const error = ref('')

const totalChecks = computed(() => {
  return status.value?.groups.reduce((sum, group) => sum + group.checks.length, 0) ?? 0
})

const configuredChecks = computed(() => {
  return (
    status.value?.groups.reduce(
      (sum, group) => sum + group.checks.filter((check) => check.configured).length,
      0,
    ) ?? 0
  )
})

onMounted(async () => {
  if (status.value) {
    return
  }

  try {
    status.value = props.publicMode ? await setupService.getPublicStatus() : await setupService.getStatus()
  } catch {
    error.value = 'Setup status could not be loaded.'
  } finally {
    isLoading.value = false
  }
})

function checkTone(check: SetupCheck) {
  if (check.configured) return 'success'
  if (check.required) return 'danger'
  if (check.warning) return 'warning'
  return 'neutral'
}

function checkMark(check: SetupCheck) {
  if (check.configured) return 'OK'
  if (check.required) return '!'
  if (check.warning) return '?'
  return '-'
}
</script>

<template>
  <main class="lp-shell">
    <div class="mx-auto max-w-6xl">
      <header class="mb-6 grid gap-5 sm:grid-cols-[10rem_minmax(0,1fr)] sm:items-center">
        <img
          :src="logoUrl"
          alt="LittlePublisher"
          class="h-auto w-40 object-contain"
          width="1254"
          height="1254"
        />
        <div>
          <p class="lp-kicker">First-run setup</p>
          <h1 class="lp-heading mt-2 text-3xl sm:text-4xl">
            {{ props.publicMode ? 'Authentication setup' : 'Configuration status' }}
          </h1>
          <p class="lp-copy mt-3 max-w-2xl text-sm">
            {{
              props.publicMode
                ? 'Configuration required before LittlePublisher can authenticate administrators.'
                : 'Configuration required before LittlePublisher can accept Micropub posts.'
            }}
          </p>
        </div>
      </header>

      <AppSurface padding="lg">
        <LoadingState v-if="isLoading || props.isRefreshing" label="Checking configuration..." />

        <div v-else-if="error" class="rounded-md border border-lp-danger/20 bg-lp-danger-soft px-3 py-2 text-sm font-semibold text-lp-danger">
          {{ error }}
        </div>

        <div v-else-if="status" class="space-y-8">
          <section
            class="rounded-md border border-l-4 p-4"
            :class="
              status.ready
                ? 'border-lp-success/20 border-l-lp-success bg-lp-success-soft'
                : 'border-lp-danger/20 border-l-lp-danger bg-lp-danger-soft'
            "
            aria-live="polite"
          >
            <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
              <div>
                <strong class="text-sm font-black text-lp-ink">
                  {{ status.ready ? 'Ready to publish' : 'Configuration needed' }}
                </strong>
                <p class="mt-1 text-sm text-lp-muted">
                  {{ configuredChecks }} of {{ totalChecks }} settings are configured.
                  <span v-if="status.missingRequiredCount">
                    {{ status.missingRequiredCount }} required setting<span
                      v-if="status.missingRequiredCount !== 1"
                      >s</span
                    >
                    missing.
                  </span>
                </p>
              </div>
              <StatusBadge :tone="status.ready ? 'success' : 'danger'">
                {{ status.ready ? 'Ready' : 'Needs setup' }}
              </StatusBadge>
            </div>
          </section>

          <section v-for="group in status.groups" :key="group.name" class="space-y-3">
            <h2 class="lp-heading text-xl">{{ group.name }}</h2>

            <div class="grid gap-3">
              <div
                v-for="check in group.checks"
                :key="check.key"
                class="grid grid-cols-[2rem_1fr] gap-3 rounded-md border border-lp-border bg-lp-surface-soft p-4"
              >
                <div
                  class="flex h-8 w-8 items-center justify-center rounded-full border text-xs font-black"
                  :class="{
                    'border-lp-success/30 bg-lp-success-soft text-lp-success': checkTone(check) === 'success',
                    'border-lp-danger/30 bg-lp-danger-soft text-lp-danger': checkTone(check) === 'danger',
                    'border-lp-warning/30 bg-lp-warning-soft text-lp-warning': checkTone(check) === 'warning',
                    'border-lp-border-strong bg-lp-surface text-lp-muted': checkTone(check) === 'neutral',
                  }"
                  aria-hidden="true"
                >
                  {{ checkMark(check) }}
                </div>

                <div class="min-w-0">
                  <div class="flex flex-wrap items-baseline justify-between gap-2">
                    <strong class="break-all text-sm font-black text-lp-ink">{{ check.key }}</strong>
                    <StatusBadge :tone="check.required ? 'danger' : 'neutral'">
                      {{ check.required ? 'Required' : 'Optional' }}
                    </StatusBadge>
                  </div>
                  <p class="mt-1 text-sm leading-6 text-lp-muted">{{ check.description }}</p>
                  <p v-if="check.displayValue" class="mt-2 break-all text-xs font-semibold text-lp-subtle">
                    {{ check.secret ? 'Secret configured' : check.displayValue }}
                  </p>
                </div>
              </div>
            </div>
          </section>
        </div>
      </AppSurface>
    </div>
  </main>
</template>
