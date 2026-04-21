/**
 * ELSyntaxHighlight — Syntax Highlighting for EasyLanguage
 *
 * Tokenizes and colorizes EasyLanguage code with basic syntax rules.
 * Highlights keywords, strings, numbers, comments, and operators.
 */

interface Token {
  type: 'keyword' | 'string' | 'number' | 'comment' | 'operator' | 'text'
  value: string
}

/**
 * Tokenizes EasyLanguage code for syntax highlighting.
 *
 * Rules:
 * - Keywords: inputs, variables, begin, end, if, then, else, buy, sell, etc.
 * - Strings: "..." or '...'
 * - Numbers: 0-9 with optional decimals
 * - Comments: // line comments
 * - Operators: + - * / = < > ( ) [ ] { } , ;
 */
function tokenizeEasyLanguage(code: string): Token[] {
  const tokens: Token[] = []
  const keywords = new Set([
    'inputs',
    'variables',
    'begin',
    'end',
    'if',
    'then',
    'else',
    'buy',
    'sell',
    'sellshort',
    'buytocover',
    'for',
    'to',
    'downto',
    'while',
    'do',
    'true',
    'false',
    'and',
    'or',
    'not',
    'value',
    'var',
    'array',
    'of',
    'plot',
    'print',
    'exitlong',
    'exitshort',
    'next',
    'bar',
    'at',
    'market',
    'netprofit',
    'daystoexpiration'
  ])

  const lines = code.split('\n')

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i]
    if (!line) {
      // Empty line - add newline token
      if (i < lines.length - 1) {
        tokens.push({ type: 'text', value: '\n' })
      }
      continue
    }

    let j = 0

    while (j < line.length) {
      const char = line[j]
      if (!char) break // Safety guard

      // Skip whitespace
      if (/\s/.test(char)) {
        tokens.push({ type: 'text', value: char })
        j++
        continue
      }

      // Comment: //
      if (line.substring(j, j + 2) === '//') {
        tokens.push({ type: 'comment', value: line.substring(j) })
        break
      }

      // String: "..." or '...'
      if (char === '"' || char === "'") {
        const quote = char
        let endIndex = j + 1
        while (endIndex < line.length && line[endIndex] !== quote) {
          endIndex++
        }
        tokens.push({ type: 'string', value: line.substring(j, endIndex + 1) })
        j = endIndex + 1
        continue
      }

      // Number: 0-9
      if (/\d/.test(char)) {
        let endIndex = j
        while (endIndex < line.length && /[\d.]/.test(line[endIndex] ?? '')) {
          endIndex++
        }
        tokens.push({ type: 'number', value: line.substring(j, endIndex) })
        j = endIndex
        continue
      }

      // Keyword or identifier
      if (/[a-zA-Z_]/.test(char)) {
        let endIndex = j
        while (endIndex < line.length && /[a-zA-Z0-9_]/.test(line[endIndex] ?? '')) {
          endIndex++
        }
        const word = line.substring(j, endIndex).toLowerCase()
        const type = keywords.has(word) ? 'keyword' : 'text'
        tokens.push({ type, value: line.substring(j, endIndex) })
        j = endIndex
        continue
      }

      // Operator: + - * / = < > ( ) [ ] { } , ;
      if (/[+\-*/=<>()[\]{},;]/.test(char)) {
        tokens.push({ type: 'operator', value: char })
        j++
        continue
      }

      // Other
      tokens.push({ type: 'text', value: char })
      j++
    }

    // Newline
    if (i < lines.length - 1) {
      tokens.push({ type: 'text', value: '\n' })
    }
  }

  return tokens
}

export function ELSyntaxHighlight({ code }: { code: string }) {
  const tokens = tokenizeEasyLanguage(code)

  const colorMap: Record<Token['type'], string> = {
    keyword: '#fbbf24', // amber
    string: '#34d399', // green
    number: '#60a5fa', // blue
    comment: '#6b7280', // gray
    operator: '#f472b6', // pink
    text: '#e5e7eb' // light gray
  }

  return (
    <>
      {tokens.map((token, i) => (
        <span key={i} style={{ color: colorMap[token.type] }}>
          {token.value}
        </span>
      ))}
    </>
  )
}
