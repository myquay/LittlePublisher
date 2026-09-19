import { describe, expect, it } from 'vitest'
import { markdownPreview } from './markdownPreview'

describe('table preview', () => {
  const render = (source: string) => {
    const root = document.createElement('div')
    root.innerHTML = markdownPreview(source)
    return root
  }

  it('renders headers, inline formatting, and aligned columns between paragraphs', () => {
    const root = render(
      'Before\n| Attempt | Time | Who |\n| :--- | ---: | :---: |\n| **01** | 142224ms | Me |\nAfter',
    )
    expect(root.querySelectorAll('th')).toHaveLength(3)
    expect(root.querySelector('th')?.getAttribute('scope')).toBe('col')
    expect(root.querySelectorAll('td')[1]?.style.textAlign).toBe('right')
    expect(root.querySelectorAll('td')[2]?.style.textAlign).toBe('center')
    expect(root.querySelector('td strong')?.textContent).toBe('01')
    expect([...root.querySelectorAll('p')].map((p) => p.textContent)).toEqual(['Before', 'After'])
    expect(root.querySelector('.table-scroll')?.getAttribute('tabindex')).toBe('0')
  })

  it('supports optional borders, escaped pipes, empty cells and uneven body rows', () => {
    const root = render('Name | Value\n--- | ---\n`a\\|b` |\nshort | value | ignored\n| only |')
    expect(root.querySelector('code')?.textContent).toBe('a|b')
    expect(
      [...root.querySelectorAll('tbody tr')].map((row) =>
        [...row.querySelectorAll('td')].map((cell) => cell.textContent),
      ),
    ).toEqual([
      ['a|b', ''],
      ['short', 'value'],
      ['only', ''],
    ])
  })

  it('keeps malformed tables and fenced examples as text', () => {
    for (const source of [
      '| Attempt | Time |\n|---|---|---:|\n|01|142224ms|Me|',
      '| A | B |\n| nope | --- |',
      '```md\n| A | B |\n| --- | --- |\n```',
      '~~~\n| A | B |\n| --- | --- |\n~~~',
    ])
      expect(render(source).querySelector('table')).toBeNull()
  })

  it('escapes HTML and unsafe links inside cells', () => {
    const root = render(
      '| A | B |\n| --- | --- |\n| <script>alert(1)</script> | [bad](javascript:alert) |',
    )
    expect(root.querySelector('script, a')).toBeNull()
    expect(root.querySelector('td')?.textContent).toBe('<script>alert(1)</script>')
  })
})

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
