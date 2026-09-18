<script setup lang="ts">
import { onBeforeUnmount, ref } from 'vue'
import api from '@/services/api'
import { safeUrl } from '@/utils/contentTypes'
interface Book {
  id: string
  title: string
  author: string
  coverUrl: string | null
  url: string
  isbn: string | null
  year: string | null
}
defineProps<{ disabled?: boolean }>()
const emit = defineEmits<{ select: [properties: Record<string, string[]>] }>()
const query = ref(''),
  results = ref<Book[]>([]),
  searching = ref(false),
  error = ref(''),
  searched = ref(false)
let controller: AbortController | undefined
onBeforeUnmount(() => controller?.abort())
async function search() {
  if (query.value.trim().length < 2 || searching.value) return
  controller = new AbortController()
  searching.value = true
  error.value = ''
  results.value = []
  searched.value = false
  try {
    const response = await api.get<Book[]>('/books/search', {
      params: { q: query.value.trim() },
      signal: controller.signal,
    })
    results.value = response.data
    searched.value = true
  } catch {
    if (!controller.signal.aborted)
      error.value =
        'Book search is unavailable. Try again shortly, or enter the details below manually.'
  } finally {
    searching.value = false
  }
}
function select(book: Book) {
  emit('select', {
    'book-title': [book.title],
    'book-author': [book.author],
    'book-cover': book.coverUrl ? [book.coverUrl] : [],
    'book-cover-alt': [`Cover of ${book.title} by ${book.author}`],
    'book-isbn': book.isbn ? [book.isbn] : [],
    'book-url': [book.url],
  })
  results.value = []
  searched.value = false
}
</script>
<template>
  <section class="book-search" aria-label="Find a book">
    <label for="book-search-query">Find a book on Open Library</label>
    <div class="search-controls">
      <input
        id="book-search-query"
        v-model="query"
        maxlength="200"
        placeholder="Title, author, or ISBN"
        :disabled="disabled"
        @keydown.enter.prevent="search"
      />
      <button
        type="button"
        class="writer-secondary"
        :disabled="disabled || searching || query.trim().length < 2"
        @click="search"
      >
        {{ searching ? 'Searching…' : 'Search' }}
      </button>
    </div>
    <p class="field-help">
      Or enter the details below manually. Search by ISBN to match your edition; title searches
      leave ISBN blank.
    </p>
    <p v-if="searching" role="status">Searching Open Library…</p>
    <p v-if="error" role="alert">{{ error }}</p>
    <p v-if="searched && !results.length" role="status">
      No books found. Try another search or enter your book manually.
    </p>
    <ul v-if="results.length" class="book-results">
      <li v-for="book in results" :key="book.id">
        <img
          v-if="safeUrl(book.coverUrl)"
          :src="safeUrl(book.coverUrl)"
          :alt="`Cover of ${book.title}`"
          width="40"
          height="60"
          loading="lazy"
          @error="($event.target as HTMLImageElement).hidden = true"
        />
        <span
          ><strong>{{ book.title }}</strong
          ><br />{{ book.author
          }}<small v-if="book.year">
            · {{ book.isbn ? 'Edition' : 'First published' }} {{ book.year }}</small
          ></span
        >
        <a :href="safeUrl(book.url)" target="_blank" rel="noopener noreferrer">Open Library ↗</a>
        <button type="button" class="writer-secondary" :disabled="disabled" @click="select(book)">
          Use this book
        </button>
      </li>
    </ul>
  </section>
</template>
<style scoped>
.book-search {
  margin-bottom: 1.5rem;
}
.search-controls {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}
.search-controls input {
  min-width: 0;
  flex: 1;
}
.book-results {
  list-style: none;
  padding: 0;
}
.book-results li {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.75rem;
  padding: 1rem 0;
  border-bottom: 1px solid #ddd;
}
.book-results li span {
  flex: 1;
  min-width: 12rem;
}
.book-results img {
  object-fit: contain;
}
.book-results a {
  font-size: 0.85rem;
}
</style>
