<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { AppButton, AppSurface, EmptyState, LoadingState, StatusBadge } from '@/components'
import { webmentionService } from '@/services/webmentionService'
import type { IncomingWebmention, OutgoingWebmention, SitePage } from '@/types/webmentions'

type Tab = 'incoming' | 'outgoing' | 'create'
const tab = ref<Tab>('incoming')
const incoming = ref<IncomingWebmention[]>([])
const outgoing = ref<OutgoingWebmention[]>([])
const pages = ref<SitePage[]>([])
const loading = ref(true)
const workingId = ref('')
const message = ref('')
const error = ref('')
const incomingFilter = ref('')
const outgoingFilter = ref('')
const scanSource = ref('')
const createType = ref<'like' | 'reply' | 'blogroll'>('like')
const targetUrl = ref('')
const comment = ref('')
const targetTitle = ref('')
const blogName = ref('')
const feedUrl = ref('')

const visibleIncoming = computed(() => incomingFilter.value ? incoming.value.filter(x => x.state === incomingFilter.value) : incoming.value)
const visibleOutgoing = computed(() => outgoingFilter.value ? outgoing.value.filter(x => x.state === outgoingFilter.value) : outgoing.value)

onMounted(load)
async function load() {
  loading.value = true; error.value = ''
  try { [incoming.value, outgoing.value, pages.value] = await Promise.all([webmentionService.incoming(), webmentionService.outgoing(), webmentionService.pages()]) }
  catch (e) { error.value = readError(e) }
  finally { loading.value = false }
}

async function act(id: string, action: () => Promise<unknown>, success: string) {
  workingId.value = id; error.value = ''; message.value = ''
  try { await action(); message.value = success; await load() }
  catch (e) { error.value = readError(e) }
  finally { workingId.value = '' }
}
async function approve(item: IncomingWebmention) { await act(item.id, () => webmentionService.approveIncoming(item.id), 'Webmention approved and queued for site publication.') }
async function reject(item: IncomingWebmention, block = false) {
  const reason = window.prompt(block ? 'Reason for blocking this source domain?' : 'Reason for rejecting this Webmention?') ?? ''
  await act(item.id, () => webmentionService.rejectIncoming(item.id, reason, block), block ? 'Domain blocked.' : 'Webmention rejected.')
}
async function scan() { if (scanSource.value) await act('scan', () => webmentionService.scan(scanSource.value), 'The deployed page was scanned and its drafts were reconciled.') }
async function createContent() {
  await act('create', async () => {
    if (createType.value === 'like') return webmentionService.createLike(targetUrl.value, comment.value)
    if (createType.value === 'reply') return webmentionService.createReply(targetUrl.value, comment.value, targetTitle.value)
    return webmentionService.createBlogroll(blogName.value, targetUrl.value, feedUrl.value, comment.value)
  }, 'Content committed. Its Webmention draft will appear after the site deployment is ingested.')
}
function formatDate(value?: string | null) { return value ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : '—' }
function host(value: string) { try { return new URL(value).hostname } catch { return value } }
function titleCase(value: string) { return value.charAt(0).toUpperCase() + value.slice(1) }
function tone(state: string) { if (state === 'approved' || state === 'sent') return 'success'; if (state === 'failed' || state === 'invalid' || state === 'rejected') return 'danger'; if (state.includes('pending') || state === 'draft' || state === 'withdrawal-draft') return 'warning'; return 'neutral' }
function readError(e: unknown) {
  if (e && typeof e === 'object' && 'response' in e) { const response = (e as { response?: { data?: { error?: string; message?: string } } }).response; return response?.data?.error || response?.data?.message || 'The operation failed.' }
  return 'The operation failed.'
}
</script>

