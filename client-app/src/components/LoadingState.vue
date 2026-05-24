<script setup lang="ts">
withDefaults(
  defineProps<{
    label?: string
  }>(),
  {
    label: 'Loading...',
  },
)
</script>

<template>
  <div class="flex items-center gap-3 text-sm text-lp-muted" role="status" aria-live="polite">
    <span class="h-4 w-4 animate-spin rounded-full border-2 border-lp-border-strong border-t-lp-primary" aria-hidden="true"></span>
    <span>{{ label }}</span>
  </div>
</template>
