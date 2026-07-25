<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { AppButton, AppSurface, LoadingState, StatusBadge } from '@/components'
import { adminService } from '@/services/adminService'
import type { Post, SavePostRequest } from '@/types/admin'

const route = useRoute()
const router = useRouter()
const post = ref<Post | null>(null)
const loading = ref(false)
const saving = ref(false)
const publishing = ref(false)
const message = ref('')
const error = ref('')
const form = reactive<SavePostRequest>({
  title: '',
  content: '',
  summary: '',
  categories: [],
  slug: '',
  postType: 'article',
  requestedPublishedUtc: null,
})
const categoriesText = ref('')
const lastSaved = ref('')
const isNew = computed(() => !route.params.id)
const statusLabel = computed(() => {
  if (!post.value) return 'Unsaved draft'
  if (post.value.state === 'publish-failed') return 'Publish failed'
  if (post.value.hasUnpublishedChanges && post.value.publishedRevision) return 'Unpublished changes'
  if (post.value.publishedRevision) return 'Published'
  return 'Draft'
})

onMounted(async () => {
  if (isNew.value) return
  loading.value = true
  try {
    applyPost(await adminService.getPost(String(route.params.id)))
  } catch (caught) {
    error.value = readError(caught)
  } finally {
    loading.value = false
  }
})

function applyPost(value: Post) {
  post.value = value
  form.title = value.title ?? ''
  form.content = value.content
  form.summary = value.summary ?? ''
  form.categories = [...value.categories]
  form.slug = value.slug
  form.postType = value.postType
  form.requestedPublishedUtc = value.requestedPublishedUtc ?? null
  categoriesText.value = value.categories.join(', ')
  lastSaved.value = JSON.stringify(request())
}

function request(): SavePostRequest {
  return {
    ...form,
    title: form.postType === 'note' && !form.title?.trim() ? null : form.title,
    summary: form.summary?.trim() || null,
    slug: form.slug?.trim() || null,
    categories: categoriesText.value.split(',').map((value) => value.trim()).filter(Boolean),
  }
}

async function save() {
  saving.value = true
  error.value = ''
  message.value = ''
  try {
    const saved = post.value
      ? await adminService.updatePost(post.value.id, request(), post.value.eTag)
      : await adminService.createPost(request())
    applyPost(saved)
    message.value = 'Draft saved to Azure.'
    if (isNew.value) await router.replace(`/posts/${saved.id}`)
  } catch (caught) {
    error.value = readError(caught)
  } finally {
    saving.value = false
  }
}

async function publish() {
  if (!post.value || JSON.stringify(request()) !== lastSaved.value) await save()
  if (!post.value) return
  publishing.value = true
  error.value = ''
  message.value = ''
  try {
    applyPost(await adminService.publishPost(post.value.id))
    message.value = 'Published to GitHub.'
  } catch (caught) {
    error.value = readError(caught)
  } finally {
    publishing.value = false
  }
}

function readError(caught: unknown) {
  if (caught && typeof caught === 'object' && 'response' in caught) {
    const response = caught.response as { data?: { message?: string } }
    if (response.data?.message) return response.data.message
  }
  return 'The post could not be saved.'
}
</script>

<template>
  <main class="lp-shell">
    <LoadingState v-if="loading" label="Loading post..." />
    <div v-else class="grid gap-6 lg:grid-cols-[minmax(0,1fr)_20rem]">
      <AppSurface padding="lg">
        <div class="flex items-start justify-between gap-4">
          <div>
            <p class="lp-kicker">Editor</p>
            <h1 class="lp-heading mt-2 text-3xl">{{ isNew ? 'New post' : (post?.title || 'Untitled post') }}</h1>
          </div>
          <StatusBadge :tone="post?.state === 'publish-failed' ? 'danger' : post?.publishedRevision ? 'success' : 'warning'">
            {{ statusLabel }}
          </StatusBadge>
        </div>

        <div class="mt-6 space-y-5">
          <label class="block text-sm font-black text-lp-ink">
            Type
            <select v-model="form.postType" class="mt-2 w-full rounded-md border border-lp-border bg-white px-3 py-2 font-normal">
              <option value="article">Article</option>
              <option value="note">Note</option>
            </select>
          </label>
          <label class="block text-sm font-black text-lp-ink">
            Title
            <input v-model="form.title" class="mt-2 w-full rounded-md border border-lp-border bg-white px-3 py-2 font-normal" />
          </label>
          <label class="block text-sm font-black text-lp-ink">
            Summary
            <input v-model="form.summary" class="mt-2 w-full rounded-md border border-lp-border bg-white px-3 py-2 font-normal" />
          </label>
          <label class="block text-sm font-black text-lp-ink">
            Markdown
            <textarea v-model="form.content" rows="20" class="mt-2 w-full rounded-md border border-lp-border bg-white px-3 py-2 font-mono text-sm font-normal"></textarea>
          </label>
          <label class="block text-sm font-black text-lp-ink">
            Categories
            <input v-model="categoriesText" placeholder="indieweb, notes" class="mt-2 w-full rounded-md border border-lp-border bg-white px-3 py-2 font-normal" />
          </label>
          <label class="block text-sm font-black text-lp-ink">
            Slug
            <input v-model="form.slug" class="mt-2 w-full rounded-md border border-lp-border bg-white px-3 py-2 font-normal" />
          </label>
        </div>
      </AppSurface>

      <aside class="space-y-5">
        <AppSurface>
          <p class="lp-kicker">Actions</p>
          <div class="mt-4 space-y-3">
            <AppButton full-width variant="secondary" :loading="saving" @click="save">Save draft</AppButton>
            <AppButton full-width :loading="publishing" @click="publish">
              {{ post?.publishedRevision ? 'Republish' : 'Publish' }}
            </AppButton>
          </div>
          <p v-if="message" class="mt-4 text-sm font-bold text-lp-success">{{ message }}</p>
          <p v-if="error" class="mt-4 text-sm font-bold text-lp-danger">{{ error }}</p>
          <p v-if="post?.lastPublishError" class="mt-4 text-sm text-lp-danger">{{ post.lastPublishError }}</p>
          <a v-if="post?.publishedUrl" :href="post.publishedUrl" class="mt-4 block break-all text-sm font-bold text-lp-info">
            {{ post.publishedUrl }}
          </a>
        </AppSurface>
      </aside>
    </div>
  </main>
</template>
