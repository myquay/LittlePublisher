export interface IncomingWebmention {
  id: string
  source: string
  target: string
  state: string
  detectedType?: string | null
  presentationType?: string | null
  authorName?: string | null
  authorUrl?: string | null
  title?: string | null
  displayContent?: string | null
  verificationEvidence?: string | null
  failureReason?: string | null
  moderationReason?: string | null
  firstSeenUtc: string
  verifiedUtc?: string | null
  updatedUtc: string
}

export interface OutgoingWebmention {
  id: string
  source: string
  target: string
  state: string
  intent: string
  relationship: string
  sourceTitle?: string | null
  anchorText?: string | null
  context?: string | null
  latestSourceHash: string
  latestDeploymentSha: string
  endpoint?: string | null
  discoveryMethod?: string | null
  reason: string
  revisionCount: number
  firstDiscoveredUtc: string
  lastObservedUtc: string
  lastSentUtc?: string | null
  failureReason?: string | null
  updatedUtc: string
}

export interface SitePage {
  sourceUrl: string
  title?: string | null
  contentHash: string
  deploymentSha: string
  acceptsWebmentions: boolean
}

export interface WebmentionList<T> { items: T[] }
