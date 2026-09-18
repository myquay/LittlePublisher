<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { onBeforeRouteLeave, onBeforeRouteUpdate, useRoute, useRouter } from 'vue-router'
import { adminService } from '@/services/adminService'
import { contentTypes, readError, safeUrl } from '@/utils/contentTypes'
import { conversationTemplate, parseConversations } from '@/utils/conversations'
import { markdownPreview } from '@/utils/markdownPreview'
import { registerEditorLeave } from '@/utils/editorNavigation'
import BookSearch from '@/components/BookSearch.vue'
import MediaUpload from '@/components/MediaUpload.vue'
import type { Post, PostType, SavePostRequest } from '@/types/admin'
const route = useRoute(),
  router = useRouter()
const post = ref<Post | null>(null),
  loading = ref(false),
  loadFailed = ref(false),
  saving = ref(false),
  publishing = ref(false),
  uploading = ref(false)
const error = ref(''),
  message = ref(''),
  categoriesText = ref(''),
  lastSaved = ref('')
let movingSaved = false
const previewing = ref(false),
  focusMode = ref(false),
  formatOpen = ref(false)
const detailsDialog = ref<HTMLDialogElement>(),
  reviewDialog = ref<HTMLDialogElement>(),
  helpDialog = ref<HTMLDialogElement>()
const titleInput = ref<HTMLTextAreaElement>(),
  bodyInput = ref<HTMLTextAreaElement>()
