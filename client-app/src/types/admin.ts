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

export interface PublishedItem {
  id: string
  url: string
  title?: string | null
  content: string
  categories: string[]
  publishedUtc: string
  filePath?: string | null
  commitSha?: string | null
  propertiesJson: string
}

export interface AdminDashboard {
  jobs: PublishJob[]
  items: PublishedItem[]
}

export interface AdminCheck {
  ok: boolean
  message: string
}