<template>
  <main class="lp-shell space-y-6">
    <AppSurface padding="lg">
      <div class="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div><p class="lp-kicker">Webmentions</p><h1 class="lp-heading mt-2 text-3xl">Conversation across the web</h1><p class="lp-copy mt-2 text-sm">Review incoming responses and choose when outgoing mentions are sent.</p></div>
        <AppButton variant="secondary" :loading="loading" @click="load">Refresh</AppButton>
      </div>
      <div class="mt-5 flex flex-wrap gap-2" role="tablist">
        <AppButton v-for="value in (['incoming','outgoing','create'] as Tab[])" :key="value" :variant="tab === value ? 'primary' : 'secondary'" @click="tab = value">{{ titleCase(value) }}</AppButton>
      </div>
      <p v-if="message" class="mt-4 rounded-md border border-lp-success/20 bg-lp-success-soft p-3 text-sm font-semibold text-lp-success">{{ message }}</p>
      <p v-if="error" class="mt-4 rounded-md border border-lp-danger/20 bg-lp-danger-soft p-3 text-sm font-semibold text-lp-danger">{{ error }}</p>
    </AppSurface>

    <LoadingState v-if="loading" label="Loading Webmentions..." />

    <AppSurface v-else-if="tab === 'incoming'" padding="lg">
      <div class="flex items-center justify-between gap-4"><div><p class="lp-kicker">Moderation</p><h2 class="lp-heading mt-2 text-2xl">Incoming queue</h2></div><select v-model="incomingFilter" class="rounded-md border border-lp-border bg-lp-surface px-3 py-2 text-sm"><option value="">All states</option><option v-for="state in ['pending-review','update-pending-review','approved','rejected','invalid','withdrawn']" :key="state">{{ state }}</option></select></div>
      <EmptyState v-if="!visibleIncoming.length" class="mt-6" title="No incoming Webmentions in this view." description="New verified responses will wait here until you approve or reject them." />
      <div v-else class="mt-6 space-y-4">
        <article v-for="item in visibleIncoming" :key="item.id" class="rounded-md border border-lp-border bg-lp-surface-soft p-4">
          <div class="flex flex-wrap items-start justify-between gap-3"><div><p class="font-black text-lp-ink">{{ item.authorName || item.title || host(item.source) }}</p><p class="mt-1 text-xs font-semibold text-lp-subtle">{{ item.detectedType || 'mention' }} · {{ formatDate(item.updatedUtc) }}</p></div><StatusBadge :tone="tone(item.state)">{{ item.state }}</StatusBadge></div>
          <p v-if="item.displayContent" class="mt-3 whitespace-pre-line text-sm text-lp-muted">{{ item.displayContent }}</p>
          <dl class="mt-3 grid gap-2 text-xs sm:grid-cols-2"><div><dt class="font-black">Source</dt><dd><a :href="item.source" class="break-all text-lp-info">{{ item.source }}</a></dd></div><div><dt class="font-black">Target</dt><dd><a :href="item.target" class="break-all text-lp-info">{{ item.target }}</a></dd></div></dl>
          <p v-if="item.verificationEvidence" class="mt-3 text-xs text-lp-subtle">{{ item.verificationEvidence }}</p><p v-if="item.failureReason" class="mt-3 text-sm font-semibold text-lp-danger">{{ item.failureReason }}</p>
          <div class="mt-4 flex flex-wrap gap-2"><AppButton v-if="['pending-review','update-pending-review'].includes(item.state)" size="sm" :loading="workingId === item.id" @click="approve(item)">Approve</AppButton><AppButton v-if="!['rejected','withdrawn'].includes(item.state)" size="sm" variant="secondary" @click="reject(item)">Reject</AppButton><AppButton size="sm" variant="secondary" @click="act(item.id, () => webmentionService.reverifyIncoming(item.id), 'Reverification queued.')">Reverify</AppButton><AppButton size="sm" variant="secondary" @click="reject(item, true)">Block domain</AppButton></div>
        </article>
      </div>
    </AppSurface>

    <AppSurface v-else-if="tab === 'outgoing'" padding="lg">
      <div class="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between"><div><p class="lp-kicker">Manual sending</p><h2 class="lp-heading mt-2 text-2xl">Outgoing drafts</h2></div><select v-model="outgoingFilter" class="rounded-md border border-lp-border bg-lp-surface px-3 py-2 text-sm"><option value="">All states</option><option v-for="state in ['draft','withdrawal-draft','failed','sent','declined','no-endpoint','superseded']" :key="state">{{ state }}</option></select></div>
      <div class="mt-5 flex gap-2"><select v-model="scanSource" class="min-w-0 flex-1 rounded-md border border-lp-border bg-lp-surface px-3 py-2 text-sm"><option value="">Select a deployed page to scan</option><option v-for="page in pages" :key="page.sourceUrl" :value="page.sourceUrl">{{ page.title || page.sourceUrl }}</option></select><AppButton :disabled="!scanSource" :loading="workingId === 'scan'" @click="scan">Scan page</AppButton></div>
      <EmptyState v-if="!visibleOutgoing.length" class="mt-6" title="No outgoing Webmentions in this view." description="Deployment manifests and manual scans create view-only drafts here." />
      <div v-else class="mt-6 space-y-4"><article v-for="item in visibleOutgoing" :key="item.id" class="rounded-md border border-lp-border bg-lp-surface-soft p-4"><div class="flex flex-wrap items-start justify-between gap-3"><div><p class="font-black text-lp-ink">{{ item.sourceTitle || item.source }}</p><p class="mt-1 text-xs font-semibold text-lp-subtle">{{ item.relationship }} · {{ item.intent }} · observed {{ item.revisionCount }} time(s)</p></div><StatusBadge :tone="tone(item.state)">{{ item.state }}</StatusBadge></div><p v-if="item.context" class="mt-3 text-sm text-lp-muted">{{ item.context }}</p><p class="mt-3 break-all text-sm"><span class="font-black">Target:</span> <a :href="item.target" class="text-lp-info">{{ item.target }}</a></p><p v-if="item.endpoint" class="mt-2 break-all text-xs text-lp-subtle">Endpoint: {{ item.endpoint }} ({{ item.discoveryMethod }})</p><p v-if="item.failureReason" class="mt-2 text-sm font-semibold text-lp-danger">{{ item.failureReason }}</p><div class="mt-4 flex flex-wrap gap-2"><AppButton v-if="['draft','withdrawal-draft'].includes(item.state)" size="sm" :loading="workingId === item.id" @click="act(item.id, () => webmentionService.sendOutgoing(item.id), 'Webmention queued for sending.')">Approve and send</AppButton><AppButton v-if="item.state === 'failed' || item.state === 'no-endpoint'" size="sm" @click="act(item.id, () => webmentionService.retryOutgoing(item.id), 'Retry queued.')">Retry</AppButton><AppButton v-if="!['sent','declined','superseded'].includes(item.state)" size="sm" variant="secondary" @click="act(item.id, () => webmentionService.declineOutgoing(item.id), 'Draft declined.')">Decline</AppButton></div></article></div>
    </AppSurface>

    <AppSurface v-else padding="lg">
      <p class="lp-kicker">Create activity</p><h2 class="lp-heading mt-2 text-2xl">Like, reply, or blogroll entry</h2><p class="lp-copy mt-2 text-sm">This commits public source content first. Sending remains unavailable until that page has deployed and produced a draft.</p>
      <form class="mt-6 max-w-2xl space-y-4" @submit.prevent="createContent"><label class="block text-sm font-black">Content type<select v-model="createType" class="mt-1 block w-full rounded-md border border-lp-border bg-lp-surface px-3 py-2"><option value="like">Like</option><option value="reply">Reply</option><option value="blogroll">Add to blogroll</option></select></label><label v-if="createType === 'blogroll'" class="block text-sm font-black">Blog name<input v-model="blogName" required class="mt-1 block w-full rounded-md border border-lp-border bg-lp-surface px-3 py-2" /></label><label class="block text-sm font-black">{{ createType === 'blogroll' ? 'Site URL' : 'Target URL' }}<input v-model="targetUrl" type="url" required class="mt-1 block w-full rounded-md border border-lp-border bg-lp-surface px-3 py-2" /></label><label v-if="createType === 'reply'" class="block text-sm font-black">Target title<input v-model="targetTitle" class="mt-1 block w-full rounded-md border border-lp-border bg-lp-surface px-3 py-2" /></label><label v-if="createType === 'blogroll'" class="block text-sm font-black">Feed URL (optional)<input v-model="feedUrl" type="url" class="mt-1 block w-full rounded-md border border-lp-border bg-lp-surface px-3 py-2" /></label><label class="block text-sm font-black">{{ createType === 'reply' ? 'Reply' : 'Comment (optional)' }}<textarea v-model="comment" :required="createType === 'reply'" rows="5" class="mt-1 block w-full rounded-md border border-lp-border bg-lp-surface px-3 py-2" /></label><AppButton type="submit" :loading="workingId === 'create'">Commit content</AppButton></form>
    </AppSurface>
  </main>
</template>
