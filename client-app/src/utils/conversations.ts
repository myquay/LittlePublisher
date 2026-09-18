export const conversationTemplate = `{{< conversation title="Conversation" >}}

{{< message role="human" >}}
Your question here.
{{< /message >}}

{{< message role="ai" >}}
The reply here.
{{< /message >}}

{{< /conversation >}}`

type Attributes = Record<string, string>
export type Conversation = {
  attributes: Attributes
  messages: { attributes: Attributes; body: string }[]
}
export type ConversationPart = string | Conversation

// Parse the blog's line-oriented authoring format without changing the saved source.
export function parseConversations(source: string): { parts: ConversationPart[]; error: string } {
  const parts: ConversationPart[] = []
  let prose: string[] = []
  let conversation: Conversation | undefined
  let message: Conversation['messages'][number] | undefined
  let fence = ''
  const fail = (line: number, reason: string) => ({
    parts: [source],
    error: `Conversation on line ${line}: ${reason}`,
  })
  const lines = source.replace(/\r\n?/g, '\n').split('\n')
  for (const [index, line] of lines.entries()) {
    const number = index + 1
    const marker = line.match(/^ {0,3}(`{3,}|~{3,})/)
    const inCode = Boolean(fence || marker)
    if (fence) {
      if (new RegExp(`^ {0,3}${fence[0]}{${fence.length},}\\s*$`).test(line)) fence = ''
    } else if (marker) fence = marker[1]!
    const tag = !inCode && line.match(/^\s*{{<\s*(\/?)\s*(conversation|message)\b(.*?)>}}\s*$/)
    if (!tag) {
      if (!inCode && /^\s*{{[<%]\s*\/?\s*(conversation|message)\b/.test(line))
        return fail(number, 'Put each complete {{< shortcode >}} on its own line.')
      if (message) message.body += `${line}\n`
      else if (conversation && line.trim())
        return fail(number, 'Only messages may appear inside a conversation.')
      else if (!conversation) prose.push(line)
      continue
    }
    const closing = Boolean(tag[1])
    const kind = tag[2]
    let rest = tag[3]!.trim()
    const attributes: Attributes = {}
    if (closing && rest) return fail(number, 'Closing tags cannot have parameters.')
    while (rest) {
      const attr = rest.match(/^(\w+)\s*=\s*(?:"((?:\\.|[^"\\])*)"|`([^`]*)`|([^\s"`]+))(?:\s+|$)/)
      if (!attr) return fail(number, 'Use named parameters such as role="human".')
      attributes[attr[1]!] = (attr[2] ?? attr[3] ?? attr[4]!).replace(/\\(["\\])/g, '$1')
      rest = rest.slice(attr[0].length)
    }
    if (kind === 'conversation') {
      if (closing) {
        if (!conversation || message)
          return fail(number, 'Close each message before closing its conversation.')
        if (!conversation.messages.length) return fail(number, 'Add at least one message.')
        parts.push(conversation)
        conversation = undefined
      } else {
        if (conversation) return fail(number, 'Conversations cannot be nested.')
        if (prose.length) parts.push(prose.join('\n'))
        prose = []
        conversation = { attributes, messages: [] }
      }
    } else if (closing) {
      if (!message) return fail(number, 'No open message to close.')
      if (!message.body.trim()) return fail(number, 'Messages need Markdown content.')
      message = undefined
    } else {
      if (!conversation || message)
        return fail(number, 'Messages must be directly inside a conversation.')
      if (!['human', 'ai', 'system'].includes(attributes.role || ''))
        return fail(number, 'Message role must be human, ai, or system.')
      message = { attributes, body: '' }
      conversation.messages.push(message)
    }
  }
  if (message || conversation)
    return fail(lines.length, `Missing closing ${message ? 'message' : 'conversation'} tag.`)
  if (prose.length) parts.push(prose.join('\n'))
  return { parts, error: '' }
}
