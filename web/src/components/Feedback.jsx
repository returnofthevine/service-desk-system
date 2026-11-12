import { CircleAlert, CircleCheck, LoaderCircle } from 'lucide-react'

export function Loading({ children }) {
  return (
    <div className="feedback panel" role="status">
      <LoaderCircle className="spinner" size={20} aria-hidden="true" />
      <p>{children}</p>
    </div>
  )
}

export function Notice({ children, error = false }) {
  const Icon = error ? CircleAlert : CircleCheck
  return (
    <div className={error ? 'notice notice-error' : 'notice'} role={error ? 'alert' : 'status'}>
      <Icon size={18} aria-hidden="true" />
      <p>{children}</p>
    </div>
  )
}

export function ReadError({ children, retry }) {
  return (
    <div className="feedback panel">
      <CircleAlert size={21} aria-hidden="true" />
      <p role="alert">{children}</p>
      {retry && <button className="button button-secondary" type="button" onClick={retry}>Retry</button>}
    </div>
  )
}
