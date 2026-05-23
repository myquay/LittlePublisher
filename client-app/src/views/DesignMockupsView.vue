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
const readerPosts = ref([
  { id: 1, blog: 'Maggie Appleton', title: 'Garden histories and digital homes', read: false, starred: true, thought: 'Useful framing for the Garden.' },
  { id: 2, blog: 'Robin Sloan', title: 'A note on small software', read: true, starred: false, thought: '' },
  { id: 3, blog: 'Simon Willison', title: 'Notes on personal publishing tools', read: false, starred: false, thought: 'Check Micropub overlap.' },
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
const ideaTitle = ref('')
const dropNotice = ref('')
const oneOffLink = ref('https://example.com/thoughtful-post')

const selectedIdea = computed<Idea>(() => ideas.value.find((idea) => idea.id === selectedIdeaId.value) ?? ideas.value[0]!)
const selectedMaturity = computed<MaturityLevel>(() => maturityLevels.find((level) => level.key === selectedIdea.value.maturity) ?? maturityLevels[0]!)
const starredPosts = computed(() => readerPosts.value.filter((post) => post.starred))

function setSection(section: SectionKey) {
  activeSection.value = section
  if (section === 'garden') panelOpen.value = true
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

function addOneOffBookmark() {
  const nextId = Math.max(...readerPosts.value.map((post) => post.id)) + 1
  readerPosts.value.unshift({
    id: nextId,
    blog: 'One-off link',
    title: oneOffLink.value,
    read: false,
    starred: true,
    thought: 'Saved directly as a bookmark.',
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
    <div class="grid min-h-screen lg:grid-cols-[17.5rem_minmax(0,1fr)]">
      <aside class="border-b border-black/10 bg-[#fbfaf7] p-5 lg:border-b-0 lg:border-r">
        <div class="flex items-center gap-3 border-b border-black/10 pb-5">
          <div class="h-11 w-11 rounded-full border-[3px] border-black bg-white"></div>
          <div>
            <p class="text-lg font-black">LittlePublisher</p>
            <p class="text-xs text-[#77736b]">michael.example</p>
          </div>
        </div>

        <nav class="mt-5 grid grid-cols-2 gap-1 sm:grid-cols-4 lg:block lg:space-y-1">
          <button
            v-for="section in sections"
            :key="section.key"
            type="button"
            class="group flex min-w-0 items-center gap-3 rounded-2xl px-3 py-3 text-left transition lg:w-full lg:px-4"
            :class="activeSection === section.key ? 'bg-black text-white shadow-[0_12px_30px_rgba(0,0,0,0.18)]' : 'text-[#4b4944] hover:bg-white'"
            @click="setSection(section.key)"
          >
            <span
              class="flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-xs font-black"
              :class="activeSection === section.key ? 'bg-white text-black' : 'bg-white text-[#77736b] ring-1 ring-black/10'"
            >
              {{ section.label.slice(0, 1) }}
            </span>
            <span class="min-w-0 flex-1">
              <span class="block truncate text-sm font-bold">{{ section.label }}</span>
              <span class="hidden truncate text-xs lg:block" :class="activeSection === section.key ? 'text-white/65' : 'text-[#8a867c]'">{{ section.hint }}</span>
            </span>
            <span v-if="section.count" class="hidden text-xs lg:inline" :class="activeSection === section.key ? 'text-white/70' : 'text-[#9a968d]'">{{ section.count }}</span>
          </button>
        </nav>
      </aside>

      <section class="min-w-0">
        <header class="sticky top-0 z-20 border-b border-black/10 bg-[#f7f6f2]/95 px-5 py-5 backdrop-blur sm:px-8">
          <div class="flex flex-col gap-4 xl:flex-row xl:items-center xl:justify-between">
            <div>
              <p class="text-xs font-black uppercase tracking-[0.2em] text-[#8b887f]">Clickable concept</p>
              <h1 class="mt-1 text-4xl font-black leading-none sm:text-5xl">
                {{ sections.find((section) => section.key === activeSection)?.label }}
              </h1>
            </div>
            <div class="flex flex-wrap gap-2 text-sm font-semibold">
              <button type="button" class="rounded-full bg-[#ff5a5f] px-5 py-3 text-white shadow-[0_12px_28px_rgba(255,90,95,0.22)]" @click="activeSection = 'garden'">New idea</button>
              <button type="button" class="rounded-full border border-black/15 bg-white px-5 py-3 text-[#4b4944] hover:border-black" @click="openEditor()">Edit selected</button>
              <button type="button" class="rounded-full border border-black/15 bg-white px-5 py-3 text-[#4b4944] hover:border-black" @click="activeSection = 'settings'">Health</button>
            </div>
          </div>
        </header>

        <div v-if="activeSection === 'dashboard'" class="p-5 sm:p-8">
          <section class="rounded-[28px] border border-black/10 bg-white p-6 shadow-[0_24px_80px_rgba(20,20,20,0.08)] sm:p-8">
            <div class="flex flex-col gap-4 border-b border-black/10 pb-8 lg:flex-row lg:items-end lg:justify-between">
              <div>
                <p class="text-sm text-[#77736b]">Today</p>
                <h2 class="mt-2 max-w-3xl text-5xl font-black leading-[0.95]">What needs tending before the next publish?</h2>
              </div>
              <button type="button" class="w-fit rounded-full bg-black px-5 py-3 text-sm font-bold text-white" @click="activeSection = 'garden'">Open garden</button>
            </div>

            <div class="mt-8 grid gap-px overflow-hidden rounded-[24px] border border-black/10 bg-black/10 sm:grid-cols-2 xl:grid-cols-4">
              <button
                v-for="section in sections.filter((item) => item.key !== 'dashboard')"
                :key="section.key"
                type="button"
                class="bg-white p-6 text-left transition hover:bg-[#fbfaf7]"
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
            <div class="rounded-[28px] border border-black/10 bg-white shadow-[0_24px_80px_rgba(20,20,20,0.08)]">
              <div class="border-b border-black/10 p-6 sm:p-8">
                <p class="text-sm font-semibold uppercase tracking-[0.18em] text-[#8b887f]">The garden</p>
                <h2 class="mt-2 text-4xl font-black">Ideas are cultivated here.</h2>
                <form class="mt-6 flex flex-col gap-3 sm:flex-row" @submit.prevent="createIdea">
                  <input v-model="ideaTitle" class="min-h-14 flex-1 rounded-2xl border border-black/10 bg-[#fbfaf7] px-5 text-lg outline-none focus:border-black" placeholder="Start typing to plant an idea..." />
                  <button type="submit" class="rounded-2xl bg-black px-6 py-4 text-sm font-bold text-white">Plant</button>
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
                  <span class="flex h-14 w-14 items-center justify-center rounded-2xl text-lg font-black" :class="maturityLevels.find((level) => level.key === idea.maturity)?.tone">
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

          <aside v-if="panelOpen" class="border-t border-black/10 bg-[#fbfaf7] p-5 lg:border-l lg:border-t-0 lg:p-6">
            <div class="sticky top-28">
              <IdeaPanel
                :attachments="attachments"
                :drop-notice="dropNotice"
                :maturity-levels="maturityLevels"
                :panel-tab="panelTab"
                :research-links="researchLinks"
                :selected-idea="selectedIdea"
                :selected-maturity="selectedMaturity"
                @drop-file="handleDrop"
                @edit="openEditor()"
                @set-maturity="updateMaturity"
                @set-tab="panelTab = $event"
              />
            </div>
          </aside>
        </div>

        <div v-else-if="activeSection === 'develop'" class="grid min-h-[calc(100vh-7rem)] lg:grid-cols-[minmax(0,1fr)_24rem]">
          <section class="bg-white p-5 sm:p-8">
            <div class="flex flex-col gap-4 border-b border-black/10 pb-6 sm:flex-row sm:items-center sm:justify-between">
              <button type="button" class="w-fit rounded-full border border-black/15 px-4 py-2 text-sm font-bold text-black" @click="activeSection = 'garden'">Back</button>
              <div class="flex gap-2">
                <button type="button" class="rounded-full border border-black/15 px-4 py-2 text-sm font-semibold">Preview</button>
                <button type="button" class="rounded-full bg-[#ff5a5f] px-4 py-2 text-sm font-bold text-white shadow-[0_12px_28px_rgba(255,90,95,0.22)]">Publish</button>
              </div>
            </div>

            <article class="mx-auto max-w-3xl py-12">
              <input class="w-full border-0 bg-transparent text-5xl font-black leading-none outline-none placeholder:text-[#d2cec5] sm:text-7xl" :value="selectedIdea.title" aria-label="Title" />
              <div class="mt-8 flex flex-wrap gap-2 text-xs font-medium text-[#77736b]">
                <span class="rounded-full border border-black/10 px-3 py-1">{{ selectedIdea.status }}</span>
                <span class="rounded-full border border-black/10 px-3 py-1">{{ selectedMaturity.label }}</span>
                <span class="rounded-full border border-black/10 px-3 py-1">{{ selectedIdea.folder }}/{{ selectedIdea.filename }}</span>
              </div>
              <textarea class="mt-12 min-h-[30rem] w-full resize-none border-0 bg-transparent text-xl leading-9 text-[#252525] outline-none" :value="`Publishing should feel like lowering a note into the world, not launching software.\n\nThis is the focused writing canvas. Research stays close, but the page does not carry the full dashboard navigation.\n\nA bookmark can become a note, a review can become a post, and a project update can become an activity entry.`" />
            </article>
          </section>

          <aside class="border-t border-black/10 bg-[#fbfaf7] p-5 lg:border-l lg:border-t-0 lg:p-6">
            <div class="sticky top-28">
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
                @edit="openEditor()"
                @set-maturity="updateMaturity"
                @set-tab="panelTab = $event"
              />
            </div>
          </aside>
        </div>

        <div v-else-if="activeSection === 'reader'" class="p-5 sm:p-8">
          <section class="grid gap-6 xl:grid-cols-[minmax(0,1fr)_22rem]">
            <div class="rounded-[28px] border border-black/10 bg-white p-6 shadow-[0_24px_80px_rgba(20,20,20,0.08)] sm:p-8">
              <h2 class="text-4xl font-black">Reader</h2>
              <p class="mt-2 text-[#706c63]">Follow blogs, track read state, star posts, and keep personal thoughts close to bookmarks.</p>
              <form class="mt-6 flex flex-col gap-3 sm:flex-row" @submit.prevent="addOneOffBookmark">
                <input v-model="oneOffLink" class="min-h-14 flex-1 rounded-2xl border border-black/10 bg-[#fbfaf7] px-5 outline-none focus:border-black" placeholder="Save a one-off link..." />
                <button type="submit" class="rounded-2xl bg-black px-6 py-4 text-sm font-bold text-white">Bookmark</button>
              </form>
              <div class="mt-8 divide-y divide-black/10">
                <article v-for="post in readerPosts" :key="post.id" class="py-5">
                  <div class="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                    <div>
                      <p class="text-xs font-bold uppercase tracking-[0.16em] text-[#8b887f]">{{ post.blog }}</p>
                      <h3 class="mt-1 text-2xl font-black">{{ post.title }}</h3>
                      <textarea v-model="post.thought" class="mt-4 min-h-20 w-full rounded-2xl border border-black/10 bg-[#fbfaf7] p-4 text-sm outline-none focus:border-black" placeholder="Write thoughts about this post..." />
                    </div>
                    <div class="flex gap-2">
                      <button type="button" class="rounded-full border border-black/15 px-4 py-2 text-sm font-semibold" @click="toggleRead(post.id)">{{ post.read ? 'Read' : 'Unread' }}</button>
                      <button type="button" class="rounded-full px-4 py-2 text-sm font-bold" :class="post.starred ? 'bg-[#ff5a5f] text-white' : 'border border-black/15 text-[#706c63]'" @click="toggleStar(post.id)">Star</button>
                    </div>
                  </div>
                </article>
              </div>
            </div>
            <aside class="rounded-[28px] border border-black/10 bg-[#fbfaf7] p-6">
              <h3 class="text-2xl font-black">Bookmarks</h3>
              <div class="mt-5 space-y-3">
                <article v-for="post in starredPosts" :key="post.id" class="rounded-3xl bg-white p-4 ring-1 ring-black/10">
                  <p class="text-xs text-[#8b887f]">{{ post.blog }}</p>
                  <p class="mt-1 font-bold">{{ post.title }}</p>
                </article>
              </div>
            </aside>
          </section>
        </div>

        <div v-else-if="activeSection === 'books'" class="p-5 sm:p-8">
          <section class="rounded-[28px] border border-black/10 bg-white p-6 shadow-[0_24px_80px_rgba(20,20,20,0.08)] sm:p-8">
            <h2 class="text-4xl font-black">Books</h2>
            <div class="mt-8 grid gap-4 lg:grid-cols-3">
              <article v-for="book in books" :key="book.title" class="rounded-[24px] border border-black/10 bg-[#fbfaf7] p-5">
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
          <section class="grid gap-6 xl:grid-cols-[22rem_minmax(0,1fr)]">
            <aside class="rounded-[28px] border border-black/10 bg-white p-6">
              <h2 class="text-3xl font-black">Pages</h2>
              <div class="mt-6 space-y-3">
                <article v-for="page in pages" :key="page.path" class="rounded-3xl border border-black/10 bg-[#fbfaf7] p-4">
                  <p class="font-bold">{{ page.title }}</p>
                  <p class="mt-1 text-sm text-[#706c63]">{{ page.path }} / {{ page.updated }}</p>
                </article>
              </div>
            </aside>
            <div class="rounded-[28px] border border-black/10 bg-white p-6 shadow-[0_24px_80px_rgba(20,20,20,0.08)] sm:p-8">
              <div class="flex items-center justify-between border-b border-black/10 pb-5">
                <h2 class="text-4xl font-black">HTML editor</h2>
                <button type="button" class="rounded-full bg-black px-4 py-2 text-sm font-bold text-white">Save page</button>
              </div>
              <textarea class="mt-6 min-h-[28rem] w-full rounded-3xl border border-black/10 bg-[#151515] p-5 font-mono text-sm leading-6 text-white outline-none focus:border-black" :value="`<section class=&quot;about-page&quot;>\n  <h1>About LittlePublisher</h1>\n  <p>A small publishing cockpit for a personal web garden.</p>\n</section>`" />
            </div>
          </section>
        </div>

        <div v-else class="p-5 sm:p-8">
          <section class="rounded-[28px] border border-black/10 bg-white p-6 shadow-[0_24px_80px_rgba(20,20,20,0.08)] sm:p-8">
            <h2 class="text-4xl font-black">Settings</h2>
            <div class="mt-8 grid gap-px overflow-hidden rounded-[24px] border border-black/10 bg-black/10 md:grid-cols-3">
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
            <div class="mt-8 divide-y divide-black/10 rounded-[24px] border border-black/10 bg-[#fbfaf7]">
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
