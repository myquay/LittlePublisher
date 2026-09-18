import { describe, expect, it } from 'vitest'
import { markdownPreview } from './markdownPreview'

describe('image preview', () => {
  it('renders images with safe URLs and escaped alt text', () => {
    expect(markdownPreview('![a "quote"](/media/photo.png)')).toContain(
      '<img src="/media/photo.png" alt="a &quot;quote&quot;" />',
    )
    expect(markdownPreview('![bad](javascript:alert)')).not.toContain('<img')
    expect(markdownPreview('![a \\[bracket\\]](https://example.com/photo.png)')).toContain(
      'alt="a [bracket]"',
    )
  })
  it('keeps code samples as text and resolves staged images through the supplied resolver', () => {
    expect(markdownPreview('```\n![photo](https://example.com/photo.png)\n```')).not.toContain(
      '<img',
    )
    expect(
      markdownPreview('![photo](https://publisher.example.com/staged)', () => 'blob:private'),
    ).toContain('src="blob:private"')
  })
})
