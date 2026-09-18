import type { PostType } from '@/types/admin'
export interface ContentField {
  key: string
  label: string
  required?: boolean
}
export interface ContentType {
  label: string
  fields: ContentField[]
  media?: 'photo' | 'audio' | 'video'
  accept?: string
}
const field = (key: string, label: string, required = false): ContentField => ({
  key,
  label,
  required,
})
export const contentTypes: Record<PostType, ContentType> = {
  article: { label: 'Article', fields: [] },
  note: { label: 'Note', fields: [] },
  photo: {
    label: 'Photo',
    media: 'photo',
    accept: 'image/jpeg,image/png,image/gif,image/webp',
    fields: [
      field('photo', 'Photo URL', true),
      field('alt', 'Alternative text', true),
      field('location', 'Location'),
    ],
  },
  activity: { label: 'Activity', fields: [] },
  thought: { label: 'Thought', fields: [] },
  reply: {
    label: 'Reply',
    fields: [
      field('in-reply-to', 'Reply to URL', true),
      field('reply-to-title', 'Original post title'),
    ],
  },
  like: { label: 'Like', fields: [field('like-of', 'Liked URL', true)] },
  repost: { label: 'Repost', fields: [field('repost-of', 'Original URL', true)] },
  bookmark: { label: 'Bookmark', fields: [field('bookmark-of', 'Bookmark URL', true)] },
  blogroll: {
    label: 'Blogroll entry',
    fields: [field('url', 'Website URL', true), field('feed', 'Feed URL')],
  },
  event: {
    label: 'Event',
    fields: [field('start', 'Starts', true), field('end', 'Ends'), field('location', 'Location')],
  },
  audio: {
    label: 'Audio',
    media: 'audio',
    accept: 'audio/mpeg,audio/ogg',
    fields: [field('audio', 'Audio URL', true), field('alt', 'Description')],
  },
  video: {
    label: 'Video',
    media: 'video',
    accept: 'video/mp4,video/webm',
    fields: [field('video', 'Video URL', true), field('alt', 'Description')],
  },
}
export function safeUrl(value?: string | null): string | undefined {
  if (!value) return undefined
  if (/^\/(?![\\/])/.test(value)) return value
  try {
    const url = new URL(value)
    return ['https:', 'http:'].includes(url.protocol) ? value : undefined
  } catch {
    return undefined
  }
}
export function readError(error: unknown) {
  const response = (error as { response?: { data?: { message?: string } } })?.response
  return response?.data?.message || 'The request could not be completed. Please try again.'
}
