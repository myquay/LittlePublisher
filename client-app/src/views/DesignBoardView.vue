<script setup lang="ts">
import driftwoodStampUrl from '@/assets/maturity/driftwood-stamp.png'
import pohutukawaStampUrl from '@/assets/maturity/pohutukawa-stamp.png'
import seedStampUrl from '@/assets/maturity/seed-stamp.png'
import seedlingStampUrl from '@/assets/maturity/seedling-stamp.png'
import smallTreeStampUrl from '@/assets/maturity/small-tree-stamp.png'

const principles = [
  {
    title: 'Dashboard first',
    body: 'Dashboard is the only jumping-off place. Every product area returns there instead of carrying a global menu.',
  },
  {
    title: 'Cultivation over filing',
    body: 'Writing starts as a seed and matures over time. The UI should make that lifecycle visible without making it feel administrative.',
  },
  {
    title: 'Quiet operations',
    body: 'Settings and health checks read like calm operations: clear status, direct controls, no decoration competing with the work.',
  },
  {
    title: 'Cards for surfaces, rows for work',
    body: 'Use bordered cards for major page surfaces and simple rows for scan-heavy lists such as ideas, posts, notes, and jobs.',
  },
]

const components = [
  { name: 'Page shell', detail: 'Warm page background, centered max-width, local back control, one primary white work surface.' },
  { name: 'Dashboard tile', detail: 'Large title, compact count, and one-line purpose. Tiles are navigation, not status overload.' },
  { name: 'Garden row', detail: 'Maturity mark, title, summary, type, maturity, and status. The row is the main browsing unit.' },
  { name: 'Side panel', detail: 'Context stays beside the work: research, controls, metadata, and a persistent Cultivate action.' },
  { name: 'Buttons', detail: 'Black for primary actions, white bordered for navigation, coral only for publish/destructive attention.' },
  { name: 'Forms', detail: 'Plain inputs with small labels, 6-8px radii, generous hit areas, and restrained focus borders.' },
]

const statuses = [
  { label: 'Seed', stampUrl: seedStampUrl },
  { label: 'Seedling', stampUrl: seedlingStampUrl },
  { label: 'Small tree', stampUrl: smallTreeStampUrl },
  { label: 'Pohutakawa', stampUrl: pohutukawaStampUrl },
  { label: 'Driftwood', stampUrl: driftwoodStampUrl },
]

const currentPages = [
  {
    title: 'Dashboard',
    label: 'Jumping off place',
    items: ['Garden', 'Posts and notes', 'Settings', 'Health'],
  },
  {
    title: 'Posts and Notes',
    label: 'Existing publishing',
    items: ['Drafts', 'Published items', 'Recent publish jobs', 'Post or note type'],
  },
  {
    title: 'Publish Detail',
    label: 'Current writing flow',
    items: ['Focused editor', 'Metadata panel', 'Preview', 'Publish'],
  },
]

const settingsItems = [
  ['Identity', 'Site URL, IndieAuth profile, author details, public feeds.'],
  ['Publishing', 'Repository, branch, folders, filenames, templates, import rules.'],
  ['Health', 'Storage, GitHub, website generation, RSS checks, recent job state.'],
]

const gardenIdeas = [
  {
    title: 'The reader as compost heap',
    summary: 'Reader, bookmarks, notes, and essays as one loop: gather, tend, publish, revisit.',
    status: 'Small tree',
    stampUrl: smallTreeStampUrl,
  },
  {
    title: 'Durable links deserve better rituals',
    summary: 'A note about link saving, context, and why a bookmark without a thought goes stale.',
    status: 'Seedling',
    stampUrl: seedlingStampUrl,
  },
  {
    title: 'Inbox zero was never the point',
    summary: 'Old productivity idea that might become a footnote rather than a full post.',
    status: 'Driftwood',
    stampUrl: driftwoodStampUrl,
  },
]
</script>

