import { formatDate, statusLabels } from '../labels'

// Both portals use the same status labels and preserve UTC values in machine-readable timestamps.
export function StatusBadge({ status }) {
  return <span className="status" data-status={status}>{statusLabels[status]}</span>
}

export function PriorityBadge({ priority }) {
  return <span className="priority" data-priority={priority}>{priority}</span>
}

export function LocalTime({ utc }) {
  return <time dateTime={utc} title={utc}>{formatDate(utc)}</time>
}
