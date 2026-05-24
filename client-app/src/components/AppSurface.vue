<script setup lang="ts">
import { computed } from 'vue'

type SurfacePadding = 'none' | 'sm' | 'md' | 'lg'

const props = withDefaults(
  defineProps<{
    as?: string
    elevated?: boolean
    padding?: SurfacePadding
    soft?: boolean
  }>(),
  {
    as: 'section',
    elevated: true,
    padding: 'md',
    soft: false,
  },
)

const surfaceClasses = computed(() => {
  const padding: Record<SurfacePadding, string> = {
    none: '',
    sm: 'p-4',
    md: 'p-5 sm:p-6',
    lg: 'p-6 sm:p-8',
  }

  return [
    props.soft ? 'rounded-md border border-lp-border bg-lp-surface-soft' : 'rounded-lg border border-lp-border bg-lp-surface',
    props.elevated ? 'shadow-lp-surface' : '',
    padding[props.padding],
  ]
})
</script>

<template>
  <component :is="as" :class="surfaceClasses">
    <slot />
  </component>
</template>
