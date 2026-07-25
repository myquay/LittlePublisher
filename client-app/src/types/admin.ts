export interface PublishJob {
  id: string
  userMe: string
  clientId?: string | null
  action: string
  status: string
  publishedUrl?: string | null
  error?: string | null
  requestJson: string
  createdUtc: string
  updatedUtc: string
}

export interface Post {
  id: string
  title?: string | null
  content: string
  summary?: string | null
  categories: string[]
  slug: string
  postType: 'article' | 'note'
  state: string
  workingRevision: number
  publishedRevision?: number | null
  requestedPublishedUtc?: string | null
  publishedUtc?: string | null
  publishedUrl?: string | null
  filePath?: string | null
  commitSha?: string | null
  lastPublishError?: string | null
  sourceRepositoryPath?: string | null
  sourceCommitSha?: string | null
  createdUtc: string
  updatedUtc: string
  eTag: string
  hasUnpublishedChanges: boolean
}

export interface AdminDashboard {
  jobs: PublishJob[]
  posts: Post[]
}

export interface AdminCheck {
  ok: boolean
  message: string
}

export interface ImportRepositoryRequest {
  overwrite: boolean
  dryRun: boolean
}

export interface ImportRepositoryError {
  filePath: string
  message: string
}

export interface ImportRepositoryResult {
  scanned: number
  imported: number
  skipped: number
  failed: number
  errors: ImportRepositoryError[]
  ambiguous: number
  draftsInRepository: number
  draftFiles: string[]
}

export interface SavePostRequest {
  title?: string | null
  content: string
  summary?: string | null
  categories: string[]
  slug?: string | null
  postType: 'article' | 'note'
  requestedPublishedUtc?: string | null
}
