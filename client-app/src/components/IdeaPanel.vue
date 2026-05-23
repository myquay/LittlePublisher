<script setup lang="ts">
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

interface ResearchLink {
  title: string
  url: string
}

defineProps<{
  attachments: string[]
  dropNotice: string
  maturityLevels: MaturityLevel[]
  panelTab: PanelTab
  permanent?: boolean
  researchLinks: ResearchLink[]
  selectedIdea: Idea
  selectedMaturity: MaturityLevel
}>()

defineEmits<{
  'drop-file': [event: DragEvent]
  'set-maturity': [value: MaturityKey]
  'set-tab': [tab: PanelTab]
}>()
</script>

<template>
  <section class="rounded-lg border border-black/10 bg-white p-5 shadow-[0_18px_50px_rgba(20,20,20,0.06)]">
    <div class="flex items-start justify-between gap-4">
      <div>
        <p class="text-xs font-black uppercase tracking-[0.16em] text-[#8b887f]">
          {{ permanent ? 'Writing kit' : 'Idea panel' }}
        </p>
        <h2 class="mt-2 text-2xl font-black">{{ selectedIdea.title }}</h2>
        <p class="mt-2 text-sm text-[#706c63]">{{ selectedIdea.type }} / {{ selectedMaturity.label }}</p>
      </div>
      <img
        class="h-16 w-16 shrink-0 object-contain"
        :alt="selectedMaturity.label"
        :src="selectedMaturity.stampUrl"
      />
    </div>

    <div class="mt-5 grid grid-cols-2 gap-2 rounded-md bg-[#f7f6f2] p-1">
      <button
        type="button"
        class="rounded-md px-3 py-2 text-sm font-bold"
        :class="panelTab === 'research' ? 'bg-white shadow-sm' : 'text-[#706c63]'"
        @click="$emit('set-tab', 'research')"
      >
        Research
      </button>
      <button
        type="button"
        class="rounded-md px-3 py-2 text-sm font-bold"
        :class="panelTab === 'controls' ? 'bg-white shadow-sm' : 'text-[#706c63]'"
        @click="$emit('set-tab', 'controls')"
      >
        Controls
      </button>
    </div>

    <div v-if="panelTab === 'research'" class="mt-5 space-y-4">
      <div
        class="rounded-lg border border-dashed border-black/20 bg-[#fbfaf7] p-5 text-sm leading-6 text-[#706c63]"
        @dragover.prevent
        @drop.prevent="$emit('drop-file', $event)"
      >
        <p class="font-bold text-black">Drop research here</p>
        <p class="mt-1">Attach notes, images, links, and text blobs to the idea.</p>
        <p v-if="dropNotice" class="mt-3 rounded-md bg-white px-3 py-2 font-semibold text-[#25724f]">
          {{ dropNotice }}
        </p>
      </div>

      <div>
        <h3 class="text-sm font-black uppercase tracking-[0.16em] text-[#8b887f]">Research links</h3>
        <div class="mt-3 space-y-2">
          <article v-for="link in researchLinks" :key="link.url" class="rounded-md bg-[#fbfaf7] p-3">
            <p class="font-bold">{{ link.title }}</p>
            <p class="mt-1 truncate text-xs text-[#706c63]">{{ link.url }}</p>
          </article>
        </div>
      </div>

      <div>
        <h3 class="text-sm font-black uppercase tracking-[0.16em] text-[#8b887f]">Attachments</h3>
        <div class="mt-3 flex flex-wrap gap-2">
          <span
            v-for="attachment in attachments"
            :key="attachment"
            class="rounded-full border border-black/10 px-3 py-1 text-xs font-semibold text-[#706c63]"
          >
            {{ attachment }}
          </span>
        </div>
      </div>
    </div>

    <div v-else class="mt-5 space-y-4">
      <label class="block">
        <span class="text-xs font-black uppercase tracking-[0.16em] text-[#8b887f]">Maturity</span>
        <select
          class="mt-2 w-full rounded-md border border-black/10 bg-[#fbfaf7] px-4 py-3 text-sm outline-none focus:border-black"
          :value="selectedIdea.maturity"
          @change="$emit('set-maturity', ($event.target as HTMLSelectElement).value as MaturityKey)"
        >
          <option v-for="level in maturityLevels" :key="level.key" :value="level.key">
            {{ level.label }}
          </option>
        </select>
      </label>
      <label class="block">
        <span class="text-xs font-black uppercase tracking-[0.16em] text-[#8b887f]">Published status</span>
        <input
          class="mt-2 w-full rounded-md border border-black/10 bg-[#fbfaf7] px-4 py-3 text-sm outline-none focus:border-black"
          :value="selectedIdea.status"
        />
      </label>
      <label class="block">
        <span class="text-xs font-black uppercase tracking-[0.16em] text-[#8b887f]">Folder</span>
        <input
          class="mt-2 w-full rounded-md border border-black/10 bg-[#fbfaf7] px-4 py-3 text-sm outline-none focus:border-black"
          :value="selectedIdea.folder"
        />
      </label>
      <label class="block">
        <span class="text-xs font-black uppercase tracking-[0.16em] text-[#8b887f]">Filename</span>
        <input
          class="mt-2 w-full rounded-md border border-black/10 bg-[#fbfaf7] px-4 py-3 text-sm outline-none focus:border-black"
          :value="selectedIdea.filename"
        />
      </label>
    </div>
  </section>
</template>
