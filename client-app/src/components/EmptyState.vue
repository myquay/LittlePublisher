<script setup lang="ts">
defineProps<{
  description?: string
  title: string
}>()
</script>

<template>
  <section class="rounded-md border border-dashed border-lp-border-strong bg-lp-surface-soft p-5 text-sm">
    <h2 class="font-bold text-lp-ink">{{ title }}</h2>
    <p v-if="description" class="mt-1 leading-6 text-lp-muted">{{ description }}</p>
    <div v-if="$slots.default" class="mt-4">
      <slot />
    </div>
  </section>
</template>
