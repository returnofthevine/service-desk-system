export const statusLabels = {
  open: 'Open',
  'in-progress': 'In Progress',
  resolved: 'Resolved',
}

export const categoryLabels = {
  endpoint: 'Endpoint',
  'account-access': 'Account access',
  software: 'Software',
  network: 'Network',
  other: 'Other',
}

export const priorities = ['P1', 'P2', 'P3', 'P4']

export const bitLockerLabels = {
  protected: 'Protected',
  suspended: 'Suspended',
  off: 'Off',
  unknown: 'Unknown',
}

export const tpmLabels = {
  ready: 'Ready',
  'not-ready': 'Not ready',
  missing: 'Missing',
  unknown: 'Unknown',
}

const dateFormatter = new Intl.DateTimeFormat(undefined, {
  dateStyle: 'medium',
  timeStyle: 'short',
})

export function formatDate(utc) {
  return dateFormatter.format(new Date(utc))
}
