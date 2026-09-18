import { describe, expect, it } from 'vitest'
import { conversationTemplate, parseConversations } from './conversations'
import { markdownPreview } from './markdownPreview'

const message = (body = 'Hello', attrs = 'role="ai"') =>
  `{{< message ${attrs} >}}\n${body}\n{{< /message >}}`
const conversation = (body: string, attrs = '') =>
  `{{< conversation ${attrs} >}}\n${body}\n{{< /conversation >}}`

describe('conversation authoring', () => {
  it('accepts the insertion template and preserves surrounding prose and separate exchanges', () => {
    const source = `Before\n\n${conversationTemplate}\n\nBetween\n\n${conversation(message())}\n\nAfter`
    expect(parseConversations(source).error).toBe('')
    const root = document.createElement('div')
    root.innerHTML = markdownPreview(source)
    expect(root.querySelectorAll('figure')).toHaveLength(2)
    expect([...root.querySelectorAll('.conversation-speaker')].map((el) => el.textContent)).toEqual(
      ['Human 01', 'AI 02', 'AI 01'],
    )
    expect(root.textContent).toContain('Before')
    expect(root.textContent).toContain('Between')
    expect(root.textContent).toContain('After')
  })

  it('renders roles and Markdown bodies while escaping metadata and raw HTML', () => {
    const source = conversation(
      message('Be concise.', 'role="system"') +
        '\n' +
        message(
          '**Hello**\n\n- One\n- Two\n\n```js\nconst x = "<safe>";\n\n// still code\n```\n\n<script>alert(1)</script>',
          'role="human" name="<img src=x onerror=alert(1)>" model="<b>model</b>"',
        ),
      'title="<b>Title</b>" note="<script>Note</script>"',
    )
    const root = document.createElement('div')
    root.innerHTML = markdownPreview(source)
    expect(root.querySelector('figcaption')?.textContent).toBe('<b>Title</b>')
    expect(root.querySelector('.conversation-footnote')?.textContent).toBe('<script>Note</script>')
    expect(root.querySelectorAll('script, img, b')).toHaveLength(0)
    expect(root.querySelector('pre code')?.textContent).toBe('const x = "<safe>";\n\n// still code')
    expect(root.querySelectorAll('ul li')).toHaveLength(2)
    expect(root.textContent).toContain('Human · <b>model</b> · 02')
  })

  it.each([
    [message(), 'directly inside'],
    [conversation(''), 'at least one'],
    [conversation('Loose prose'), 'Only messages'],
    [conversation(message('', 'role="ai"')), 'Markdown content'],
    [conversation(message('Hello', 'role="robot"')), 'role must'],
    [conversation(message('Hello', 'name="Me"')), 'role must'],
    [conversation(conversation(message())), 'cannot be nested'],
    [conversation(message(message())), 'directly inside'],
    ['{{< conversation >}}', 'Missing closing'],
    ['{{< conversation >}}\n{{< message role="ai" >}}\nHello', 'Missing closing message'],
    ['{{< /message >}}', 'No open message'],
    ['{{< conversation title="Unfinished >}}', 'named parameters'],
  ])('reports invalid source without losing it: %s', (source, reason) => {
    const result = parseConversations(source)
    expect(result.error).toContain(reason)
    expect(result.error).toContain('line ')
    expect(result.parts).toEqual([source])
  })

  it('leaves fenced examples as code and accepts CRLF and unquoted roles', () => {
    expect(parseConversations('```md\n' + conversationTemplate + '\n```').parts).toHaveLength(1)
    expect(
      parseConversations(conversation(message('Hello', 'role=ai')).replace(/\n/g, '\r\n')).error,
    ).toBe('')
  })
})
