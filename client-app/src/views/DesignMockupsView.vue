<script setup lang="ts">
import { computed, ref } from 'vue'
import IdeaPanel from '@/components/IdeaPanel.vue'

type SectionKey = 'dashboard' | 'garden' | 'develop' | 'reader' | 'books' | 'pages' | 'settings'
type PanelTab = 'research' | 'controls'
type MaturityKey = 'seed' | 'seedling' | 'small-tree' | 'pohutakawa' | 'driftwood'

interface MaturityLevel {
  key: MaturityKey
  label: string
  icon: string
  tone: string
}

interface Idea {
  id: number
  title: string
  type: string
  maturity: MaturityKey
  summary: string
  folder: string
  filename: string
  status: string
}

type ReaderView = 'today' | 'readLater' | 'bookmarks' | 'sites'

interface ReaderPost {
  id: number
  blog: string
  category: string
  title: string
  excerpt: string
  read: boolean
  readLater: boolean
  starred: boolean
  thought: string
  content: string[]
}

interface ReaderNote {
  id: number
  postTitle: string
  tag: string
  quote: string
  note: string
}

const sections: Array<{ key: SectionKey; label: string; hint: string; count: string }> = [
  { key: 'dashboard', label: 'Dashboard', hint: 'Jumping off point', count: '' },
  { key: 'garden', label: 'Garden', hint: 'Ideas in progress', count: '12' },
  { key: 'develop', label: 'Develop', hint: 'Focused writing', count: '1' },
  { key: 'reader', label: 'Reader', hint: 'Blogs and bookmarks', count: '38' },
  { key: 'books', label: 'Books', hint: 'Reading notes', count: '7' },
  { key: 'pages', label: 'Pages', hint: 'One-off pages', count: '5' },
  { key: 'settings', label: 'Settings', hint: 'Config and health', count: '' },
]

const maturityLevels: MaturityLevel[] = [
  { key: 'seed', label: 'Seed', icon: 'S', tone: 'bg-[#fff7d6] text-[#745a00]' },
  { key: 'seedling', label: 'Seedling', icon: 'G', tone: 'bg-[#e6f5e9] text-[#21613a]' },
  { key: 'small-tree', label: 'Small tree', icon: 'T', tone: 'bg-[#e4f0ff] text-[#23517a]' },
  { key: 'pohutakawa', label: 'Pohutakawa in full bloom', icon: 'P', tone: 'bg-[#ffe7e8] text-[#a5343b]' },
  { key: 'driftwood', label: 'Driftwood', icon: 'D', tone: 'bg-[#eee9df] text-[#6e6555]' },
]

const ideas = ref<Idea[]>([
  {
    id: 1,
    title: 'The gentle pressure of a small weblog',
    type: 'Post',
    maturity: 'pohutakawa',
    summary: 'A nearly-ready essay about publishing as a quiet habit instead of a launch event.',
    folder: '/posts',
    filename: 'small-weblog.md',
    status: 'Draft',
  },
  {
    id: 2,
    title: 'The reader as compost heap',
    type: 'Post',
    maturity: 'small-tree',
    summary: 'Reader, bookmarks, notes, and essays as one loop: gather, tend, publish, revisit.',
    folder: '/posts',
    filename: 'reader-compost-heap.md',
    status: 'Developing',
  },
  {
    id: 3,
    title: 'Durable links deserve better rituals',
    type: 'Note',
    maturity: 'seedling',
    summary: 'A short note about link saving, context, and why a bookmark without a thought goes stale.',
    folder: '/notes',
    filename: 'durable-links.md',
    status: 'Draft',
  },
  {
    id: 4,
    title: 'Inbox zero was never the point',
    type: 'Post',
    maturity: 'driftwood',
    summary: 'Old productivity idea that might become a footnote rather than a full post.',
    folder: '/archive',
    filename: 'inbox-zero.md',
    status: 'Parked',
  },
  {
    id: 5,
    title: 'A review is a memory palace',
    type: 'Book review',
    maturity: 'seed',
    summary: 'Tiny idea about book reviews as a map of what the reader became while reading.',
    folder: '/reviews/books',
    filename: 'review-memory-palace.md',
    status: 'Seed',
  },
])

const researchLinks = ref([
  { title: 'Svbtle dashboard notes', url: 'svbtle.com/dashboard' },
  { title: 'Base Web principles', url: 'uber.com/baseweb' },
  { title: 'Old post: Publishing without ceremony', url: '/posts/publishing-without-ceremony' },
])

