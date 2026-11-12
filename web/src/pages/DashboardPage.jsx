import { useState } from 'react'
import { CalendarDays, CheckCircle2, Clock, Inbox, TicketPlus } from 'lucide-react'
import { useSearchParams } from 'react-router'
import { Loading, ReadError } from '../components/Feedback'
import { useApiRead } from '../useApiRead'

export default function DashboardPage() {
  const [search, setSearch] = useSearchParams()
  const from = search.get('from') ?? '2026-10-27'
  const to = search.get('to') ?? '2026-11-02'
  const query = new URLSearchParams({ from, to })

  return (
    <>
      <div className="page-heading">
        <h1>Incident dashboard</h1>
        <p>Review ticket volume, backlog, and resolution times.</p>
      </div>
      <DateRange
        key={`range-${query}`}
        from={from}
        to={to}
        onChange={(range) => setSearch(range)}
      />
      <DashboardResults key={`results-${query}`} path={`/api/dashboard?${query}`} />
    </>
  )
}

function DateRange({ from, to, onChange }) {
  const [start, setStart] = useState(from)
  const [end, setEnd] = useState(to)

  return (
    <form className="panel filters dashboard-filters" onSubmit={(event) => {
      event.preventDefault()
      onChange({ from: start, to: end })
    }}>
      <label>From (UTC)<input type="date" value={start} onChange={(event) => setStart(event.target.value)} required /></label>
      <label>To, exclusive (UTC)<input type="date" value={end} onChange={(event) => setEnd(event.target.value)} required /></label>
      <button className="button button-secondary" type="submit"><CalendarDays size={17} aria-hidden="true" /> Apply dates</button>
    </form>
  )
}

function DashboardResults({ path }) {
  const { read, reload } = useApiRead(path)
  if (read.kind === 'loading') return <Loading>Loading dashboard…</Loading>
  if (read.kind === 'error') return <ReadError retry={reload}>Could not load the dashboard. {read.error.message}</ReadError>

  const { createdCount, resolvedCount, openNowCount, averageResolutionMinutes } = read.data

  return (
    <dl className="metrics" aria-label="Incident metrics">
      <Metric icon={TicketPlus} label="Created in range" value={createdCount} />
      <Metric icon={CheckCircle2} label="Resolved in range" value={resolvedCount} />
      <Metric icon={Inbox} label="Open now" value={openNowCount} />
      <Metric
        icon={Clock}
        label="Average time to resolution"
        value={averageResolutionMinutes === null ? null : averageResolutionMinutes.toFixed(1)}
        unit="min"
      />
    </dl>
  )
}

function Metric({ icon, label, value, unit }) {
  const Icon = icon

  return (
    <div className="panel metric">
      <span className="section-icon"><Icon size={20} aria-hidden="true" /></span>
      <dt>{label}</dt>
      <dd>
        {value === null ? <span className="metric-empty">No resolutions in this range</span> : (
          <>{value}{unit && <span className="metric-unit"> {unit}</span>}</>
        )}
      </dd>
    </div>
  )
}
