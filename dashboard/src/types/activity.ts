// Activity feed types for the RecentActivity widget

export type ActivityIcon =
  | 'check-circle-2'
  | 'alert-triangle'
  | 'play'
  | 'x-circle'
  | 'repeat'
  | 'trending-up'
  | 'refresh-cw'
  | 'file-text'

export type ActivityTone = 'green' | 'red' | 'yellow' | 'blue' | 'purple' | 'muted'

export interface ActivityEvent {
  id: string
  icon: ActivityIcon
  tone: ActivityTone
  title: string
  subtitle: string
  timestamp: string
}

export interface ActivityResponse {
  events: ActivityEvent[]
}
