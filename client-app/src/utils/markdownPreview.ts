import { safeUrl } from './contentTypes'
import { parseConversations } from './conversations'

// A deliberately small, HTML-free reading preview. Publishing preserves the original Markdown.
export function markdownPreview(
  source: string,
  resolveImage: (url: string) => string | undefined = safeUrl,
) {
  const escape = (text: string) =>
    text
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;')
  const inline = (text: string) => {
    const images: string[] = []
    const withImages = text.replace(
      /!\[((?:\\.|[^\]\\])*)\]\(([^\s)]+)\)/g,
      (_, alt: string, url: string) => {
        const src = resolveImage(url)
        images.push(
          src
            ? `<img src="${escape(src)}" alt="${escape(alt.replace(/\\([\[\]\\])/g, '$1'))}" />`
            : `<span>${escape(alt || 'Image preview loading…')}</span>`,
        )
        return `\u0000${images.length - 1}\u0000`
      },
    )
    return escape(withImages)
      .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
      .replace(/\*(.+?)\*/g, '<em>$1</em>')
      .replace(/`([^`]+)`/g, '<code>$1</code>')
      .replace(
        /\[([^\]]+)\]\((https?:\/\/[^\s)]+)\)/g,
        '<a href="$2" target="_blank" rel="noopener noreferrer">$1</a>',
      )
      .replace(/\u0000(\d+)\u0000/g, (_, index: string) => images[Number(index)]!)
  }
  const markdown = (source: string): string => {
    const blocks: string[] = []
    let block: string[] = []
    let fence = ''
    for (const line of source.split(/\r?\n/)) {
      const marker = line.match(/^ {0,3}(`{3,}|~{3,})/)
      if (fence) {
        block.push(line)
        if (new RegExp(`^ {0,3}${fence[0]}{${fence.length},}\\s*$`).test(line)) {
          blocks.push(block.join('\n'))
          block = []
          fence = ''
        }
      } else if (marker) {
        if (block.length) blocks.push(block.join('\n'))
        block = [line]
        fence = marker[1]!
      } else if (!line.trim()) {
        if (block.length) blocks.push(block.join('\n'))
        block = []
      } else block.push(line)
    }
    if (block.length) blocks.push(block.join('\n'))
    return blocks
      .map((block) => {
        if (/^ {0,3}(`{3,}|~{3,})/.test(block))
          return (
            '<pre><code>' +
            escape(
              block
                .replace(/^ {0,3}(`{3,}|~{3,})[^\n]*\n?/, '')
                .replace(/\n? {0,3}(`{3,}|~{3,})\s*$/, ''),
            ) +
            '</code></pre>'
          )
        const heading = block.match(/^(#{1,6}) (.*)$/s)
        if (heading)
          return `<h${heading[1]!.length}>${inline(heading[2]!)}</h${heading[1]!.length}>`
        if (block.startsWith('> '))
          return '<blockquote>' + inline(block.replace(/^> /gm, '')) + '</blockquote>'
        if (block.split('\n').every((line) => line.startsWith('- ')))
          return (
            '<ul>' +
            block
              .split('\n')
              .map((line) => '<li>' + inline(line.slice(2)) + '</li>')
              .join('') +
            '</ul>'
          )
        return '<p>' + inline(block) + '</p>'
      })
      .join('')
  }
  return parseConversations(source)
    .parts.map((part) => {
      if (typeof part === 'string') return markdown(part)
      const title = escape(part.attributes.title?.trim() || 'Conversation')
      const messages = part.messages
        .map(({ attributes, body }, index) => {
          const role = { human: 'Human', ai: 'AI', system: 'System' }[attributes.role!]!
          const name = attributes.name?.trim() || role
          const metadata = [
            name !== role ? role : '',
            attributes.model,
            String(index + 1).padStart(2, '0'),
          ]
            .filter(Boolean)
            .map((value) => escape(value!))
            .join(' · ')
          return `<li class="conversation-message"><div class="conversation-speaker"><strong>${escape(name)}</strong> <span>${metadata}</span></div><div class="conversation-body">${markdown(body)}</div></li>`
        })
        .join('')
      const note = part.attributes.note
        ? `<div class="conversation-footnote">${escape(part.attributes.note)}</div>`
        : ''
      return `<figure class="conversation"><figcaption>${title}</figcaption><ol class="conversation-messages" role="list">${messages}</ol>${note}</figure>`
    })
    .join('')
}
