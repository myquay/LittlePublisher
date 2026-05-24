<script setup lang="ts">
import { computed } from 'vue'

type BadgeTone = 'neutral' | 'success' | 'warning' | 'danger' | 'info' | 'driftwood'

const props = withDefaults(
  defineProps<{
    tone?: BadgeTone
  }>(),
  {
    tone: 'neutral',
  },
)

const badgeClasses = computed(() => {
  const tones: Record<BadgeTone, string> = {
    neutral: 'border-lp-border bg-lp-surface-soft text-lp-muted',
    success: 'border-lp-success/20 bg-lp-success-soft text-lp-success',
    warning: 'border-lp-warning/20 bg-lp-warning-soft text-lp-warning',
    danger: 'border-lp-danger/20 bg-lp-danger-soft text-lp-danger',
    info: 'border-lp-info/20 bg-lp-info-soft text-lp-info',
    driftwood: 'border-lp-driftwood/20 bg-lp-driftwood-soft text-lp-driftwood',
  }

  return [
    'inline-flex min-h-7 w-fit items-center rounded-full border px-2.5 py-1 text-xs font-bold',
    tones[props.tone],
  ]
})
</script>

<template>
  <span :class="badgeClasses">
    <slot />
  </span>
</template>
