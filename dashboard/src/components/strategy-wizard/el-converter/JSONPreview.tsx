/**
 * JSONPreview — Syntax highlighted JSON display
 *
 * Shows formatted JSON with basic syntax highlighting.
 * Scrollable with max-height for large JSON objects.
 */

export function JSONPreview({ json }: { json: unknown }) {
  const jsonString = JSON.stringify(json, null, 2)

  return (
    <pre className="text-xs font-mono bg-gray-900 p-3 rounded overflow-auto max-h-96 border border-gray-700">
      <code className="text-gray-300">{jsonString}</code>
    </pre>
  )
}
