import { onBeforeUnmount, ref, watch, type Ref } from 'vue'
import { adminService } from '@/services/adminService'
import { safeUrl } from '@/utils/contentTypes'

export function stagedMediaId(value: string) {
  return value.match(/\/api\/media\/staged\/([a-f0-9]{32}\.(?:jpg|png|gif|webp))$/)?.[1]
}

// Never send authorization to a URL found in post content. Only request our own API.
export function useMediaPreview(sources: Ref<string[]>) {
  const previews = ref<Record<string, string>>({})
  const failed = ref(false)
  const pending = new Set<string>()
  let disposed = false
  watch(
    sources,
    (values) => {
      const wanted = new Set(values.map(stagedMediaId).filter((id): id is string => !!id))
      for (const [id, url] of Object.entries(previews.value)) {
        if (!wanted.has(id)) {
          URL.revokeObjectURL(url)
          delete previews.value[id]
        }
      }
      failed.value = false
      for (const id of wanted) {
        if (previews.value[id] || pending.has(id)) continue
        pending.add(id)
        void adminService
          .getStagedMedia(id)
          .then((blob) => {
            if (!disposed && sources.value.some((value) => stagedMediaId(value) === id))
              previews.value[id] = URL.createObjectURL(blob)
          })
          .catch(() => {
            if (!disposed) failed.value = true
          })
          .finally(() => pending.delete(id))
      }
    },
    { immediate: true },
  )
  onBeforeUnmount(() => {
    disposed = true
    Object.values(previews.value).forEach((url) => URL.revokeObjectURL(url))
  })
  return {
    failed,
    resolve: (value: string) => {
      const id = stagedMediaId(value)
      return id ? previews.value[id] : safeUrl(value)
    },
  }
}
