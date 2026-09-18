<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { useDeskStore } from '@/stores/desk'
import { contentTypes } from '@/utils/contentTypes'
import type { PostType } from '@/types/admin'
const desk = useDeskStore()
const status = ref('all'),
  search = ref(''),
  type = ref<PostType | ''>('')
const posts = computed(() => desk.dashboard?.posts || [])
const filtered = computed(() =>
  posts.value.filter(
    (post) =>
      (!type.value || type.value === post.postType) &&
      (status.value === 'all' ||
        (status.value === 'published' ? !!post.publishedRevision : !post.publishedRevision)) &&
      `${post.title || ''} ${post.content} ${post.categories.join(' ')}`
        .toLowerCase()
        .includes(search.value.toLowerCase()),
  ),
)
onMounted(() => desk.refresh())
</script>
<template>
  <main class="writing-desk">
    <div class="desk-heading">
      <h1>Writing desk</h1>
      <RouterLink to="/posts/new" class="writer-primary">＋ New post</RouterLink>
    </div>
    <label class="desk-type"
      >Show
      <select v-model="type" aria-label="Filter by content type">
        <option value="">All content types</option>
        <option v-for="(config, key) in contentTypes" :key="key" :value="key">
          {{ config.label }}
        </option>
      </select></label
    >
    <div class="desk-toolbar">
      <div class="desk-tabs" role="group" aria-label="Filter posts">
        <button
          v-for="tab in [
            { id: 'all', label: 'All posts' },
            { id: 'draft', label: 'Drafts' },
            { id: 'published', label: 'Published' },
          ]"
          :key="tab.id"
          :aria-pressed="status === tab.id"
          @click="status = tab.id"
        >
          {{ tab.label }}
          <small>{{
            posts.filter(
              (p) =>
                tab.id === 'all' ||
                (tab.id === 'published' ? !!p.publishedRevision : !p.publishedRevision),
            ).length
          }}</small>
        </button>
      </div>
      <input v-model="search" type="search" aria-label="Search posts" placeholder="Find a post…" />
    </div>
    <p v-if="desk.loading" class="desk-message" role="status">Loading your writing…</p>
    <div v-else-if="desk.error" class="desk-message writer-error" role="alert">
      {{ desk.error }} <button @click="desk.refresh">Try again</button>
    </div>
    <template v-else
      ><article v-for="post in filtered" :key="post.id" class="desk-post">
        <div class="post-meta">
          <span :class="{ 'writer-error': post.state === 'publish-failed' }">{{
            post.state === 'publish-failed'
              ? 'Publish failed'
              : post.publishedRevision
                ? post.hasUnpublishedChanges
                  ? 'Unpublished changes'
                  : 'Published'
                : 'Draft'
          }}</span
          ><span>· {{ contentTypes[post.postType]?.label || post.postType }}</span
          ><time
            >·
            {{
              new Date(post.updatedUtc).toLocaleDateString(undefined, {
                day: 'numeric',
                month: 'short',
                year: 'numeric',
              })
            }}</time
          >
        </div>
        <h2>
          <RouterLink :to="`/posts/${post.id}`">{{
            post.title || post.content.slice(0, 100) || post.slug || 'Untitled post'
          }}</RouterLink>
        </h2>
        <p v-if="post.summary || post.content">
          {{ (post.summary || post.content).slice(0, 200) }}
        </p>
      </article>
      <p v-if="!filtered.length" class="desk-message">
        {{
          posts.length
            ? 'No posts match your search.'
            : 'Your next idea starts here. Create your first post, or import your writing from the profile menu.'
        }}
      </p>
      <p class="desk-count">
        {{ filtered.length }} {{ filtered.length === 1 ? 'post' : 'posts' }}
      </p></template
    >
  </main>
</template>
