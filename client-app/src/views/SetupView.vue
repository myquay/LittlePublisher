<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import logoUrl from '@/assets/brand/littlepublisher-logo.png'
import { setupService } from '@/services/setupService'
import type { SetupStatus } from '@/types/setup'

const props = defineProps<{
  initialStatus?: SetupStatus
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
    status.value = await setupService.getStatus()
  } catch {
    error.value = 'Setup status could not be loaded.'
  } finally {
    isLoading.value = false
  }
})
</script>

<template>
  <main class="mx-auto max-w-6xl px-4 py-5 sm:px-6">
    <div class="mb-4 flex justify-center sm:mb-5">
      <img
        :src="logoUrl"
        alt="LittlePublisher"
        class="h-auto w-full max-w-[17.5rem] object-contain sm:max-w-xs"
        width="1254"
        height="1254"
      />
    </div>

    <section class="rounded-lg border border-slate-200 bg-white p-5 shadow-sm sm:p-6">
      <div class="mb-5 text-center">
        <p class="text-sm font-medium uppercase tracking-wide text-slate-500">First-run setup</p>
        <h1 class="mt-2 text-2xl font-semibold text-slate-950">Configuration status</h1>
        <p class="mt-2 text-sm text-slate-600">
          Configuration required before LittlePublisher can accept Micropub posts.
        </p>
      </div>

      <div v-if="isLoading" class="flex items-center gap-3 text-slate-600">
        <div class="h-4 w-4 animate-spin rounded-full border-2 border-slate-300 border-t-slate-800"></div>
        <p>Checking configuration...</p>
      </div>

      <div v-else-if="error" class="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
        {{ error }}
      </div>

      <div v-else-if="status" class="space-y-8">
        <section
          class="rounded-md border border-l-4 p-4"
          :class="
            status.ready
              ? 'border-emerald-200 border-l-emerald-500 bg-emerald-50'
              : 'border-red-200 border-l-red-500 bg-red-50'
          "
          aria-live="polite"
        >
          <strong class="text-sm font-semibold text-slate-950">
            {{ status.ready ? 'Ready to publish' : 'Configuration needed' }}
          </strong>
          <p class="mt-1 text-sm text-slate-700">
            {{ configuredChecks }} of {{ totalChecks }} settings are configured.
            <span v-if="status.missingRequiredCount">
              {{ status.missingRequiredCount }} required setting<span
                v-if="status.missingRequiredCount !== 1"
                >s</span
              >
              missing.
            </span>
          </p>
        </section>

        <section v-for="group in status.groups" :key="group.name" class="space-y-3">
          <h2 class="text-lg font-semibold text-slate-950">{{ group.name }}</h2>

          <div class="grid gap-3">
            <div
              v-for="check in group.checks"
              :key="check.key"
              class="grid grid-cols-[2rem_1fr] gap-3 rounded-md border bg-white p-4"
              :class="
                check.configured
                  ? 'border-emerald-200'
                  : check.required
                    ? 'border-red-200'
                    : 'border-slate-200'
              "
            >
              <div
                class="flex h-8 w-8 items-center justify-center rounded-full border text-xs font-bold"
                :class="
                  check.configured
                    ? 'border-emerald-300 bg-emerald-50 text-emerald-700'
                    : check.required
                      ? 'border-red-300 bg-red-50 text-red-700'
                      : 'border-slate-300 bg-slate-50 text-slate-500'
                "
                aria-hidden="true"
              >
                {{ check.configured ? 'OK' : check.required ? '!' : '-' }}
              </div>

              <div class="min-w-0">
                <div class="flex flex-wrap items-baseline justify-between gap-2">
                  <strong class="break-all text-sm font-semibold text-slate-950">{{ check.key }}</strong>
                  <span class="text-xs font-medium uppercase tracking-wide text-slate-500">
                    {{ check.required ? 'Required' : 'Optional' }}
                  </span>
                </div>
                <p class="mt-1 text-sm text-slate-600">{{ check.description }}</p>
                <p v-if="check.displayValue" class="mt-2 break-all text-xs text-slate-500">
                  {{ check.secret ? 'Secret configured' : check.displayValue }}
                </p>
              </div>
            </div>
          </div>
        </section>
      </div>
    </section>
  </main>
</template>
