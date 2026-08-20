// Pretty-prints XML for display in the mock UIs, which log messages exactly as they came off
// the wire (usually a single unbroken line). Falls back to the original string for anything
// that isn't XML (error text, JSON, ...) rather than mangling it.
export function formatXml(value: string): string {
  const trimmed = value.trim()
  if (!trimmed.startsWith('<')) {
    return value
  }

  const withBreaks = trimmed.replace(/(>)(<)(\/*)/g, '$1\n$2$3')
  let pad = 0

  const lines = withBreaks.split('\n').map((line) => {
    let indent = 0
    if (/.+<\/\w[^>]*>$/.test(line)) {
      indent = 0
    } else if (/^<\/\w/.test(line)) {
      pad = Math.max(0, pad - 1)
    } else if (/^<\w[^>]*[^/?]>.*$/.test(line)) {
      indent = 1
    }

    const padding = '  '.repeat(pad)
    pad += indent
    return padding + line
  })

  return lines.join('\n')
}
