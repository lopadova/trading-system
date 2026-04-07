/**
 * Status Formatter
 * Formats service status data as Markdown message
 */

import type { Language } from '../i18n'
import type { StatusData } from '../types'
import { processSignal, heartbeatSignal } from '../semaphores'

/**
 * Format status data as Markdown
 * @param data - Status data
 * @param lang - Language ('it' or 'en')
 * @returns Formatted Markdown message
 */
export function formatStatus(data: StatusData, lang: Language): string {
  const isItalian = lang === 'it'

  // Header
  const lines: string[] = []
  const title = isItalian ? '⚙️ *STATO SERVIZI*' : '⚙️ *SERVICES STATUS*'
  lines.push(title)
  lines.push('━━━━━━━━━━━━━━━━━━━━')

  if (data.services.length === 0) {
    const noServices = isItalian
      ? 'Nessun servizio registrato'
      : 'No services registered'
    lines.push(noServices)
  } else {
    data.services.forEach((service) => {
      const statusSignal = processSignal(service.status)
      const hbSignal = heartbeatSignal(service.ageMinutes)

      let statusText = ''
      if (service.status === 'running') {
        statusText = isItalian ? 'ATTIVO' : 'RUNNING'
      } else if (service.status === 'stopped') {
        statusText = isItalian ? 'OFFLINE' : 'OFFLINE'
      } else {
        statusText = isItalian ? 'SCONOSCIUTO' : 'UNKNOWN'
      }

      const ageText = formatAge(service.ageMinutes, lang)
      const lastHbLabel = isItalian ? 'ultimo heartbeat' : 'last heartbeat'

      lines.push(
        `${statusSignal} *${service.name}*  ${hbSignal}`
      )
      lines.push(`   ${statusText} (${lastHbLabel}: ${ageText})`)
      lines.push('')
    })
  }

  // Timestamp
  const timestampStr = formatTimestamp(data.timestamp, lang)
  lines.push(`🕐 ${timestampStr}`)

  return lines.join('\n')
}

/**
 * Format age in minutes
 */
function formatAge(minutes: number | null, lang: Language): string {
  if (minutes === null) {
    return lang === 'it' ? 'mai' : 'never'
  }

  if (minutes < 1) {
    return lang === 'it' ? '< 1 min' : '< 1 min'
  }

  if (minutes < 60) {
    const m = Math.floor(minutes)
    return lang === 'it' ? `${m} min fa` : `${m} min ago`
  }

  const hours = Math.floor(minutes / 60)
  const m = Math.floor(minutes % 60)
  return lang === 'it' ? `${hours}h ${m}m fa` : `${hours}h ${m}m ago`
}

/**
 * Format timestamp
 */
function formatTimestamp(iso: string, lang: Language): string {
  try {
    const date = new Date(iso)
    const day = date.getDate().toString().padStart(2, '0')
    const month = (date.getMonth() + 1).toString().padStart(2, '0')
    const year = date.getFullYear()
    const hours = date.getHours().toString().padStart(2, '0')
    const minutes = date.getMinutes().toString().padStart(2, '0')
    const seconds = date.getSeconds().toString().padStart(2, '0')

    return `${day}/${month}/${year} ${hours}:${minutes}:${seconds} ET`
  } catch {
    return iso
  }
}
