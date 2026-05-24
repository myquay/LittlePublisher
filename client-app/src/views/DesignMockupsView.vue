<script setup lang="ts">
import { computed, ref } from 'vue'
import IdeaPanel from '@/components/IdeaPanel.vue'
import gardenHedgeGateUrl from '@/assets/illustrations/garden-hedge-gate.png'
import driftwoodStampUrl from '@/assets/maturity/driftwood-stamp.png'
import pohutukawaStampUrl from '@/assets/maturity/pohutukawa-stamp.png'
import seedStampUrl from '@/assets/maturity/seed-stamp.png'
import seedlingStampUrl from '@/assets/maturity/seedling-stamp.png'
import smallTreeStampUrl from '@/assets/maturity/small-tree-stamp.png'

type SectionKey = 'dashboard' | 'garden' | 'develop' | 'reader' | 'mentions' | 'books' | 'pages' | 'settings'
type PanelTab = 'research' | 'controls'
type MaturityKey = 'seed' | 'seedling' | 'small-tree' | 'pohutakawa' | 'driftwood'

interface MaturityLevel {
  key: MaturityKey
  label: string
  icon: string
  tone: string
  stampUrl: string
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

interface WebmentionActivity {
  id: number
  source: string
  target: string
  author: string
  type: string
  excerpt: string
  received: string
  status: 'Pending' | 'Approved' | 'Rejected'
  verified: boolean
}

interface OutgoingWebmention {
  id: number
  title: string
  target: string
  endpoint: string
  type: string
  source: string
  sourceMode: string
  send: boolean
  status: string
}

interface CultivationFlowItem {
  title: string
  role: string
  detail: string
  count: string
  action: string
  section: SectionKey
}

interface CultivationFlowRow {
  label: string
  summary: string
  items: CultivationFlowItem[]
}

const sections: Array<{ key: SectionKey; label: string; hint: string; count: string }> = [
  { key: 'dashboard', label: 'Dashboard', hint: 'Jumping off point', count: '' },
  { key: 'garden', label: 'Garden Home', hint: 'Articles, notes, and activity', count: '12' },
  { key: 'develop', label: 'Harvest', hint: 'Focused writing', count: '1' },
  { key: 'reader', label: 'Mangrove Roots', hint: 'Blogs, bookmarks, and references', count: '38' },
  { key: 'mentions', label: 'The Mangroves', hint: 'Mentions and backlinks', count: '3' },
  { key: 'books', label: 'Books', hint: 'Reading notes', count: '7' },
  { key: 'pages', label: 'Pages', hint: 'One-off pages', count: '5' },
  { key: 'settings', label: 'Settings', hint: 'Config and health', count: '' },
]

const cultivationFlow: CultivationFlowRow[] = [
  {
    label: 'Incoming signals',
    summary: 'The external web arrives as feeds, bookmarks, webmentions, backlinks, and saved references.',
    items: [
      {
        title: 'The Mangroves',
        role: 'Mentions and backlinks',
        detail: 'Webmentions, backlinks, likes, replies, and places around the web that point back to the site.',
        count: '3 pending',
        action: 'Review signals',
        section: 'mentions',
      },
      {
        title: 'Mangrove Roots',
        role: 'Saved sources',
        detail: 'Blogs, bookmarks, docs, quotes, reading notes, and references that can root into future writing.',
        count: '38 items',
        action: 'Open roots',
        section: 'reader',
      },
    ],
  },
  {
    label: 'Cultivation',
    summary: 'Your own publishing lives in the Garden Home: durable articles, working notes, and lightweight activity.',
    items: [
      {
        title: 'Articles',
        role: 'Mature writing',
        detail: 'Long-lived technical explanations, essays, and series that are actively tended over time.',
        count: '31 articles',
        action: 'Tend articles',
        section: 'garden',
      },
      {
        title: 'Notes',
        role: 'Working thoughts',
        detail: 'Short posts, discoveries, snippets, and observations that may later grow into articles.',
        count: '14 notes',
        action: 'Browse notes',
        section: 'garden',
      },
      {
        title: 'Activity Feed',
        role: 'Personal stream',
        detail: 'Quick thoughts, photos with comments, and links to other articles with your own notes attached.',
        count: '26 updates',
        action: 'See stream',
        section: 'garden',
      },
    ],
  },
  {
    label: 'Protected growth',
    summary: 'Experimental and not-yet-public work gets its own protected place before it joins the Garden.',
    items: [
      {
        title: 'The Greenhouse',
        role: 'Projects and experiments',
        detail: 'Projects, prototypes, tools, one-off pages, and small systems still getting light and attention.',
        count: '5 pages',
        action: 'Open greenhouse',
        section: 'pages',
      },
      {
        title: 'Harvest',
        role: 'Ready to publish',
        detail: 'Focused writing and publish controls for work ready to move from private tending to public presence.',
        count: '2 ready',
        action: 'Prepare harvest',
        section: 'develop',
      },
    ],
  },
]

const maturityLevels: MaturityLevel[] = [
  { key: 'seed', label: 'Seed', icon: 'S', tone: 'bg-[#fff7d6] text-[#745a00]', stampUrl: seedStampUrl },
  { key: 'seedling', label: 'Seedling', icon: 'G', tone: 'bg-[#e6f5e9] text-[#21613a]', stampUrl: seedlingStampUrl },
  { key: 'small-tree', label: 'Small tree', icon: 'T', tone: 'bg-[#e4f0ff] text-[#23517a]', stampUrl: smallTreeStampUrl },
  { key: 'pohutakawa', label: 'Pohutakawa in full bloom', icon: 'P', tone: 'bg-[#ffe7e8] text-[#a5343b]', stampUrl: pohutukawaStampUrl },
  { key: 'driftwood', label: 'Driftwood', icon: 'D', tone: 'bg-[#eee9df] text-[#6e6555]', stampUrl: driftwoodStampUrl },
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
    summary: 'Mangrove roots, notes, and essays as one loop: gather, tend, publish, revisit.',
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
  { name: 'Mangroves', detail: 'Endpoint receiving, 3 external signals awaiting review', ok: true },
  { name: 'RSS', detail: 'Mangrove root import paused until feeds are configured', ok: false },
]
const webmentionActivities = ref<WebmentionActivity[]>([
  {
    id: 1,
    source: 'https://maggie.example/notes/gardens',
    target: '/posts/reader-compost-heap',
    author: 'Maggie Appleton',
    type: 'Reply',
    excerpt: 'This framing of the reader as a compost heap captures the slow value of notes becoming public.',
    received: '18 minutes ago',
    status: 'Pending',
    verified: true,
  },
  {
    id: 2,
    source: 'https://robin.example/bookmarks/small-software',
    target: '/notes/durable-links',
    author: 'Robin Sloan',
    type: 'Bookmark',
    excerpt: 'Saved for the section on durable links and small personal rituals.',
    received: '2 hours ago',
    status: 'Pending',
    verified: true,
  },
  {
    id: 3,
    source: 'https://social.example/@ana/114',
    target: '/posts/small-weblog',
    author: 'Ana Rodrigues',
    type: 'Like',
    excerpt: 'Liked this post.',
    received: 'Yesterday',
    status: 'Approved',
    verified: true,
  },
  {
    id: 4,
    source: 'https://events.example/rsvp/quiet-web',
    target: '/posts/small-weblog',
    author: 'Quiet Web Club',
    type: 'RSVP',
    excerpt: 'RSVP yes to the small-web publishing session.',
    received: 'Yesterday',
    status: 'Pending',
    verified: false,
  },
])
const outgoingWebmentions = ref<OutgoingWebmention[]>([
  {
    id: 1,
    title: 'Garden histories and digital homes',
    target: 'https://maggieappleton.com/garden-history',
    endpoint: 'https://webmention.io/maggieappleton.com/webmention',
    type: 'Reply',
    source: '/posts/reader-compost-heap',
    sourceMode: 'Post content',
    send: true,
    status: 'Endpoint found',
  },
  {
    id: 2,
    title: 'A note on small software',
    target: 'https://www.robinsloan.com/notes/small-software',
    endpoint: 'https://webmention.io/robinsloan.com/webmention',
    type: 'Mention',
    source: '/posts/reader-compost-heap',
    sourceMode: 'Post content',
    send: true,
    status: 'Endpoint found',
  },
  {
    id: 3,
    title: 'A quiet note I liked',
    target: 'https://example.org/quiet-note',
    endpoint: 'https://webmention.io/example.org/webmention',
    type: 'Like',
    source: '/likes/quiet-note',
    sourceMode: 'Generated like',
    send: true,
    status: 'Endpoint found',
  },
  {
    id: 4,
    title: 'Personal publishing tools',
    target: 'https://example.net/publishing-tools',
    endpoint: '',
    type: 'Bookmark',
    source: '/bookmarks/publishing-tools',
    sourceMode: 'Generated bookmark',
    send: false,
    status: 'No endpoint discovered',
  },
])
const mentionTypes = ['Mention', 'Reply', 'Like', 'Bookmark', 'Repost', 'RSVP']
const quickMentionTypes = ['Like', 'Bookmark', 'Repost', 'RSVP']

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
const pendingMentions = computed(() => webmentionActivities.value.filter((mention) => mention.status === 'Pending'))
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

function setMentionStatus(id: number, status: WebmentionActivity['status']) {
  const mention = webmentionActivities.value.find((item) => item.id === id)
  if (mention) mention.status = status
}

function toggleOutgoingWebmention(id: number) {
  const mention = outgoingWebmentions.value.find((item) => item.id === id)
  if (mention && mention.endpoint) mention.send = !mention.send
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
            <div class="border-b border-black/10 pb-8">
              <h2 class="sr-only">Garden Home dashboard</h2>
              <img
                :src="gardenHedgeGateUrl"
                alt="Watercolor hedge with a small garden gate"
                class="mx-auto block h-36 w-full object-contain object-bottom sm:h-44 lg:h-52"
              />
            </div>

            <div class="mt-8 space-y-4">
              <section
                v-for="row in cultivationFlow"
                :key="row.label"
                class="grid gap-px overflow-hidden rounded-lg border border-black/10 bg-black/10 lg:grid-cols-[15rem_minmax(0,1fr)]"
              >
                <div class="bg-[#fbfaf7] p-5">
                  <p class="text-xs font-black uppercase tracking-[0.18em] text-[#8b887f]">{{ row.label }}</p>
                  <p class="mt-4 text-sm leading-6 text-[#706c63]">{{ row.summary }}</p>
                </div>
                <div class="grid gap-px bg-black/10 md:grid-cols-2 xl:grid-cols-3">
                  <button
                    v-for="item in row.items"
                    :key="item.title"
                    type="button"
                    class="min-h-56 bg-white p-6 text-left transition hover:bg-[#fbfaf7] focus:outline-none focus:ring-2 focus:ring-black/20"
                    @click="setSection(item.section)"
                  >
                    <div class="flex items-start justify-between gap-3">
                      <div>
                        <h3 class="text-2xl font-black">{{ item.title }}</h3>
                        <p class="mt-1 text-sm font-semibold text-[#8b887f]">{{ item.role }}</p>
                      </div>
                      <span class="rounded-full border border-black/10 px-3 py-1 text-xs text-[#77736b]">{{ item.count }}</span>
                    </div>
                    <p class="mt-8 text-sm leading-6 text-[#706c63]">{{ item.detail }}</p>
                    <p class="mt-6 text-xs font-black uppercase tracking-[0.14em] text-[#8b887f]">{{ item.action }}</p>
                  </button>
                </div>
              </section>
            </div>
          </section>
        </div>

        <div v-else-if="activeSection === 'garden'" class="grid min-h-[calc(100vh-7rem)] lg:grid-cols-[minmax(0,1fr)_24rem]">
          <section class="p-5 sm:p-8">
            <button type="button" class="mb-5 rounded-full border border-black/15 bg-white px-4 py-2 text-sm font-bold text-[#4b4944] hover:border-black" @click="backToDashboard">Back to dashboard</button>
            <div class="rounded-lg border border-black/10 bg-white shadow-[0_18px_50px_rgba(20,20,20,0.06)]">
              <div class="border-b border-black/10 p-6 sm:p-8">
                <p class="text-sm font-semibold uppercase tracking-[0.18em] text-[#8b887f]">Garden Home</p>
                <h2 class="mt-2 text-4xl font-black">Articles, notes, and activity are cultivated here.</h2>
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
                  <img
                    class="h-16 w-16 object-contain"
                    :alt="maturityLevels.find((level) => level.key === idea.maturity)?.label"
                    :src="maturityLevels.find((level) => level.key === idea.maturity)?.stampUrl"
                  />
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

              <section class="border-t border-black/10 p-6 sm:p-8">
                <div class="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
                  <div>
                    <p class="text-xs font-black uppercase tracking-[0.16em] text-[#8b887f]">Outgoing Webmentions</p>
                    <h3 class="mt-2 text-2xl font-black">Notify linked sites on publish</h3>
                    <p class="mt-2 max-w-2xl text-sm leading-6 text-[#706c63]">
                      Supported links are discovered before publishing. LittlePublisher sends source and target URLs; the selected type describes how this post should be interpreted locally.
                    </p>
                  </div>
                  <span class="rounded-full border border-black/10 px-3 py-2 text-xs font-bold text-[#706c63]">
                    {{ outgoingWebmentions.filter((mention) => mention.send).length }} queued
                  </span>
                </div>

                <div class="mt-6 divide-y divide-black/10 rounded-lg border border-black/10">
                  <article
                    v-for="mention in outgoingWebmentions"
                    :key="mention.id"
                    class="grid gap-4 p-4 lg:grid-cols-[minmax(0,1fr)_9rem_8rem]"
                  >
                    <div class="min-w-0">
                      <p class="truncate text-base font-black">{{ mention.title }}</p>
                      <p class="mt-1 truncate text-sm text-[#706c63]">{{ mention.target }}</p>
                      <p class="mt-1 truncate text-xs font-semibold text-[#8b887f]">{{ mention.sourceMode }} from {{ mention.source }}</p>
                      <p class="mt-2 text-xs font-bold" :class="mention.endpoint ? 'text-[#23834b]' : 'text-[#8b887f]'">{{ mention.status }}</p>
                    </div>
                    <label class="block">
                      <span class="text-xs font-black uppercase tracking-[0.14em] text-[#8b887f]">Type</span>
                      <select v-model="mention.type" class="mt-2 w-full rounded-md border border-black/10 bg-[#fbfaf7] px-3 py-2 text-sm">
                        <option v-for="type in mentionTypes" :key="type">{{ type }}</option>
                      </select>
                    </label>
                    <button
                      type="button"
                      class="self-end rounded-md border border-black/15 px-4 py-3 text-sm font-bold"
                      :class="mention.send ? 'bg-black text-white' : 'bg-white text-[#706c63]'"
                      :disabled="!mention.endpoint"
                      @click="toggleOutgoingWebmention(mention.id)"
                    >
                      {{ mention.send ? 'Send' : 'Skip' }}
                    </button>
                  </article>
                </div>

                <div class="mt-6 rounded-lg border border-black/10 bg-[#fbfaf7] p-5">
                  <div class="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
                    <div>
                      <h4 class="text-xl font-black">Quick mention without writing a post</h4>
                      <p class="mt-2 max-w-2xl text-sm leading-6 text-[#706c63]">
                        For likes, bookmarks, reposts, and RSVPs, LittlePublisher can create a small source entry, then send the Webmention from that URL.
                      </p>
                    </div>
                    <span class="w-fit rounded-full border border-black/10 bg-white px-3 py-2 text-xs font-bold text-[#706c63]">No article body required</span>
                  </div>
                  <div class="mt-5 grid gap-3 lg:grid-cols-[minmax(0,1fr)_9rem_9rem]">
                    <input class="min-h-12 rounded-md border border-black/10 bg-white px-4 text-sm outline-none focus:border-black" value="https://example.org/quiet-note" aria-label="Target URL" />
                    <select class="rounded-md border border-black/10 bg-white px-3 py-2 text-sm">
                      <option v-for="type in quickMentionTypes" :key="type">{{ type }}</option>
                    </select>
                    <button type="button" class="rounded-md bg-black px-4 py-3 text-sm font-bold text-white">Create and send</button>
                  </div>
                </div>
              </section>
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
                    <span>Incoming roots</span>
                    <span>{{ readerPosts.filter((post) => !post.read).length }}</span>
                  </button>
                  <button
                    type="button"
                    class="flex w-full items-center justify-between rounded-md px-4 py-3 text-left font-bold"
                    :class="activeReaderView === 'readLater' ? 'bg-[#e7f7e9] text-[#23834b]' : 'text-[#4b4944] hover:bg-white'"
                    @click="activeReaderView = 'readLater'"
                  >
                    <span>Reference shelf</span>
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
                    <span>Blogs I Like</span>
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

                <button type="button" class="mt-8 w-full rounded-md bg-black px-4 py-3 text-sm font-bold text-white" @click="activeReaderView = 'sites'">Add or manage blogs</button>
              </aside>

              <div class="p-6 sm:p-8">
                <div class="border-b border-black/10 pb-5">
                  <div class="flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
                    <div>
                      <h2 class="text-4xl font-black">
                        {{ selectedReaderPostId ? selectedReaderPost.blog : activeReaderView === 'today' ? 'Mangrove Roots' : activeReaderView === 'readLater' ? 'Reference shelf' : activeReaderView === 'sites' ? 'Blogs I Like' : 'Bookmarks' }}
                      </h2>
                      <p class="mt-2 text-sm text-[#706c63]">
                        {{ selectedReaderPostId ? selectedReaderPost.category : activeReaderView === 'sites' ? 'Manage feed URLs, categories, blog roll status, and sync health.' : `${visibleReaderPosts.length} external roots from ${activeReaderCategory}` }}
                      </p>
                    </div>
                    <button v-if="selectedReaderPostId" type="button" class="rounded-full border border-black/15 px-4 py-2 text-sm font-bold text-[#4b4944] hover:border-black" @click="closeReaderPost">Back to roots</button>
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
                    <h3 class="text-2xl font-black">Reading notes</h3>
                    <p class="mt-2 text-sm leading-6 text-[#706c63]">Tag a section, leave a free note, or plant either as a Garden seed.</p>
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

        <div v-else-if="activeSection === 'mentions'" class="p-5 sm:p-8">
          <button type="button" class="mb-5 rounded-full border border-black/15 bg-white px-4 py-2 text-sm font-bold text-[#4b4944] hover:border-black" @click="backToDashboard">Back to dashboard</button>
          <section class="overflow-hidden rounded-lg border border-black/10 bg-white shadow-[0_18px_50px_rgba(20,20,20,0.06)]">
            <div class="border-b border-black/10 p-6 sm:p-8">
              <div class="flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
                <div>
                  <p class="text-sm font-semibold uppercase tracking-[0.18em] text-[#8b887f]">The Mangroves</p>
                  <h2 class="mt-2 text-4xl font-black">Signals from the wider web</h2>
                  <p class="mt-3 max-w-3xl text-sm leading-6 text-[#706c63]">
                    Incoming source and target URLs are verified first, then held for review. Approved mentions, backlinks, likes, bookmarks, and replies can appear as mangrove roots around the relevant content.
                  </p>
                </div>
                <div class="grid grid-cols-3 gap-px overflow-hidden rounded-lg border border-black/10 bg-black/10 text-center">
                  <div class="bg-[#fbfaf7] px-4 py-3">
                    <p class="text-2xl font-black">{{ pendingMentions.length }}</p>
                    <p class="text-xs font-bold text-[#706c63]">Pending</p>
                  </div>
                  <div class="bg-[#fbfaf7] px-4 py-3">
                    <p class="text-2xl font-black">{{ webmentionActivities.filter((mention) => mention.status === 'Approved').length }}</p>
                    <p class="text-xs font-bold text-[#706c63]">Approved</p>
                  </div>
                  <div class="bg-[#fbfaf7] px-4 py-3">
                    <p class="text-2xl font-black">{{ webmentionActivities.filter((mention) => !mention.verified).length }}</p>
                    <p class="text-xs font-bold text-[#706c63]">Needs check</p>
                  </div>
                </div>
              </div>
            </div>

            <div class="grid min-h-[38rem] lg:grid-cols-[minmax(0,1fr)_22rem]">
              <div class="divide-y divide-black/10">
                <article
                  v-for="mention in webmentionActivities"
                  :key="mention.id"
                  class="grid gap-5 p-5 sm:p-6 xl:grid-cols-[7rem_minmax(0,1fr)_12rem]"
                >
                  <div>
                    <span class="inline-flex rounded-md border border-black/10 bg-[#fbfaf7] px-3 py-2 text-xs font-black uppercase tracking-[0.14em] text-[#4b4944]">{{ mention.type }}</span>
                    <p class="mt-3 text-xs font-semibold text-[#8b887f]">{{ mention.received }}</p>
                  </div>
                  <div class="min-w-0">
                    <div class="flex flex-wrap items-center gap-2">
                      <h3 class="text-2xl font-black">{{ mention.author }}</h3>
                      <span class="rounded-full px-3 py-1 text-xs font-bold" :class="mention.verified ? 'bg-[#e7f7e9] text-[#23834b]' : 'bg-[#fff5d8] text-[#856100]'">
                        {{ mention.verified ? 'Verified link' : 'Verify source' }}
                      </span>
                    </div>
                    <p class="mt-3 max-w-3xl text-lg leading-7 text-[#252525]">"{{ mention.excerpt }}"</p>
                    <div class="mt-4 grid gap-2 text-sm text-[#706c63]">
                      <p class="truncate"><span class="font-bold text-black">Source:</span> {{ mention.source }}</p>
                      <p class="truncate"><span class="font-bold text-black">Target:</span> {{ mention.target }}</p>
                    </div>
                  </div>
                  <div class="flex flex-col gap-2 xl:items-end">
                    <span class="w-fit rounded-full border border-black/10 px-3 py-2 text-xs font-bold" :class="mention.status === 'Approved' ? 'bg-[#e7f7e9] text-[#23834b]' : mention.status === 'Rejected' ? 'bg-[#fff0f0] text-[#a5343b]' : 'text-[#706c63]'">
                      {{ mention.status }}
                    </span>
                    <button type="button" class="w-full rounded-md bg-black px-4 py-3 text-sm font-bold text-white disabled:opacity-30" :disabled="mention.status === 'Approved'" @click="setMentionStatus(mention.id, 'Approved')">Approve</button>
                    <button type="button" class="w-full rounded-md border border-black/15 px-4 py-3 text-sm font-bold text-[#706c63] disabled:opacity-30" :disabled="mention.status === 'Rejected'" @click="setMentionStatus(mention.id, 'Rejected')">Reject</button>
                  </div>
                </article>
              </div>

              <aside class="border-t border-black/10 bg-[#fbfaf7] p-5 sm:p-6 lg:border-l lg:border-t-0">
                <h3 class="text-2xl font-black">Mangrove rules</h3>
                <div class="mt-5 space-y-4 text-sm leading-6 text-[#706c63]">
                  <p><span class="font-bold text-black">Receive:</span> accept source and target URLs, then queue verification.</p>
                  <p><span class="font-bold text-black">Verify:</span> fetch the source and confirm it links exactly to the target before publication.</p>
                  <p><span class="font-bold text-black">Review:</span> nothing appears publicly until approved.</p>
                </div>
                <div class="mt-6 rounded-lg border border-black/10 bg-white p-4">
                  <p class="text-xs font-black uppercase tracking-[0.14em] text-[#8b887f]">Public display</p>
                  <div class="mt-4 space-y-2">
                    <label v-for="type in mentionTypes" :key="type" class="flex items-center justify-between gap-3 rounded-md bg-[#fbfaf7] px-3 py-2 text-sm font-bold">
                      <span>{{ type }}</span>
                      <input type="checkbox" checked class="h-4 w-4 accent-black" />
                    </label>
                  </div>
                </div>
                <div class="mt-6 rounded-lg border border-black/10 bg-white p-4">
                  <p class="text-xs font-black uppercase tracking-[0.14em] text-[#8b887f]">Send without content</p>
                  <p class="mt-3 text-sm leading-6 text-[#706c63]">
                    A like still needs a source URL. LittlePublisher can create a tiny private-to-public source entry such as <span class="font-bold text-black">/likes/quiet-note</span>, then send source and target to the discovered endpoint.
                  </p>
                  <div class="mt-4 grid gap-2">
                    <input class="min-h-11 rounded-md border border-black/10 bg-[#fbfaf7] px-3 text-sm" value="https://example.org/quiet-note" aria-label="Quick Webmention target" />
                    <select class="rounded-md border border-black/10 bg-[#fbfaf7] px-3 py-2 text-sm">
                      <option v-for="type in quickMentionTypes" :key="type">{{ type }}</option>
                    </select>
                    <button type="button" class="rounded-md bg-black px-4 py-3 text-sm font-bold text-white">Create source and send</button>
                  </div>
                </div>
              </aside>
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
            <div class="mt-8 grid gap-px overflow-hidden rounded-lg border border-black/10 bg-black/10 md:grid-cols-2 xl:grid-cols-4">
              <article class="bg-white p-6">
                <h3 class="text-xl font-black">Identity</h3>
                <p class="mt-3 text-sm leading-6 text-[#706c63]">Site URL, IndieAuth profile, author name, avatar, and public feeds.</p>
              </article>
              <article class="bg-white p-6">
                <h3 class="text-xl font-black">Publishing</h3>
                <p class="mt-3 text-sm leading-6 text-[#706c63]">Repository, branch, content paths, item templates, and import rules.</p>
              </article>
              <article class="bg-white p-6">
                <h3 class="text-xl font-black">Mangroves</h3>
                <p class="mt-3 text-sm leading-6 text-[#706c63]">Webmention endpoint, backlink discovery, moderation defaults, and outgoing send behavior.</p>
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
