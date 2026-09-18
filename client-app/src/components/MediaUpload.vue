<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { adminService } from '@/services/adminService'
import { useMediaPreview } from '@/composables/useMediaPreview'
import { readError } from '@/utils/contentTypes'
const props = defineProps<{
  modelValue: string
  alt?: string
  kind: 'photo' | 'audio' | 'video'
  accept: string
  disabled?: boolean
}>()
const emit = defineEmits<{ 'update:modelValue': [url: string]; busy: [value: boolean] }>()
const fileInput = ref<HTMLInputElement>(),
  uploading = ref(false),
  progress = ref(0),
  error = ref(''),
  localPreview = ref(''),
  failedPreview = ref(false),
  dragDepth = ref(0)
let disposed = false
const staged = useMediaPreview(computed(() => [props.modelValue]))
const preview = computed(() => localPreview.value || staged.resolve(props.modelValue))
watch(
  () => props.modelValue,
  () => {
    failedPreview.value = false
    releasePreview()
  },
)
function releasePreview() {
  if (localPreview.value) URL.revokeObjectURL(localPreview.value)
  localPreview.value = ''
}
onBeforeUnmount(() => {
  disposed = true
  releasePreview()
})
function fileChanged(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (file) void upload(file)
}
function drop(event: DragEvent) {
  dragDepth.value = 0
  if (event.dataTransfer?.files.length !== 1) {
    error.value = 'Drop one file at a time.'
    return
  }
  const file = event.dataTransfer.files[0]
  if (file) void upload(file)
}
async function upload(file: File) {
  if (uploading.value || props.disabled) return
  error.value = ''
  if (!props.accept.split(',').includes(file.type) || !file.size || file.size > 20 * 1024 * 1024) {
    error.value = 'Choose a supported file up to 20 MB.'
    return
  }
  uploading.value = true
  progress.value = 0
  emit('busy', true)
  let candidate = ''
  try {
    candidate = URL.createObjectURL(file)
    if (props.kind === 'photo') {
      const image = new Image()
      image.src = candidate
      try {
        await image.decode()
      } catch {
        throw new Error('invalid-image')
      }
    }
    if (disposed) return
    const result = await adminService.uploadMedia(file, (percent) => {
      progress.value = percent
    })
    if (disposed) return
    releasePreview()
    localPreview.value = candidate
    candidate = ''
    failedPreview.value = false
    emit('update:modelValue', result.url)
  } catch (caught) {
    if (!disposed)
      error.value =
        caught instanceof Error && caught.message === 'invalid-image'
          ? 'This file could not be opened as a photo. Your previous photo is unchanged.'
          : readError(caught)
  } finally {
    if (candidate) URL.revokeObjectURL(candidate)
    uploading.value = false
    if (!disposed) emit('busy', false)
  }
}
</script>
<template>
  <div class="media-upload">
    <input
      ref="fileInput"
      type="file"
      :accept="accept"
      hidden
      :disabled="disabled || uploading"
      @change="fileChanged"
    />
    <button
      type="button"
      class="photo-dropzone"
      :class="{ dragging: dragDepth > 0, 'has-photo': preview && !failedPreview }"
      :disabled="disabled || uploading"
      :aria-busy="uploading"
      :aria-label="`${preview ? 'Change' : 'Upload'} ${kind}`"
      @click="fileInput?.click()"
      @dragenter.prevent="dragDepth++"
      @dragover.prevent
      @dragleave.prevent="dragDepth = Math.max(0, dragDepth - 1)"
      @drop.prevent="drop"
    >
      <img
        v-if="kind === 'photo' && preview && !failedPreview"
        :src="preview"
        :alt="alt || 'Selected photo'"
        draggable="false"
        @error="failedPreview = true"
      />
      <span v-else class="dropzone-empty"
        ><svg viewBox="0 0 32 32" aria-hidden="true">
          <rect x="4" y="5" width="24" height="22" rx="3" />
          <circle cx="11" cy="12" r="2" />
          <path d="m5 24 8-8 5 5 4-5 6 7" /></svg
        ><strong>{{
          uploading
            ? 'Uploading…'
            : preview
              ? `Change ${kind}`
              : `Drop ${kind === 'audio' ? 'an audio file' : 'a ' + kind} here`
        }}</strong
        ><span>{{
          failedPreview
            ? 'Preview unavailable. Click to choose a replacement.'
            : 'or click to choose a file'
        }}</span></span
      >
      <span v-if="kind === 'photo' && preview && !failedPreview" class="photo-change">{{
        uploading ? `Uploading… ${progress}%` : 'Change photo'
      }}</span>
    </button>
    <progress v-if="uploading" :value="progress" max="100" aria-label="Upload progress" />
    <p class="upload-status" role="status">
      {{ uploading ? `Uploading… ${progress}%` : 'Up to 20 MB · Click or drop a file to replace' }}
    </p>
    <p v-if="staged.failed.value" class="writer-error" role="alert">
      Could not load the staged image. Reopen the post to retry.
    </p>
    <p v-if="error" role="alert" class="writer-error">{{ error }}</p>
    <audio v-if="kind === 'audio' && preview" :src="preview" controls preload="metadata" />
    <video v-if="kind === 'video' && preview" :src="preview" controls preload="metadata" />
  </div>
</template>
