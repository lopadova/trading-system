/**
 * IssuesList — Display conversion issues with icons and suggestions
 *
 * Shows categorized issues from EL conversion:
 * - not_supported: Red badge, features not available in SDF
 * - ambiguous: Yellow badge, unclear intent from EL code
 * - manual_required: Orange badge, requires human intervention
 */

import type { ConversionIssue } from '../../../hooks/useELConversion'
import { Badge, type BadgeTone } from '../../ui/Badge'

interface IssuesListProps {
  issues: ConversionIssue[]
}

export function IssuesList({ issues }: IssuesListProps) {
  const getIssueIcon = (type: ConversionIssue['type']) => {
    switch (type) {
      case 'not_supported': return '🔴'
      case 'ambiguous': return '🟡'
      case 'manual_required': return '🟠'
    }
  }

  const getIssueLabel = (type: ConversionIssue['type']) => {
    switch (type) {
      case 'not_supported': return 'Non Supportato'
      case 'ambiguous': return 'Ambiguo'
      case 'manual_required': return 'Richiesta Modifica Manuale'
    }
  }

  const getIssueBadgeTone = (type: ConversionIssue['type']): BadgeTone => {
    return type === 'not_supported' ? 'red' : 'yellow'
  }

  return (
    <div className="space-y-3">
      {issues.map((issue, i) => (
        <div key={i} className="border border-gray-700 rounded-lg p-3 bg-gray-800">
          <div className="flex items-start gap-2 mb-2">
            <span className="text-xl">{getIssueIcon(issue.type)}</span>
            <div className="flex-1">
              <div className="flex items-center gap-2 mb-1">
                <Badge tone={getIssueBadgeTone(issue.type)}>
                  {getIssueLabel(issue.type)}
                </Badge>
                <code className="text-xs text-amber-400 bg-amber-400/10 px-2 py-0.5 rounded">
                  {issue.el_construct}
                </code>
              </div>
              <p className="text-sm text-gray-300 mb-2">{issue.description}</p>
              {issue.suggestion && (
                <div className="text-xs text-blue-400 bg-blue-400/10 p-2 rounded">
                  💡 <strong>Suggerimento:</strong> {issue.suggestion}
                </div>
              )}
            </div>
          </div>
        </div>
      ))}
    </div>
  )
}