const attachments = ref(['rough-outline.txt', 'homepage-sketch.png', 'quote-bank.md'])
const readerPosts = ref<ReaderPost[]>([
  {
    id: 1,
    blog: 'Maggie Appleton',
    category: 'Digital gardens',
    title: 'Garden histories and digital homes',
    excerpt: 'A thoughtful essay on tending public notes, maps, and traces of learning over time.',
    read: false,
    readLater: true,
    starred: true,
    thought: 'Useful framing for the Garden.',
    content: [
      'Digital gardens work best when they are allowed to show the intermediate state of thought. A note can be useful before it becomes polished.',
      'The useful part is not the garden metaphor by itself, but the repeated act of tending. Links, quotes, and private notes gather until a shape starts to appear.',
      'A publishing tool can make that transition feel gentle: from reader, to bookmark, to note, to draft, to published piece.',
    ],
  },
  {
    id: 2,
    blog: 'Robin Sloan',
    category: 'Small web',
    title: 'A note on small software',
    excerpt: 'Small tools can create a different tempo: personal, legible, and easy to repair.',
    read: true,
    readLater: false,
    starred: false,
    thought: '',
    content: [
      'Small software creates a different relationship between the person and the machine. It is legible enough to be changed by the person who uses it.',
      'The trick is to keep the tool quiet. The best personal software does not ask to become a platform.',
      'A weblog is one of the oldest examples of this: a tool with a narrow surface and an unusually large emotional range.',
    ],
  },
  {
    id: 3,
    blog: 'Simon Willison',
    category: 'Technology',
    title: 'Notes on personal publishing tools',
    excerpt: 'A practical walkthrough of publishing workflows, feeds, and keeping personal tools boring.',
    read: false,
    readLater: false,
    starred: false,
    thought: 'Check Micropub overlap.',
    content: [
      'Publishing workflows become easier to trust when each step is visible. Feeds, slugs, files, and commits all benefit from boring transparency.',
      'A personal publishing system should make the happy path fast while keeping the repair path obvious.',
      'The reader can be more than consumption. It can become the top of the writing funnel.',
    ],
  },
  {
    id: 4,
    blog: 'Austin Kleon',
    category: 'Creative practice',
    title: 'Keeping a notebook that talks back',
    excerpt: 'A short piece about rereading, copying, clipping, and turning scraps into work.',
    read: false,
    readLater: false,
    starred: false,
    thought: '',
    content: [
      'A notebook that talks back is one that can surprise you with your own earlier attention.',
      'The important thing is not capture. It is return. A saved clipping needs a place to become useful again.',
      'The best notes are not final thoughts. They are invitations to keep thinking.',
    ],
  },
])
const books = [
  { title: 'The Creative Act', author: 'Rick Rubin', state: 'Review draft', rating: '4.5', note: 'Good language for creative practice without turning it into productivity.' },
  { title: 'How Buildings Learn', author: 'Stewart Brand', state: 'Reading', rating: '5', note: 'Useful analogy for software that changes slowly over time.' },
  { title: 'A Swim in a Pond in the Rain', author: 'George Saunders', state: 'Read', rating: '4', note: 'Great for thinking about paragraph rhythm.' },
]
const pages = [
  { title: 'About', path: '/about', style: 'Classic essay page', updated: 'Yesterday' },
  { title: 'Now', path: '/now', style: 'Short status page', updated: '24 May' },
  { title: 'Uses', path: '/uses', style: 'Dense tool list', updated: '12 May' },
]
const healthChecks = [
  { name: 'Storage', detail: 'Table storage reachable', ok: true },
  { name: 'GitHub', detail: 'Push token valid', ok: true },
  { name: 'Website', detail: 'Last publish finished in 18s', ok: true },
  { name: 'RSS', detail: 'Reader import paused until feeds are configured', ok: false },
]

const activeSection = ref<SectionKey>('dashboard')
const selectedIdeaId = ref(ideas.value[0]!.id)
const panelOpen = ref(true)
const panelTab = ref<PanelTab>('research')
const activeReaderView = ref<ReaderView>('today')
const activeReaderCategory = ref('All')
const selectedReaderPostId = ref<number | null>(null)
const selectedReaderQuote = ref('')
const readerNote = ref('')
const readerNoteTag = ref('Source note')
const readerNotes = ref<ReaderNote[]>([
  {
    id: 1,
    postTitle: 'Garden histories and digital homes',
    tag: 'Garden',
    quote: 'A note can be useful before it becomes polished.',
    note: 'This maps directly to maturity levels in the Garden.',
  },
])
const ideaTitle = ref('')
const dropNotice = ref('')
const oneOffLink = ref('https://example.com/thoughtful-post')

const selectedIdea = computed<Idea>(() => ideas.value.find((idea) => idea.id === selectedIdeaId.value) ?? ideas.value[0]!)
const selectedMaturity = computed<MaturityLevel>(() => maturityLevels.find((level) => level.key === selectedIdea.value.maturity) ?? maturityLevels[0]!)
const starredPosts = computed(() => readerPosts.value.filter((post) => post.starred))
const selectedReaderPost = computed(() => readerPosts.value.find((post) => post.id === selectedReaderPostId.value) ?? readerPosts.value[0]!)
const readerCategories = computed(() => {
  const categories = readerPosts.value.reduce<Record<string, number>>((groups, post) => {
    groups[post.category] = (groups[post.category] ?? 0) + 1
    return groups
  }, {})

  return [
    { name: 'All', count: readerPosts.value.length },
    ...Object.entries(categories).map(([name, count]) => ({ name, count })),
  ]
})
const visibleReaderPosts = computed(() => {
  return readerPosts.value.filter((post) => {
    const viewMatch =
      activeReaderView.value === 'sites' ||
      activeReaderView.value === 'today' ||
      (activeReaderView.value === 'readLater' && post.readLater) ||
      (activeReaderView.value === 'bookmarks' && post.starred)
    const categoryMatch = activeReaderCategory.value === 'All' || post.category === activeReaderCategory.value

    return viewMatch && categoryMatch
  })
})

