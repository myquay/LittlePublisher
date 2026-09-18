// A deliberately small, HTML-free reading preview. Publishing preserves the original Markdown.
export function markdownPreview(source: string) {
  const escape = (text: string) =>
    text
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;')
  const inline = (text: string) =>
    escape(text)
      .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
      .replace(/\*(.+?)\*/g, '<em>$1</em>')
      .replace(/`([^`]+)`/g, '<code>$1</code>')
      .replace(
        /\[([^\]]+)\]\((https?:\/\/[^\s)]+)\)/g,
        '<a href="$2" target="_blank" rel="noopener noreferrer">$1</a>',
      )
  return source
    .split(/\n\s*\n/)
    .map((block) => {
      if (block.startsWith('```'))
        return (
          '<pre><code>' +
          escape(block.replace(/^```[^\n]*\n?/, '').replace(/\n?```$/, '')) +
          '</code></pre>'
        )
      const heading = block.match(/^(#{1,6}) (.*)$/s)
      if (heading) return `<h${heading[1]!.length}>${inline(heading[2]!)}</h${heading[1]!.length}>`
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
