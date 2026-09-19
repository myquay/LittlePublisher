import { safeUrl } from './contentTypes'
import { parseConversations } from './conversations'

// Split only unescaped pipes, including the optional outer borders.
function tableCells(line: string): string[] | undefined {
  const cells: string[] = []
  let cell = ''
  let hasPipe = false
  for (let index = 0; index < line.length; index++) {
    const char = line[index]!
    if (char === '\\' && index + 1 < line.length) {
      const next = line[++index]!
      cell += next === '|' ? '|' : char + next
    } else if (char === '|') {
      cells.push(cell.trim())
      cell = ''
      hasPipe = true
    } else cell += char
  }
  if (!hasPipe) return undefined
  cells.push(cell.trim())
  if (line.trimStart().startsWith('|')) cells.shift()
  if (cells[cells.length - 1] === '') cells.pop()
  return cells
}

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
        const lines = block.split('\n')
        for (let index = 0; index < lines.length - 1; index++) {
          const headers = tableCells(lines[index]!)
          const separators = tableCells(lines[index + 1]!)
          if (
            !headers?.length ||
            separators?.length !== headers.length ||
            !separators.every((cell) => /^:?-+:?$/.test(cell))
          )
            continue
          const alignments = separators.map((cell) =>
            cell.endsWith(':') ? (cell.startsWith(':') ? 'center' : 'right') : 'left',
          )
          const row = (cells: string[], tag: 'th' | 'td') =>
            '<tr>' +
            headers
              .map(
                (_, column) =>
                  `<${tag}${tag === 'th' ? ' scope="col"' : ''} style="text-align: ${alignments[column]}">${inline(cells[column] || '')}</${tag}>`,
              )
              .join('') +
            '</tr>'
          const rows: string[] = []
          let end = index + 2
          while (end < lines.length) {
            const cells = tableCells(lines[end]!)
            if (!cells || /^ {0,3}(?:#{1,6} |>|[-+*] |\d+[.)] )/.test(lines[end]!)) break
            rows.push(row(cells, 'td'))
            end++
          }
          return (
            markdown(lines.slice(0, index).join('\n')) +
            '<div class="table-scroll" tabindex="0" role="region" aria-label="Table">' +
            `<table><thead>${row(headers, 'th')}</thead><tbody>${rows.join('')}</tbody></table></div>` +
            markdown(lines.slice(end).join('\n'))
          )
        }
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