function setSection(section: SectionKey) {
  activeSection.value = section
  if (section === 'garden') panelOpen.value = true
  if (section !== 'reader') selectedReaderPostId.value = null
}

function backToDashboard() {
  activeSection.value = 'dashboard'
  selectedReaderPostId.value = null
  selectedReaderQuote.value = ''
}

function selectIdea(id: number) {
  selectedIdeaId.value = id
  panelOpen.value = true
  panelTab.value = 'research'
}

function createIdea() {
  const title = ideaTitle.value.trim()
  if (!title) return

  const nextId = Math.max(...ideas.value.map((idea) => idea.id)) + 1
  ideas.value.unshift({
    id: nextId,
    title,
    type: 'Post',
    maturity: 'seed',
    summary: 'Freshly planted. Add notes, links, and scraps until it starts to take shape.',
    folder: '/posts',
    filename: `${title.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'untitled'}.md`,
    status: 'Seed',
  })
  selectedIdeaId.value = nextId
  ideaTitle.value = ''
  activeSection.value = 'garden'
  panelOpen.value = true
}

function openEditor(id = selectedIdea.value.id) {
  selectedIdeaId.value = id
  activeSection.value = 'develop'
  panelOpen.value = true
}

function updateMaturity(value: MaturityKey) {
  selectedIdea.value.maturity = value
}

function toggleStar(id: number) {
  const post = readerPosts.value.find((item) => item.id === id)
  if (post) post.starred = !post.starred
}

function toggleRead(id: number) {
  const post = readerPosts.value.find((item) => item.id === id)
  if (post) post.read = !post.read
}

function toggleReadLater(id: number) {
  const post = readerPosts.value.find((item) => item.id === id)
  if (post) post.readLater = !post.readLater
}

function openReaderPost(post: ReaderPost) {
  post.read = true
  selectedReaderPostId.value = post.id
  selectedReaderQuote.value = ''
  readerNote.value = ''
}

function closeReaderPost() {
  selectedReaderPostId.value = null
  selectedReaderQuote.value = ''
}

function snipReaderSection(section: string) {
  selectedReaderQuote.value = section
  readerNote.value = ''
}

function saveReaderNote() {
  const note = readerNote.value.trim()
  if (!note && !selectedReaderQuote.value) return

  const nextId = Math.max(0, ...readerNotes.value.map((item) => item.id)) + 1
  readerNotes.value.unshift({
    id: nextId,
    postTitle: selectedReaderPost.value.title,
    tag: readerNoteTag.value,
    quote: selectedReaderQuote.value,
    note: note || 'Saved section for later.',
  })
  readerNote.value = ''
}

function startIdeaFromPost(post: ReaderPost, quote = '', note = '') {
  const nextId = Math.max(...ideas.value.map((idea) => idea.id)) + 1
  ideas.value.unshift({
    id: nextId,
    title: post.title,
    type: 'Post',
    maturity: 'seed',
    summary: quote ? `Started from ${post.blog}: "${quote}"` : `Started from ${post.blog}: ${note || post.excerpt}`,
    folder: '/posts',
    filename: `${post.title.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'reader-idea'}.md`,
    status: 'Seed',
  })
  researchLinks.value.unshift({ title: post.title, url: post.blog })
  selectedIdeaId.value = nextId
  activeSection.value = 'garden'
  selectedReaderPostId.value = null
  panelOpen.value = true
}

function startIdeaFromReaderNote(note?: ReaderNote) {
  startIdeaFromPost(selectedReaderPost.value, note?.quote ?? selectedReaderQuote.value, note?.note ?? readerNote.value)
}

function addOneOffBookmark() {
  const nextId = Math.max(...readerPosts.value.map((post) => post.id)) + 1
  readerPosts.value.unshift({
    id: nextId,
    blog: 'One-off link',
    category: 'Bookmarks',
    title: oneOffLink.value,
    excerpt: 'Saved directly as a bookmark.',
    read: false,
    readLater: false,
    starred: true,
    thought: 'Saved directly as a bookmark.',
    content: [
      'This link was saved directly into bookmarks.',
      'Add notes after opening it in the reader detail view.',
    ],
  })
  oneOffLink.value = ''
}

function handleDrop(event: DragEvent) {
  const count = event.dataTransfer?.files.length ?? 0
  dropNotice.value = count ? `${count} file attached to ${selectedIdea.value.title}` : 'Dropped text attached to the idea'
}
</script>