<template>
  <main class="min-h-screen bg-[#f7f6f2] text-[#151515]">
    <section class="mx-auto max-w-[96rem] px-5 py-8 sm:px-8">
      <header class="rounded-lg border border-black/10 bg-white p-6 shadow-[0_18px_50px_rgba(20,20,20,0.06)] sm:p-8">
        <div class="flex flex-col gap-8 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <p class="text-xs font-black uppercase tracking-[0.2em] text-[#8b887f]">LittlePublisher</p>
            <h1 class="mt-3 max-w-4xl text-5xl font-black leading-none sm:text-6xl">Design board</h1>
            <p class="mt-5 max-w-3xl text-lg leading-8 text-[#706c63]">
              A design-language board for the current product scope: dashboard, existing publishing pages, settings, and the garden for idea cultivation.
            </p>
          </div>
          <a href="/mockups" class="w-fit rounded-full border border-black/15 px-5 py-3 text-sm font-bold text-[#4b4944] hover:border-black">
            Open prototype
          </a>
        </div>
      </header>

      <section class="mt-8 grid gap-6 xl:grid-cols-[minmax(0,1fr)_24rem]">
        <div class="rounded-lg border border-black/10 bg-white p-6 shadow-[0_18px_50px_rgba(20,20,20,0.06)] sm:p-8">
          <p class="text-xs font-black uppercase tracking-[0.18em] text-[#8b887f]">Design language</p>
          <h2 class="mt-2 text-4xl font-black">Calm tools for tending work.</h2>
          <div class="mt-8 grid gap-px overflow-hidden rounded-lg border border-black/10 bg-black/10 md:grid-cols-2">
            <article v-for="principle in principles" :key="principle.title" class="bg-white p-5">
              <h3 class="text-xl font-black">{{ principle.title }}</h3>
              <p class="mt-3 text-sm leading-6 text-[#706c63]">{{ principle.body }}</p>
            </article>
          </div>
        </div>

        <aside class="rounded-lg border border-black/10 bg-white p-6 shadow-[0_18px_50px_rgba(20,20,20,0.06)]">
          <p class="text-xs font-black uppercase tracking-[0.18em] text-[#8b887f]">Tokens</p>
          <div class="mt-5 space-y-5">
            <div>
              <h3 class="font-black">Color</h3>
              <div class="mt-3 grid grid-cols-5 gap-2">
                <span class="h-12 rounded-md bg-[#151515]"></span>
                <span class="h-12 rounded-md bg-[#f7f6f2] ring-1 ring-black/10"></span>
                <span class="h-12 rounded-md bg-[#fbfaf7] ring-1 ring-black/10"></span>
                <span class="h-12 rounded-md bg-[#35c759]"></span>
                <span class="h-12 rounded-md bg-[#ff5a5f]"></span>
              </div>
            </div>
            <div>
              <h3 class="font-black">Maturity</h3>
              <div class="mt-3 grid grid-cols-5 gap-2">
                <img
                  v-for="status in statuses"
                  :key="status.label"
                  class="h-14 w-full object-contain"
                  :alt="status.label"
                  :src="status.stampUrl"
                />
              </div>
            </div>
            <div>
              <h3 class="font-black">Shape</h3>
              <p class="mt-2 text-sm leading-6 text-[#706c63]">Cards use 8px radius or less. Pills are reserved for compact actions and status chips.</p>
            </div>
          </div>
        </aside>
      </section>

      <section class="mt-8 rounded-lg border border-black/10 bg-white p-6 shadow-[0_18px_50px_rgba(20,20,20,0.06)] sm:p-8">
        <p class="text-xs font-black uppercase tracking-[0.18em] text-[#8b887f]">Components</p>
        <h2 class="mt-2 text-4xl font-black">Reusable parts</h2>
        <div class="mt-8 grid gap-px overflow-hidden rounded-lg border border-black/10 bg-black/10 md:grid-cols-2 xl:grid-cols-3">
          <article v-for="component in components" :key="component.name" class="bg-white p-5">
            <h3 class="text-xl font-black">{{ component.name }}</h3>
            <p class="mt-3 text-sm leading-6 text-[#706c63]">{{ component.detail }}</p>
          </article>
        </div>
      </section>

      <section class="mt-8">
        <div class="mb-5">
          <p class="text-xs font-black uppercase tracking-[0.18em] text-[#8b887f]">Page board</p>
          <h2 class="mt-2 text-4xl font-black">Current pages in the design language</h2>
        </div>

        <div class="grid gap-6 xl:grid-cols-3">
          <article v-for="page in currentPages" :key="page.title" class="rounded-lg border border-black/10 bg-white p-6 shadow-[0_18px_50px_rgba(20,20,20,0.06)]">
            <p class="text-sm font-semibold text-[#706c63]">{{ page.label }}</p>
            <h3 class="mt-2 text-3xl font-black">{{ page.title }}</h3>
            <div class="mt-6 divide-y divide-black/10 rounded-lg border border-black/10 bg-[#fbfaf7]">
              <p v-for="item in page.items" :key="item" class="p-4 text-sm font-bold text-[#4b4944]">{{ item }}</p>
            </div>
          </article>
        </div>
      </section>

      <section class="mt-8 grid gap-6 xl:grid-cols-[minmax(0,1fr)_24rem]">
        <div class="rounded-lg border border-black/10 bg-white shadow-[0_18px_50px_rgba(20,20,20,0.06)]">
          <div class="border-b border-black/10 p-6 sm:p-8">
            <p class="text-xs font-black uppercase tracking-[0.18em] text-[#8b887f]">Garden and ideas cultivation</p>
            <h2 class="mt-2 text-4xl font-black">Ideas are cultivated here.</h2>
            <div class="mt-6 flex gap-3">
              <div class="min-h-14 flex-1 rounded-md border border-black/10 bg-[#fbfaf7] px-5 py-4 text-lg text-[#8b887f]">Start typing to plant an idea...</div>
              <button type="button" class="rounded-md bg-black px-6 text-sm font-bold text-white">Plant</button>
            </div>
          </div>
          <div class="divide-y divide-black/10">
            <article v-for="idea in gardenIdeas" :key="idea.title" class="grid gap-4 p-5 sm:grid-cols-[4rem_minmax(0,1fr)_8rem] sm:p-6">
              <img
                class="h-16 w-16 object-contain"
                :alt="idea.status"
                :src="idea.stampUrl"
              />
              <div>
                <h3 class="text-2xl font-black">{{ idea.title }}</h3>
                <p class="mt-2 text-sm leading-6 text-[#706c63]">{{ idea.summary }}</p>
                <p class="mt-3 text-xs font-semibold uppercase tracking-[0.14em] text-[#8b887f]">Post / {{ idea.status }}</p>
              </div>
              <span class="self-start rounded-full border border-black/10 px-3 py-2 text-center text-xs font-semibold text-[#706c63]">Draft</span>
            </article>
          </div>
        </div>

        <aside>
          <div class="rounded-lg border border-black/10 bg-white p-5 shadow-[0_18px_50px_rgba(20,20,20,0.06)]">
            <div class="flex items-start justify-between gap-4">
              <div>
                <p class="text-xs font-black uppercase tracking-[0.16em] text-[#8b887f]">Idea panel</p>
                <h3 class="mt-2 text-2xl font-black">The reader as compost heap</h3>
                <p class="mt-2 text-sm text-[#706c63]">Post / Small tree</p>
              </div>
              <img
                class="h-16 w-16 shrink-0 object-contain"
                alt="Small tree"
                :src="smallTreeStampUrl"
              />
            </div>
            <div class="mt-5 grid grid-cols-2 gap-2 rounded-md bg-[#f7f6f2] p-1 text-sm font-bold">
              <span class="rounded-md bg-white px-3 py-2 text-center shadow-sm">Research</span>
              <span class="px-3 py-2 text-center text-[#706c63]">Controls</span>
            </div>
            <div class="mt-5 rounded-lg border border-dashed border-black/20 bg-[#fbfaf7] p-5 text-sm leading-6 text-[#706c63]">
              <p class="font-bold text-black">Drop research here</p>
              <p class="mt-1">Attach notes, links, images, and source text.</p>
            </div>
          </div>
          <button type="button" class="mt-4 w-full rounded-md bg-black px-5 py-3 text-sm font-bold text-white">Cultivate</button>
        </aside>
      </section>

      <section class="mt-8 rounded-lg border border-black/10 bg-white p-6 shadow-[0_18px_50px_rgba(20,20,20,0.06)] sm:p-8">
        <p class="text-xs font-black uppercase tracking-[0.18em] text-[#8b887f]">Settings</p>
        <h2 class="mt-2 text-4xl font-black">Configuration and health</h2>
        <div class="mt-8 grid gap-px overflow-hidden rounded-lg border border-black/10 bg-black/10 md:grid-cols-3">
          <article v-for="[title, body] in settingsItems" :key="title" class="bg-white p-6">
            <h3 class="text-xl font-black">{{ title }}</h3>
            <p class="mt-3 text-sm leading-6 text-[#706c63]">{{ body }}</p>
          </article>
        </div>
      </section>
    </section>
  </main>
</template>
