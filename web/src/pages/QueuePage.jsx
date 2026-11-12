import { ArrowDownRight, ChevronLeft, ChevronRight, Inbox } from 'lucide-react'
import { Link, useLocation, useSearchParams } from 'react-router'
import { Loading, ReadError } from '../components/Feedback'
import { LocalTime, PriorityBadge, StatusBadge } from '../components/TicketFields'
import { statusLabels } from '../labels'
import { useApiRead } from '../useApiRead'

const statuses = ['all', ...Object.keys(statusLabels)]
const priorities = ['all', 'P1', 'P2', 'P3', 'P4']

export default function QueuePage() {
  const [filters, setFilters] = useSearchParams()
  const { search } = useLocation()
  const requestedStatus = filters.get('status')
  const requestedPriority = filters.get('priority')
  const requestedPage = Number(filters.get('page'))
  const status = statuses.includes(requestedStatus) ? requestedStatus : 'open'
  const priority = priorities.includes(requestedPriority) ? requestedPriority : 'all'
  const page = Number.isInteger(requestedPage) && requestedPage >= 1 && requestedPage <= 2_147_483_647
    ? requestedPage
    : 1

  const query = new URLSearchParams({ page: String(page), pageSize: '10' })
  if (status !== 'all') query.set('status', status)
  if (priority !== 'all') query.set('priority', priority)
  const url = `/api/v2/tickets?${query}`

  function changeFilter(name, value) {
    const next = new URLSearchParams(filters)
    next.set(name, value)
    next.set('page', '1')
    setFilters(next)
  }

  function changePage(nextPage) {
    const next = new URLSearchParams(filters)
    next.set('page', String(nextPage))
    setFilters(next)
  }

  return (
    <>
      <div className="page-heading">
        <h1>Ticket queue</h1>
        <p>Track endpoint incidents by status and priority.</p>
      </div>

      <div className="panel filters" aria-label="Ticket filters">
        <label>
          Status
          <select value={status} onChange={(event) => changeFilter('status', event.target.value)}>
            {statuses.map((item) => (
              <option key={item} value={item}>{statusLabels[item] ?? 'All statuses'}</option>
            ))}
          </select>
        </label>
        <label>
          Priority
          <select value={priority} onChange={(event) => changeFilter('priority', event.target.value)}>
            {priorities.map((item) => (
              <option key={item} value={item}>{item === 'all' ? 'All priorities' : item}</option>
            ))}
          </select>
        </label>
      </div>

      <TicketResults key={url} url={url} queueSearch={search} changePage={changePage} />
    </>
  )
}

function TicketResults({ url, queueSearch, changePage }) {
  const { read, reload } = useApiRead(url)

  if (read.kind === 'loading') return <Loading>Loading tickets…</Loading>
  if (read.kind === 'error') return <ReadError retry={reload}>Could not load tickets. {read.error.message}</ReadError>

  const { items, page, pageSize, totalCount, totalPages } = read.data
  if (totalCount === 0) {
    return (
      <div className="empty-state panel" role="status">
        <span className="section-icon"><Inbox size={22} aria-hidden="true" /></span>
        <h2>No matching tickets</h2>
        <p>Try a different status or priority.</p>
      </div>
    )
  }

  if (page > totalPages) {
    return (
      <div className="empty-state panel">
        <span className="section-icon"><Inbox size={22} aria-hidden="true" /></span>
        <h2>This page is empty</h2>
        <p role="status">{totalCount} matching {totalCount === 1 ? 'ticket is' : 'tickets are'} on earlier pages.</p>
        <button className="button button-secondary" type="button" onClick={() => changePage(totalPages)}>
          Go to page {totalPages}
        </button>
      </div>
    )
  }

  const firstItem = (page - 1) * pageSize + 1
  const lastItem = firstItem + items.length - 1

  return (
    <section className="panel queue-results" aria-label="Matching tickets">
      <div className="results-heading">
        <h2>Tickets</h2>
        <p role="status">{totalCount} matching {totalCount === 1 ? 'ticket' : 'tickets'}</p>
      </div>
      <div className="table-scroll" role="region" aria-label="Ticket queue results" tabIndex={0}>
        <table className="ticket-table">
          <thead>
            <tr>
              <th scope="col">Ticket</th>
              <th scope="col">Priority</th>
              <th scope="col">Status</th>
              <th scope="col">Workstation</th>
              <th scope="col">Technician</th>
              <th scope="col">Created</th>
            </tr>
          </thead>
          <tbody>
            {items.map((ticket) => (
              <tr key={ticket.id}>
                <td>
                  <Link className="ticket-link" to={{ pathname: `/tickets/${ticket.id}`, search: queueSearch }}>
                    <span className="ticket-id">#{ticket.id}</span>
                    <span>{ticket.summary}</span>
                    <ArrowDownRight size={15} aria-hidden="true" />
                  </Link>
                </td>
                <td><PriorityBadge priority={ticket.priority} /></td>
                <td><StatusBadge status={ticket.status} /></td>
                <td><span className="asset-tag">{ticket.assetTag}</span><br /><span className="muted">{ticket.hostname}</span></td>
                <td>{ticket.assignedTechnician ?? <span className="muted">Unassigned</span>}</td>
                <td><LocalTime utc={ticket.createdAtUtc} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="pagination">
        <p>{firstItem}–{lastItem} of {totalCount}</p>
        <div className="pagination-controls" role="group" aria-label="Pagination">
          <button className="button button-secondary" type="button" onClick={() => changePage(page - 1)} disabled={page === 1}>
            <ChevronLeft size={16} aria-hidden="true" /> Previous
          </button>
          <span>Page {page} of {totalPages}</span>
          <button className="button button-secondary" type="button" onClick={() => changePage(page + 1)} disabled={page >= totalPages}>
            Next <ChevronRight size={16} aria-hidden="true" />
          </button>
        </div>
      </div>
    </section>
  )
}
