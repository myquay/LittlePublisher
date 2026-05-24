<script setup lang="ts">
import { computed } from 'vue'

type ButtonVariant = 'primary' | 'secondary' | 'danger' | 'ghost'
type ButtonSize = 'sm' | 'md' | 'lg'
type ButtonType = 'button' | 'submit' | 'reset'

const props = withDefaults(
  defineProps<{
    disabled?: boolean
    fullWidth?: boolean
    loading?: boolean
    size?: ButtonSize
    type?: ButtonType
    variant?: ButtonVariant
  }>(),
  {
    disabled: false,
    fullWidth: false,
    loading: false,
    size: 'md',
    type: 'button',
    variant: 'primary',
  },
)

const emit = defineEmits<{
  click: [event: MouseEvent]
}>()

const buttonClasses = computed(() => {
  const variants: Record<ButtonVariant, string> = {
    primary: 'border-lp-primary bg-lp-primary text-white hover:bg-lp-primary-hover',
    secondary: 'border-lp-border bg-lp-surface text-lp-ink hover:border-lp-border-strong hover:bg-lp-surface-soft',
    danger: 'border-lp-danger bg-lp-danger text-white hover:bg-[#8f2931]',
    ghost: 'border-transparent bg-transparent text-lp-muted hover:bg-lp-surface-soft hover:text-lp-ink',
  }

  const sizes: Record<ButtonSize, string> = {
    sm: 'min-h-9 px-3 py-1.5 text-sm',
    md: 'min-h-10 px-4 py-2 text-sm',
    lg: 'min-h-12 px-5 py-3 text-base',
  }

  return [
    'inline-flex items-center justify-center gap-2 rounded-md border font-bold shadow-lp-control transition disabled:cursor-not-allowed disabled:opacity-55',
    props.fullWidth ? 'w-full' : 'w-fit',
    variants[props.variant],
    sizes[props.size],
  ]
})

function handleClick(event: MouseEvent) {
  if (props.disabled || props.loading) return
  emit('click', event)
}
</script>

<template>
  <button
    :type="type"
    :class="buttonClasses"
    :disabled="disabled || loading"
    :aria-busy="loading"
    @click="handleClick"
  >
    <span
      v-if="loading"
      class="h-4 w-4 animate-spin rounded-full border-2 border-current border-t-transparent"
      aria-hidden="true"
    ></span>
    <slot />
  </button>
</template>