const form = reactive<SavePostRequest>({
  title: '',
  content: '',
  summary: '',
  categories: [],
  slug: '',
  postType: 'article',
  properties: {},
  requestedPublishedUtc: null,
})
const propertyDrafts = reactive<Partial<Record<PostType, Record<string, string[]>>>>({})
const config = computed(() => contentTypes[form.postType])
const busy = computed(() => saving.value || publishing.value || uploading.value || loading.value)
const dirty = computed(() => JSON.stringify(request()) !== lastSaved.value)
const words = computed(() => form.content.trim().split(/\s+/).filter(Boolean).length)
const statusLabel = computed(() =>
  post.value?.state === 'publish-failed'
    ? 'Publish failed'
    : post.value?.publishedRevision
      ? dirty.value || post.value.hasUnpublishedChanges
        ? 'Unpublished changes'
        : 'Published'
      : 'Draft',
)
const conversationError = computed(() => parseConversations(form.content).error)
const readingPreview = computed(() => markdownPreview(form.content))
const mediaUrl = computed(() =>
  config.value.media ? safeUrl(form.properties[config.value.media]?.[0]) : undefined,
)
function request(): SavePostRequest {
  return {
    ...form,
    title: form.title?.trim() || null,
    summary: form.summary?.trim() || null,
    slug: form.slug?.trim() || null,
    categories: categoriesText.value
      .split(',')
      .map((v) => v.trim())
      .filter(Boolean),
    properties: Object.fromEntries(
      config.value.fields
        .map(
          (field) =>
            [
              field.key,
              (form.properties[field.key] || []).map((v) => v.trim()).filter(Boolean),
            ] as [string, string[]],
        )
        .filter(([, values]) => values.length),
    ),
  }
}
function property(key: string) {
  return form.properties[key]?.[0] || ''
}
function setProperty(key: string, value: string) {
  form.properties[key] = [value, ...(form.properties[key]?.slice(1) || [])]
}
function selectBook(properties: Record<string, string[]>) {
  Object.assign(form.properties, properties)
  if (!form.title?.trim()) form.title = `${properties['book-title']?.[0]} — review`
}
function changeType(event: Event) {
  propertyDrafts[form.postType] = structuredClone(request().properties)
  form.postType = (event.target as HTMLSelectElement).value as PostType
  form.properties = Object.fromEntries(
    Object.entries(propertyDrafts[form.postType] || {}).map(([key, values]) => [key, [...values]]),
  )
  error.value = ''
  void resize()
}
function applyPost(value: Post) {
  post.value = value
  Object.assign(form, {
    title: value.title || '',
    content: value.content,
    summary: value.summary || '',
    categories: [...value.categories],
    slug: value.slug,
    postType: value.postType,
    requestedPublishedUtc: value.requestedPublishedUtc ?? null,
    properties: Object.fromEntries(
      Object.entries(value.properties || {}).map(([key, values]) => [key, [...values]]),
    ),
  })
  categoriesText.value = value.categories.join(', ')
  lastSaved.value = JSON.stringify(request())
  void resize()
}
async function load(id?: string) {
  loadFailed.value = false
  error.value = ''
  message.value = ''
  post.value = null
  for (const key of Object.keys(propertyDrafts)) delete propertyDrafts[key as PostType]
  Object.assign(form, {
    title: '',
    content: '',
    summary: '',
    categories: [],
    slug: '',
    postType: 'article',
    requestedPublishedUtc: null,
    properties: {},
  })
  categoriesText.value = ''
  lastSaved.value = JSON.stringify(request())
  previewing.value = false
  if (!id) {
    void resize()
    return
  }
  loading.value = true
  try {
    applyPost(await adminService.getPost(id))
  } catch (caught) {
    error.value = readError(caught)
    loadFailed.value = true
  } finally {
    loading.value = false
    void resize()
  }
}
function validate(complete: boolean) {
  if (complete && conversationError.value) return conversationError.value
  if (!form.title?.trim() && !form.content.trim())
    return 'Add a title or some writing before saving.'
  if (form.postType === 'book-review') {
    if (complete && !form.content.trim()) return 'Write your review before publishing.'
    if (property('rating') && !/^[1-5]$/.test(property('rating')))
      return 'Choose a whole-star rating from 1 to 5.'
    const cover = property('book-cover')
    if (
      cover &&
      !/^https:\/\//.test(cover) &&
      (cover.includes(':') ||
        cover.includes('\\') ||
        cover.startsWith('//') ||
        cover.split('/').some((p) => p === '..' || p === '.'))
    )
      return 'Use an HTTPS cover URL, site-relative path, or bundle resource.'
  }
  for (const field of config.value.fields) {
    const value = property(field.key).trim()
    if (complete && field.required && !value)
      return `Add ${field.label.toLowerCase()} before publishing.`
    if (
      value &&
      field.label.endsWith('URL') &&
      (!safeUrl(value) ||
        (field.key === config.value.media &&
          !value.startsWith('/') &&
          !value.startsWith('https://')))
    )
      return `${field.label} must be ${field.key === config.value.media ? 'HTTPS' : 'HTTP(S)'} or a site-relative path.`
  }
  return ''
}
async function save(navigate = true): Promise<boolean> {
  if (busy.value || loadFailed.value) return false
  error.value = validate(false)
  if (error.value) return false
  saving.value = true
  message.value = ''
  try {
    const saved = post.value
      ? await adminService.updatePost(post.value.id, request(), post.value.eTag)
      : await adminService.createPost(request())
    applyPost(saved)
    message.value = 'Draft saved.'
    if (navigate && !route.params.id) {
      movingSaved = true
      try {
        await router.replace(`/posts/${saved.id}`)
      } finally {
        movingSaved = false
      }
    }
    return true
  } catch (caught) {
    error.value = readError(caught)
    return false
  } finally {
    saving.value = false
  }
}
function review() {
  error.value = validate(true)
  if (!error.value) reviewDialog.value?.showModal()
}
async function publish() {
  if (busy.value) return
  error.value = validate(true)
  if (error.value) return
  if ((!post.value || dirty.value) && !(await save())) return
  if (!post.value) return
  publishing.value = true
  message.value = ''
  try {
    applyPost(await adminService.publishPost(post.value.id))
    message.value = 'Published to your site.'
    reviewDialog.value?.close()
  } catch (caught) {
    error.value = readError(caught)
  } finally {
    publishing.value = false
  }
}
async function resize() {
  await nextTick()
  for (const el of [titleInput.value, bodyInput.value]) {
    if (el) {
      // Measure outside the document flow so a long post never collapses while
      // typing. Collapsing the live textarea clamps the page's scroll position.
      const measuring = el.cloneNode(false) as HTMLTextAreaElement
      measuring.value = el.value
      measuring.removeAttribute('id')
      measuring.removeAttribute('name')
      measuring.setAttribute('aria-hidden', 'true')
      measuring.tabIndex = -1
      Object.assign(measuring.style, {
        position: 'absolute',
        top: '0',
        left: '0',
        height: 'auto',
        width: `${el.clientWidth}px`,
        visibility: 'hidden',
        pointerEvents: 'none',
      })
      el.after(measuring)
      el.style.height = `${measuring.scrollHeight}px`
      measuring.remove()
    }
  }
}
function insert(kind: string) {
  const el = bodyInput.value
  if (!el) return
  const start = el.selectionStart,
    end = el.selectionEnd,
    selected = form.content.slice(start, end)
  const text =
    (
      {
        conversation: `\n\n${conversationTemplate.replace('Your question here.', selected || 'Your question here.')}\n\n`,
        bold: `**${selected || 'bold text'}**`,
        italic: `*${selected || 'italic text'}*`,
        heading: `\n## ${selected || 'Heading'}\n`,
        link: `[${selected || 'link text'}](https://example.com)`,
        image: `![${selected || 'Image description'}](https://example.com/image.jpg)`,
        quote: `\n> ${selected || 'A thought worth keeping'}\n`,
      } as Record<string, string>
    )[kind] || ''
  form.content = form.content.slice(0, start) + text + form.content.slice(end)
  formatOpen.value = false
  void nextTick(() => {
    el.focus()
    el.setSelectionRange(start, start + text.length)
  })
}
function keys(event: KeyboardEvent) {
  if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 's') {
    event.preventDefault()
    void save()
  }
  if (event.key === 'Escape') {
    focusMode.value = false
    formatOpen.value = false
  }
}
function beforeUnload(event: BeforeUnloadEvent) {
  if (dirty.value || busy.value) {
    event.preventDefault()
    event.returnValue = ''
  }
}
async function leave() {
  if (movingSaved) return true
  if (busy.value) {
    error.value = 'Please wait for the current save or upload to finish.'
    return false
  }
  if (!dirty.value || loadFailed.value) return true
  return save(false)
}
const unregisterLeave = registerEditorLeave(leave)
onBeforeRouteLeave(leave)
onBeforeRouteUpdate(async (to) => {
  if (to.params.id === post.value?.id) return true
  return leave()
})
watch(
  () => route.params.id,
  (id) => {
    if (id !== post.value?.id) void load(id ? String(id) : undefined)
  },
)
watch(() => [form.title, form.content, previewing.value], resize)
watch(focusMode, (value) => document.documentElement.classList.toggle('writer-focus', value))
onMounted(() => {
  void load(route.params.id ? String(route.params.id) : undefined)
  window.addEventListener('keydown', keys)
  window.addEventListener('beforeunload', beforeUnload)
})
onBeforeUnmount(() => {
  unregisterLeave()
  window.removeEventListener('keydown', keys)
  window.removeEventListener('beforeunload', beforeUnload)
  document.documentElement.classList.remove('writer-focus')
})
</script>
<template>
  <div class="writer-editor">
    <p v-if="loading" class="desk-message" role="status">Loading your post…</p>
    <div v-else-if="loadFailed" class="writing-canvas">
      <p role="alert" class="writer-error">{{ error }}</p>
      <button class="writer-secondary" @click="load(String(route.params.id))">Try again</button>
    </div>
    <template v-else>
      <nav class="editor-toolbar" aria-label="Editor tools">
        <button :aria-pressed="focusMode" @click="focusMode = !focusMode">
          {{ focusMode ? 'Exit focus' : 'Focus' }}</button
        ><button :aria-pressed="previewing" @click="previewing = !previewing">
          {{ previewing ? 'Edit' : 'Preview' }}</button
        ><button class="writer-secondary" @click="detailsDialog?.showModal()">
          Post details ☷
        </button>
      </nav>
      <main class="writing-canvas">
        <div class="writer-eyebrow">
          <select
            aria-label="Content type"
            :value="form.postType"
            :disabled="busy"
            @change="changeType"
          >
            <option v-for="(value, key) in contentTypes" :key="key" :value="key">
              {{ value.label }}
            </option></select
          ><span>/</span><span>{{ statusLabel }}</span>
        </div>
        <fieldset v-if="!previewing" :disabled="busy" class="writing-fields">
          <textarea
            ref="titleInput"
            v-model="form.title"
            class="writing-title"
            aria-label="Post title"
            :placeholder="form.postType === 'article' ? 'Title here' : 'Title (optional)'"
            rows="1"
          />
          <div v-if="config.fields.length" class="type-context">
            <template v-if="form.postType === 'book-review'">
              <BookSearch
                :key="String(route.params.id || 'new')"
                :disabled="busy"
                @select="selectBook"
              />
              <MediaUpload
                kind="photo"
                accept="image/jpeg,image/png,image/gif,image/webp"
                :model-value="property('book-cover')"
                :alt="property('book-cover-alt')"
                :disabled="saving || publishing"
                @busy="uploading = $event"
                @update:model-value="setProperty('book-cover', $event)"
              />
            </template>
            <MediaUpload
              v-if="config.media"
              :key="form.postType"
              :kind="config.media"
              :accept="config.accept!"
              :model-value="property(config.media)"
              :alt="property('alt')"
              :disabled="saving || publishing"
              @busy="uploading = $event"
              @update:model-value="setProperty(config.media!, $event)"
            />
            <template v-for="field in config.fields" :key="field.key"
              ><details v-if="field.key === config.media" class="media-url">
                <summary>Use {{ config.media === 'photo' ? 'an image' : 'a media' }} URL</summary>
                <label :for="`property-${field.key}`">{{ field.label }}</label
                ><input
                  :id="`property-${field.key}`"
                  :value="property(field.key)"
                  type="text"
                  @input="setProperty(field.key, ($event.target as HTMLInputElement).value)"
                />
              </details>
              <div v-else class="property-field">
                <label :for="`property-${field.key}`"
                  >{{ field.label }} <small v-if="field.required">required to publish</small></label
                ><select
                  v-if="field.key === 'rating'"
                  :id="`property-${field.key}`"
                  :value="property(field.key)"
                  @change="setProperty(field.key, ($event.target as HTMLSelectElement).value)"
                >
                  <option value="">Choose a rating</option>
                  <option v-for="stars in 5" :key="stars" :value="String(stars)">
                    {{ '★'.repeat(stars) }} — {{ stars }} / 5
                  </option></select
                ><textarea
                  v-else-if="field.key === 'alt' || field.key === 'book-cover-alt'"
                  :id="`property-${field.key}`"
                  :value="property(field.key)"
                  rows="2"
                  @input="setProperty(field.key, ($event.target as HTMLTextAreaElement).value)"
                /><input
                  v-else
                  :type="field.key === 'date-read' ? 'date' : 'text'"
                  :id="`property-${field.key}`"
                  :value="property(field.key)"
                  :placeholder="
                    ['start', 'end'].includes(field.key) ? '2026-09-18T18:00:00+12:00' : ''
                  "
                  @input="setProperty(field.key, ($event.target as HTMLInputElement).value)"
                />
                <p v-if="field.key === 'alt' && form.postType === 'photo'" class="field-help">
                  Describe what matters in the image for someone who cannot see it.
                </p>
                <p v-if="field.key === 'start'" class="field-help">
                  Include the date, time and timezone.
                </p>
              </div></template
            >
          </div>
          <div class="writing-body-wrap">
            <button
              class="format-toggle"
              aria-label="Insert Markdown formatting"
              :aria-expanded="formatOpen"
              @click="formatOpen = !formatOpen"
            >
              ＋
            </button>
            <div v-if="formatOpen" class="format-menu">
              <button
                v-for="kind in [
                  'bold',
                  'italic',
                  'heading',
                  'link',
                  'image',
                  'quote',
                  'conversation',
                ]"
                :key="kind"
                @click="insert(kind)"
              >
                {{ kind }}
              </button>
            </div>
            <textarea
              ref="bodyInput"
              v-model="form.content"
              class="writing-body"
              aria-label="Post content in Markdown"
              :placeholder="form.postType === 'photo' ? 'Add a caption…' : 'Write post here…'"
            />
          </div>
        </fieldset>
        <article v-else class="reading-preview">
          <h1>{{ form.title }}</h1>
          <img
            v-if="form.postType === 'book-review' && safeUrl(property('book-cover'))"
            :src="safeUrl(property('book-cover'))"
            :alt="property('book-cover-alt') || `Cover of ${property('book-title')}`"
          />
          <img
            v-if="form.postType === 'photo' && mediaUrl"
            :src="mediaUrl"
            :alt="property('alt')"
          /><audio v-if="form.postType === 'audio' && mediaUrl" :src="mediaUrl" controls /><video
            v-if="form.postType === 'video' && mediaUrl"
            :src="mediaUrl"
            controls
          />
          <p v-if="conversationError" class="writer-error" role="alert">{{ conversationError }}</p>
          <div v-html="readingPreview" />
          <dl v-if="config.fields.length" class="preview-properties">
            <template v-for="field in config.fields" :key="field.key"
              ><template v-if="property(field.key)"
                ><dt>{{ field.label }}</dt>
                <dd>{{ (form.properties[field.key] || []).join(', ') }}</dd></template
              ></template
            >
          </dl>
        </article>
        <div class="writing-below">
          <button @click="helpDialog?.showModal()">M↓ Markdown supported ↗</button
          ><span>{{ words }} words · {{ Math.max(1, Math.ceil(words / 220)) }} min read</span>
        </div>
        <p v-if="error" role="alert" class="writer-error">{{ error }}</p>
        <p v-if="post?.lastPublishError" class="writer-error">{{ post.lastPublishError }}</p>
      </main>
      <footer class="writer-footer">
        <span role="status" :class="{ 'writer-error': error }">{{
          error ||
          (uploading
            ? 'Uploading media…'
            : saving
              ? 'Saving…'
              : publishing
                ? 'Publishing…'
                : dirty
                  ? 'Unsaved changes'
                  : message || (post ? 'Draft saved' : 'Start a new draft'))
        }}</span>
        <div>
          <a
            v-if="safeUrl(post?.publishedUrl)"
            :href="safeUrl(post?.publishedUrl)"
            target="_blank"
            rel="noopener"
            >View published ↗</a
          ><button class="writer-secondary" :disabled="busy" @click="save()">
            Save draft <span class="save-shortcut">⌘ S</span></button
          ><button class="writer-primary" :disabled="busy" @click="review">
            {{ post?.publishedRevision ? 'Republish' : 'Publish' }} ↗
          </button>
        </div>
      </footer>
    </template>
    <dialog
      ref="detailsDialog"
      class="writer-dialog details-drawer"
      aria-labelledby="details-title"
    >
      <div class="dialog-heading">
        <h2 id="details-title">Post details</h2>
        <button aria-label="Close post details" @click="detailsDialog?.close()">×</button>
      </div>
      <fieldset :disabled="busy">
        <p class="field-help">The details can wait until the words are ready.</p>
        <label for="summary">Summary</label
        ><textarea id="summary" v-model="form.summary" rows="4" /><label for="categories"
          >Categories</label
        ><input id="categories" v-model="categoriesText" placeholder="writing, everyday life" />
        <p class="field-help">Separate categories with commas.</p>
        <label for="slug">Post URL</label
        ><input id="slug" v-model="form.slug" placeholder="Leave blank to generate a slug" />
        <p class="field-help">These details are included when you save.</p>
      </fieldset>
      <button class="writer-primary" @click="detailsDialog?.close()">Done</button>
    </dialog>
    <dialog
      ref="reviewDialog"
      class="writer-dialog"
      aria-labelledby="review-title"
      @cancel="busy && $event.preventDefault()"
    >
      <div class="dialog-heading">
        <h2 id="review-title">Ready to share?</h2>
        <button
          :disabled="busy"
          aria-label="Close publishing review"
          @click="reviewDialog?.close()"
        >
          ×
        </button>
      </div>
      <h3>{{ form.title || 'Untitled ' + config.label.toLowerCase() }}</h3>
      <p>{{ form.summary }}</p>
      <p class="field-help">{{ config.label }} · {{ categoriesText || 'No categories' }}</p>
      <p
        v-for="field in config.fields.filter((f) => property(f.key))"
        :key="field.key"
        class="review-property"
      >
        {{ field.label }}: {{ property(field.key) }}
      </p>
      <p v-if="error" class="writer-error" role="alert">{{ error }}</p>
      <div class="dialog-actions">
        <button :disabled="busy" class="writer-secondary" @click="reviewDialog?.close()">
          Keep writing</button
        ><button class="writer-primary" :disabled="busy" @click="publish">
          {{ publishing ? 'Publishing…' : saving ? 'Saving…' : 'Publish post ↗' }}
        </button>
      </div>
    </dialog>
    <dialog ref="helpDialog" class="writer-dialog" aria-labelledby="help-title">
      <div class="dialog-heading">
        <h2 id="help-title">Writing in Markdown</h2>
        <button aria-label="Close Markdown help" @click="helpDialog?.close()">×</button>
      </div>
      <p>Use the + beside your writing to insert formatting.</p>
      <pre>## Heading\n**bold** · *italic*\n[label](https://example.com)\n&gt; A quotation</pre>
      <p>
        Choose conversation from the + menu to insert an exchange. Each message needs a role: human,
        ai, or system. Add optional name and model parameters to identify the speaker, and title or
        note parameters to the conversation. Keep each shortcode on its own line and close every
        message and conversation.
      </p>
      <pre>{{ conversationTemplate }}</pre>
      <p class="field-help">
        The reading preview supports basic Markdown. Your original source is preserved for
        publishing.
      </p>
    </dialog>
  </div>
</template>
