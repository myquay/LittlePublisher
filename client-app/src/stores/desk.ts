import { defineStore } from 'pinia'
import { ref } from 'vue'
import { adminService } from '@/services/adminService'
import { readError } from '@/utils/contentTypes'
import type { AdminDashboard } from '@/types/admin'
export const useDeskStore = defineStore('desk', () => {
  const dashboard = ref<AdminDashboard | null>(null)
  const loading = ref(false)
  const error = ref('')
  let generation = 0
  function reset() {
    generation++
    dashboard.value = null
    error.value = ''
    loading.value = false
  }
  async function refresh() {
    if (loading.value) return
    const current = generation
    loading.value = true
    error.value = ''
    try {
      const result = await adminService.getDashboard()
      if (current === generation) dashboard.value = result
    } catch (caught) {
      if (current === generation) error.value = readError(caught)
    } finally {
      if (current === generation) loading.value = false
    }
  }
  return { dashboard, loading, error, refresh, reset }
})
