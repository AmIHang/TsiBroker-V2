// Small helpers for pulling identifying info out of a logged message's raw XML - shared by
// ReceivedMessagesView's reply action and SendMessageView's duplicate action, both of which
// need to read Sender/Recipient/message type off an already-sent-or-received message.

export function extractTag(xml: string, tag: string): string | null {
  const match = new RegExp(`<${tag}>([^<]*)</${tag}>`).exec(xml)
  const value = match?.[1]?.trim()
  return value ? value : null
}

// The message type is just the payload's root element name (see MessageTemplates.ByMessageType
// on the sending side, whose keys match these verbatim).
export function extractMessageType(xml: string): string | null {
  return /^\s*<([A-Za-z]+)[\s>]/.exec(xml)?.[1] ?? null
}