<template>
  <main class="min-h-screen bg-[#f7f6f2] text-[#151515]">
    <div class="min-h-screen">
      <section class="mx-auto min-w-0 max-w-[96rem]">
        <div v-if="activeSection === 'dashboard'" class="p-5 sm:p-8">
          <section class="rounded-lg border border-black/10 bg-white p-6 shadow-[0_18px_50px_rgba(20,20,20,0.06)] sm:p-8">
            <div class="mb-10 flex items-center justify-between gap-4">
              <div>
                <p class="text-xl font-black">LittlePublisher</p>
                <p class="mt-1 text-sm text-[#77736b]">michael.example</p>
              </div>
              <span class="rounded-full border border-black/10 px-3 py-1 text-xs font-bold text-[#77736b]">Concept</span>
            </div>

            <div class="flex flex-col gap-4 border-b border-black/10 pb-8 lg:flex-row lg:items-end lg:justify-between">
              <div>
                <p class="text-sm text-[#77736b]">Today</p>
                <h2 class="mt-2 max-w-3xl text-5xl font-black leading-[0.95]">What needs tending before the next publish?</h2>
              </div>
              <button type="button" class="w-fit rounded-full bg-black px-5 py-3 text-sm font-bold text-white" @click="activeSection = 'garden'">Open garden</button>
            </div>

            <div class="mt-8 grid gap-px overflow-hidden rounded-lg border border-black/10 bg-black/10 sm:grid-cols-2 xl:grid-cols-4">
              <button
                v-for="section in sections.filter((item) => item.key !== 'dashboard')"
                :key="section.key"
                type="button"
                class="bg-white p-6 text-left transition hover:bg-[#fbfaf7] focus:outline-none focus:ring-2 focus:ring-black/20"
                @click="setSection(section.key)"
              >
                <div class="flex items-start justify-between gap-3">
                  <h3 class="text-2xl font-black">{{ section.label }}</h3>
                  <span class="rounded-full border border-black/10 px-3 py-1 text-xs text-[#77736b]">{{ section.count || 'OK' }}</span>
                </div>
                <p class="mt-8 text-sm leading-6 text-[#706c63]">{{ section.hint }}</p>
              </button>
            </div>
          </section>
        </div>

        <div v-else-if="activeSection === 'garden'" class="grid min-h-[calc(100vh-7rem)] lg:grid-cols-[minmax(0,1fr)_24rem]">
          <section class="p-5 sm:p-8">
            <button type="button" class="mb-5 rounded-full border border-black/15 bg-white px-4 py-2 text-sm font-bold text-[#4b4944] hover:border-black" @click="backToDashboard">Back to dashboard</button>
            <div class="rounded-lg border border-black/10 bg-white shadow-[0_18px_50px_rgba(20,20,20,0.06)]">
              <div class="border-b border-black/10 p-6 sm:p-8">
                <p class="text-sm font-semibold uppercase tracking-[0.18em] text-[#8b887f]">The garden</p>
                <h2 class="mt-2 text-4xl font-black">Ideas are cultivated here.</h2>
                <form class="mt-6 flex flex-col gap-3 sm:flex-row" @submit.prevent="createIdea">
                  <input v-model="ideaTitle" class="min-h-14 flex-1 rounded-md border border-black/10 bg-[#fbfaf7] px-5 text-lg outline-none focus:border-black" placeholder="Start typing to plant an idea..." />
                  <button type="submit" class="rounded-md bg-black px-6 py-4 text-sm font-bold text-white">Plant</button>
                </form>
              </div>

              <div class="divide-y divide-black/10">
                <button
                  v-for="idea in ideas"
                  :key="idea.id"
                  type="button"
                  class="grid w-full gap-4 p-5 text-left transition hover:bg-[#fbfaf7] sm:grid-cols-[4rem_minmax(0,1fr)_8rem] sm:p-6"
                  :class="selectedIdea.id === idea.id ? 'bg-[#fffafa]' : ''"
                  @click="selectIdea(idea.id)"
                >
                  <span class="flex h-14 w-14 items-center justify-center rounded-md text-lg font-black" :class="maturityLevels.find((level) => level.key === idea.maturity)?.tone">
                    {{ maturityLevels.find((level) => level.key === idea.maturity)?.icon }}
                  </span>
                  <span class="min-w-0">
                    <span class="block text-2xl font-black">{{ idea.title }}</span>
                    <span class="mt-2 block text-sm leading-6 text-[#706c63]">{{ idea.summary }}</span>
                    <span class="mt-3 block text-xs font-semibold uppercase tracking-[0.14em] text-[#8b887f]">{{ idea.type }} / {{ maturityLevels.find((level) => level.key === idea.maturity)?.label }}</span>
                  </span>
                  <span class="self-start rounded-full border border-black/10 px-3 py-2 text-center text-xs font-semibold text-[#706c63]">{{ idea.status }}</span>
                </button>
              </div>
            </div>
          </section>

          <aside v-if="panelOpen" class="border-t border-black/10 bg-[#fbfaf7] p-5 sm:p-8 lg:border-l lg:border-t-0">
            <div class="mb-5 hidden h-10 lg:block"></div>
            <div class="sticky top-6">
              <IdeaPanel
                :attachments="attachments"
                :drop-notice="dropNotice"
                :maturity-levels="maturityLevels"
                :panel-tab="panelTab"
                :research-links="researchLinks"
                :selected-idea="selectedIdea"
                :selected-maturity="selectedMaturity"
                @drop-file="handleDrop"
                @set-maturity="updateMaturity"
                @set-tab="panelTab = $event"
              />
              <button
                type="button"
                class="mt-4 w-full rounded-md bg-black px-5 py-3 text-sm font-bold text-white"
                @click="openEditor()"
              >
                Cultivate
              </button>
            </div>
          </aside>
        </div>

        <div v-else-if="activeSection === 'develop'" class="grid min-h-[calc(100vh-7rem)] lg:grid-cols-[minmax(0,1fr)_24rem]">
          <section class="p-5 sm:p-8">
            <div class="mb-5 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
              <div class="flex flex-wrap gap-2">
                <button type="button" class="w-fit rounded-full border border-black/15 bg-white px-4 py-2 text-sm font-bold text-black hover:border-black" @click="activeSection = 'garden'">Back to garden</button>
                <button type="button" class="w-fit rounded-full border border-black/15 bg-white px-4 py-2 text-sm font-bold text-[#4b4944] hover:border-black" @click="backToDashboard">Back to dashboard</button>
              </div>
              <div class="flex gap-2">
                <button type="button" class="rounded-full border border-black/15 bg-white px-4 py-2 text-sm font-semibold">Preview</button>
                <button type="button" class="rounded-full bg-[#ff5a5f] px-4 py-2 text-sm font-bold text-white shadow-[0_12px_28px_rgba(255,90,95,0.22)]">Publish</button>
              </div>
            </div>

            <div class="rounded-lg border border-black/10 bg-white shadow-[0_18px_50px_rgba(20,20,20,0.06)]">
              <article class="mx-auto max-w-3xl px-6 py-12 sm:px-8">
                <input class="w-full border-0 bg-transparent text-5xl font-black leading-none outline-none placeholder:text-[#d2cec5] sm:text-7xl" :value="selectedIdea.title" aria-label="Title" />
                <div class="mt-8 flex flex-wrap gap-2 text-xs font-medium text-[#77736b]">
                  <span class="rounded-full border border-black/10 px-3 py-1">{{ selectedIdea.status }}</span>
                  <span class="rounded-full border border-black/10 px-3 py-1">{{ selectedMaturity.label }}</span>
                  <span class="rounded-full border border-black/10 px-3 py-1">{{ selectedIdea.folder }}/{{ selectedIdea.filename }}</span>
                </div>
                <textarea class="mt-12 min-h-[30rem] w-full resize-none border-0 bg-transparent text-xl leading-9 text-[#252525] outline-none" :value="`Publishing should feel like lowering a note into the world, not launching software.\n\nThis is the focused writing canvas. Research stays close, but the page does not carry the full dashboard navigation.\n\nA bookmark can become a note, a review can become a post, and a project update can become an activity entry.`" />
              </article>
            </div>
          </section>

          <aside class="border-t border-black/10 bg-[#fbfaf7] p-5 sm:p-8 lg:border-l lg:border-t-0">
            <div class="mb-5 hidden h-10 lg:block"></div>
            <div class="sticky top-6">
              <IdeaPanel
                :attachments="attachments"
                :drop-notice="dropNotice"
                :maturity-levels="maturityLevels"
                :panel-tab="panelTab"
                :research-links="researchLinks"
                :selected-idea="selectedIdea"
                :selected-maturity="selectedMaturity"
                permanent
                @drop-file="handleDrop"
                @set-maturity="updateMaturity"
                @set-tab="panelTab = $event"
              />
              <button
                type="button"
                class="mt-4 w-full rounded-md bg-black px-5 py-3 text-sm font-bold text-white"
                @click="openEditor()"
              >
                Cultivate
              </button>
            </div>
          </aside>
        </div>

        <div v-else-if="activeSection === 'reader'" class="p-5 sm:p-8">
          <button type="button" class="mb-5 rounded-full border border-black/15 bg-white px-4 py-2 text-sm font-bold text-[#4b4944] hover:border-black" @click="backToDashboard">Back to dashboard</button>
          <section class="overflow-hidden rounded-lg border border-black/10 bg-white shadow-[0_18px_50px_rgba(20,20,20,0.06)]">
            <div class="grid min-h-[46rem] xl:grid-cols-[18rem_minmax(0,1fr)]">
              <aside class="border-b border-black/10 bg-[#fbfaf7] p-5 xl:border-b-0 xl:border-r">
                <div class="space-y-1">
                  <button
                    type="button"
                    class="flex w-full items-center justify-between rounded-md px-4 py-3 text-left font-bold"
                    :class="activeReaderView === 'today' ? 'bg-[#e7f7e9] text-[#23834b]' : 'text-[#4b4944] hover:bg-white'"
                    @click="activeReaderView = 'today'"
                  >
                    <span>Today</span>
                    <span>{{ readerPosts.filter((post) => !post.read).length }}</span>
                  </button>
                  <button
                    type="button"
                    class="flex w-full items-center justify-between rounded-md px-4 py-3 text-left font-bold"
                    :class="activeReaderView === 'readLater' ? 'bg-[#e7f7e9] text-[#23834b]' : 'text-[#4b4944] hover:bg-white'"
                    @click="activeReaderView = 'readLater'"
                  >
                    <span>Read later</span>
                    <span>{{ readerPosts.filter((post) => post.readLater).length }}</span>
                  </button>
                  <button
                    type="button"
                    class="flex w-full items-center justify-between rounded-md px-4 py-3 text-left font-bold"
                    :class="activeReaderView === 'bookmarks' ? 'bg-[#e7f7e9] text-[#23834b]' : 'text-[#4b4944] hover:bg-white'"
                    @click="activeReaderView = 'bookmarks'"
                  >
                    <span>Bookmarks</span>
                    <span>{{ readerPosts.filter((post) => post.starred).length }}</span>
                  </button>
                  <button
                    type="button"
                    class="flex w-full items-center justify-between rounded-md px-4 py-3 text-left font-bold"
                    :class="activeReaderView === 'sites' ? 'bg-[#e7f7e9] text-[#23834b]' : 'text-[#4b4944] hover:bg-white'"
                    @click="activeReaderView = 'sites'"
                  >
                    <span>Tracked sites</span>
                    <span>{{ readerPosts.length }}</span>
                  </button>
                </div>

                <div class="mt-8">
                  <p class="px-4 text-xs font-black uppercase tracking-[0.18em] text-[#aaa59b]">Feeds</p>
                  <div class="mt-3 space-y-2">
                    <button
                      v-for="category in readerCategories"
                      :key="category.name"
                      type="button"
                      class="flex w-full items-center justify-between rounded-md px-4 py-3 text-left"
                      :class="activeReaderCategory === category.name ? 'bg-white text-black ring-1 ring-black/10' : 'text-[#706c63] hover:bg-white'"
                      @click="activeReaderCategory = category.name"
                    >
                      <span class="font-bold">{{ category.name }}</span>
                      <span class="text-sm text-[#9a968d]">{{ category.count }}</span>
                    </button>
                  </div>
                </div>

                <button type="button" class="mt-8 w-full rounded-md bg-black px-4 py-3 text-sm font-bold text-white" @click="activeReaderView = 'sites'">Add or manage feeds</button>
              </aside>

              <div class="p-6 sm:p-8">
                <div class="border-b border-black/10 pb-5">
                  <div class="flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
                    <div>
                      <h2 class="text-4xl font-black">
                        {{ selectedReaderPostId ? selectedReaderPost.blog : activeReaderView === 'today' ? 'Today' : activeReaderView === 'readLater' ? 'Read later' : activeReaderView === 'sites' ? 'Tracked sites' : 'Bookmarks' }}
                      </h2>
                      <p class="mt-2 text-sm text-[#706c63]">
                        {{ selectedReaderPostId ? selectedReaderPost.category : activeReaderView === 'sites' ? 'Manage feed URLs, categories, blog roll status, and sync health.' : `${visibleReaderPosts.length} items from ${activeReaderCategory}` }}
                      </p>
                    </div>
                    <button v-if="selectedReaderPostId" type="button" class="rounded-full border border-black/15 px-4 py-2 text-sm font-bold text-[#4b4944] hover:border-black" @click="closeReaderPost">Back to reader</button>
                  </div>
                </div>

                <div v-if="selectedReaderPostId && activeReaderView !== 'sites'" class="grid gap-8 pt-7 xl:grid-cols-[minmax(0,1fr)_20rem]">
                  <article class="mx-auto max-w-3xl">
                    <div v-if="selectedReaderQuote" class="mb-8 border-l-4 border-black bg-[#fbfaf7] p-5">
                      <p class="text-xs font-black uppercase tracking-[0.16em] text-[#8b887f]">Selected section</p>
                      <p class="mt-3 text-xl font-semibold leading-8">"{{ selectedReaderQuote }}"</p>
                    </div>
                    <h3 class="text-5xl font-black leading-none">{{ selectedReaderPost.title }}</h3>
                    <p class="mt-4 text-lg text-[#706c63]">{{ selectedReaderPost.excerpt }}</p>
                    <div class="mt-10 space-y-8 text-xl leading-9 text-[#252525]">
                      <section v-for="section in selectedReaderPost.content" :key="section" class="group border-b border-black/10 pb-8">
                        <p>{{ section }}</p>
                        <button type="button" class="mt-4 text-sm font-bold text-[#23834b] opacity-80 group-hover:opacity-100" @click="snipReaderSection(section)">Snip this section</button>
                      </section>
                    </div>
                  </article>

                  <aside class="border-t border-black/10 pt-6 xl:border-l xl:border-t-0 xl:pl-6">
                    <h3 class="text-2xl font-black">Reader notes</h3>
                    <p class="mt-2 text-sm leading-6 text-[#706c63]">Tag a section, leave a free note, or turn either into a Garden idea.</p>
                    <label class="mt-5 block">
                      <span class="text-xs font-black uppercase tracking-[0.14em] text-[#8b887f]">Tag</span>
                      <select v-model="readerNoteTag" class="mt-2 w-full rounded-md border border-black/10 bg-[#fbfaf7] px-3 py-2 text-sm">
                        <option>Source note</option>
                        <option>Quote</option>
                        <option>Question</option>
                        <option>Follow up</option>
                      </select>
                    </label>
                    <label class="mt-4 block">
                      <span class="text-xs font-black uppercase tracking-[0.14em] text-[#8b887f]">Note</span>
                      <textarea v-model="readerNote" class="mt-2 min-h-32 w-full rounded-md border border-black/10 bg-[#fbfaf7] p-3 text-sm outline-none focus:border-black" placeholder="Write a note with or without a selected section..." />
                    </label>
                    <div class="mt-4 grid gap-2">
                      <button type="button" class="rounded-md border border-black/15 px-4 py-3 text-sm font-bold" @click="saveReaderNote">Save note</button>
                      <button type="button" class="rounded-md bg-black px-4 py-3 text-sm font-bold text-white" @click="startIdeaFromReaderNote()">Convert note to idea</button>
                    </div>
                    <div class="mt-6 space-y-3">
                      <article v-for="note in readerNotes.filter((item) => item.postTitle === selectedReaderPost.title)" :key="note.id" class="rounded-md border border-black/10 bg-white p-4">
                        <p class="text-xs font-black uppercase tracking-[0.14em] text-[#8b887f]">{{ note.tag }}</p>
                        <p v-if="note.quote" class="mt-2 text-sm font-semibold">"{{ note.quote }}"</p>
                        <p class="mt-2 text-sm text-[#706c63]">{{ note.note }}</p>
                        <button type="button" class="mt-3 text-xs font-bold text-[#23834b]" @click="startIdeaFromReaderNote(note)">Make idea</button>
                      </article>
                    </div>
                  </aside>
                </div>

                <div v-else-if="activeReaderView === 'sites'" class="pt-7">
                  <form class="flex flex-col gap-3 border-b border-black/10 pb-7 sm:flex-row" @submit.prevent>
                    <input class="min-h-12 min-w-0 flex-1 rounded-md border border-black/10 bg-[#fbfaf7] px-4 text-sm outline-none focus:border-black" placeholder="https://example.com/feed.xml" />
                    <button type="submit" class="rounded-md bg-black px-5 py-3 text-sm font-bold text-white">Add feed URL</button>
                  </form>

                  <div class="mt-7 divide-y divide-black/10">
                    <article v-for="post in readerPosts" :key="`managed-${post.blog}`" class="grid gap-4 py-5 md:grid-cols-[minmax(0,1fr)_10rem_10rem_7rem] md:items-center">
                      <div class="min-w-0">
                        <p class="truncate text-xl font-black">{{ post.blog }}</p>
                        <p class="mt-1 text-sm text-[#706c63]">{{ post.category }}</p>
                      </div>
                      <label class="block">
                        <span class="text-xs font-black uppercase tracking-[0.14em] text-[#8b887f]">Category</span>
                        <select class="mt-2 w-full rounded-md border border-black/10 bg-[#fbfaf7] px-3 py-2 text-sm">
                          <option>{{ post.category }}</option>
                          <option>Small web</option>
                          <option>Technology</option>
                          <option>Creative practice</option>
                        </select>
                      </label>
                      <div class="flex flex-wrap gap-2 text-xs font-bold md:justify-end">
                        <button type="button" class="rounded-full border border-black/15 px-3 py-2" :class="post.starred ? 'bg-[#e7f7e9] text-[#23834b]' : 'text-[#706c63]'">Blog roll</button>
                        <button type="button" class="rounded-full border border-black/15 px-3 py-2 text-[#706c63]">Pause</button>
                      </div>
                      <span class="flex items-center gap-2 text-sm font-bold" :class="post.read ? 'text-[#8b887f]' : 'text-[#23834b]'">
                        <span class="h-2.5 w-2.5 rounded-full" :class="post.read ? 'bg-[#c8c5bc]' : 'bg-[#35c759]'"></span>
                        {{ post.read ? 'Quiet' : 'Active' }}
                      </span>
                    </article>
                  </div>
                </div>

                <div v-else class="divide-y divide-black/10">
                  <article
                    v-for="post in visibleReaderPosts"
                    :key="post.id"
                    class="grid cursor-pointer gap-5 py-7 transition hover:bg-[#fbfaf7] md:grid-cols-[8.5rem_minmax(0,1fr)_5rem]"
                    @click="openReaderPost(post)"
                  >
                    <div class="flex h-24 items-center justify-center rounded-md bg-[#eef1ed] text-3xl font-black text-[#778071]">
                      {{ post.blog.slice(0, 1) }}
                    </div>
                    <div class="min-w-0">
                      <p class="text-sm font-semibold text-[#8b887f]">{{ post.blog }} / {{ post.category }}</p>
                      <h3 class="mt-1 text-2xl font-black leading-tight">{{ post.title }}</h3>
                      <p class="mt-4 max-w-3xl text-lg leading-7 text-[#706c63]">{{ post.excerpt }}</p>
                    </div>
                    <div class="flex items-start justify-end gap-2">
                      <span v-if="!post.read" class="mt-3 h-2.5 w-2.5 rounded-full bg-[#35c759]" aria-label="Unread"></span>
                      <button type="button" class="rounded-full border border-black/15 px-3 py-2 text-xs font-bold" :class="post.readLater ? 'bg-[#e7f7e9] text-[#23834b]' : 'text-[#706c63]'" aria-label="Save for later" @click.stop="toggleReadLater(post.id)">L</button>
                      <button type="button" class="rounded-full px-3 py-2 text-xs font-bold" :class="post.starred ? 'bg-[#ff5a5f] text-white' : 'border border-black/15 text-[#706c63]'" aria-label="Bookmark" @click.stop="toggleStar(post.id)">B</button>
                    </div>
                  </article>
                </div>
              </div>
            </div>
          </section>
        </div>

        <div v-else-if="activeSection === 'books'" class="p-5 sm:p-8">
          <button type="button" class="mb-5 rounded-full border border-black/15 bg-white px-4 py-2 text-sm font-bold text-[#4b4944] hover:border-black" @click="backToDashboard">Back to dashboard</button>
          <section class="rounded-lg border border-black/10 bg-white p-6 shadow-[0_18px_50px_rgba(20,20,20,0.06)] sm:p-8">
            <h2 class="text-4xl font-black">Books</h2>
            <div class="mt-8 grid gap-4 lg:grid-cols-3">
              <article v-for="book in books" :key="book.title" class="rounded-lg border border-black/10 bg-[#fbfaf7] p-5">
                <p class="text-xs font-bold uppercase tracking-[0.16em] text-[#8b887f]">{{ book.state }}</p>
                <h3 class="mt-3 text-2xl font-black">{{ book.title }}</h3>
                <p class="mt-1 text-sm text-[#706c63]">{{ book.author }}</p>
                <p class="mt-5 text-sm leading-6 text-[#4b4944]">{{ book.note }}</p>
                <button type="button" class="mt-5 rounded-full bg-black px-4 py-2 text-sm font-bold text-white">Write review</button>
              </article>
            </div>
          </section>
        </div>

        <div v-else-if="activeSection === 'pages'" class="p-5 sm:p-8">
          <button type="button" class="mb-5 rounded-full border border-black/15 bg-white px-4 py-2 text-sm font-bold text-[#4b4944] hover:border-black" @click="backToDashboard">Back to dashboard</button>
          <section class="grid gap-6 xl:grid-cols-[22rem_minmax(0,1fr)]">
            <aside class="rounded-lg border border-black/10 bg-white p-6">
              <h2 class="text-3xl font-black">Pages</h2>
              <div class="mt-6 space-y-3">
                <article v-for="page in pages" :key="page.path" class="rounded-lg border border-black/10 bg-[#fbfaf7] p-4">
                  <p class="font-bold">{{ page.title }}</p>
                  <p class="mt-1 text-sm text-[#706c63]">{{ page.path }} / {{ page.updated }}</p>
                </article>
              </div>
            </aside>
            <div class="rounded-lg border border-black/10 bg-white p-6 shadow-[0_18px_50px_rgba(20,20,20,0.06)] sm:p-8">
              <div class="flex items-center justify-between border-b border-black/10 pb-5">
                <h2 class="text-4xl font-black">HTML editor</h2>
                <button type="button" class="rounded-full bg-black px-4 py-2 text-sm font-bold text-white">Save page</button>
              </div>
              <textarea class="mt-6 min-h-[28rem] w-full rounded-lg border border-black/10 bg-[#151515] p-5 font-mono text-sm leading-6 text-white outline-none focus:border-black" :value="`<section class=&quot;about-page&quot;>\n  <h1>About LittlePublisher</h1>\n  <p>A small publishing cockpit for a personal web garden.</p>\n</section>`" />
            </div>
          </section>
        </div>

        <div v-else class="p-5 sm:p-8">
          <button type="button" class="mb-5 rounded-full border border-black/15 bg-white px-4 py-2 text-sm font-bold text-[#4b4944] hover:border-black" @click="backToDashboard">Back to dashboard</button>
          <section class="rounded-lg border border-black/10 bg-white p-6 shadow-[0_18px_50px_rgba(20,20,20,0.06)] sm:p-8">
            <h2 class="text-4xl font-black">Settings</h2>
            <div class="mt-8 grid gap-px overflow-hidden rounded-lg border border-black/10 bg-black/10 md:grid-cols-3">
              <article class="bg-white p-6">
                <h3 class="text-xl font-black">Identity</h3>
                <p class="mt-3 text-sm leading-6 text-[#706c63]">Site URL, IndieAuth profile, author name, avatar, and public feeds.</p>
              </article>
              <article class="bg-white p-6">
                <h3 class="text-xl font-black">Publishing</h3>
                <p class="mt-3 text-sm leading-6 text-[#706c63]">Repository, branch, content paths, item templates, and import rules.</p>
              </article>
              <article class="bg-white p-6">
                <h3 class="text-xl font-black">Health</h3>
                <p class="mt-3 text-sm leading-6 text-[#706c63]">Storage, GitHub, deploy status, recent jobs, and feed generation.</p>
              </article>
            </div>
            <div class="mt-8 divide-y divide-black/10 rounded-lg border border-black/10 bg-[#fbfaf7]">
              <article v-for="check in healthChecks" :key="check.name" class="flex items-center justify-between gap-4 p-5">
                <div>
                  <h3 class="font-bold">{{ check.name }}</h3>
                  <p class="mt-1 text-sm text-[#706c63]">{{ check.detail }}</p>
                </div>
                <span class="h-3 w-3 rounded-full" :class="check.ok ? 'bg-[#25724f]' : 'bg-[#c99739]'"></span>
              </article>
            </div>
          </section>
        </div>
      </section>
    </div>
  </main>
</template>
