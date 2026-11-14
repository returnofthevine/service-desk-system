import { ArrowDownRight, Inbox, Plus } from 'lucide-react'
import { Link } from 'react-router'
import { Loading, ReadError } from '../components/Feedback'
import { LocalTime, StatusBadge } from '../components/TicketFields'
import { categoryLabels } from '../labels'
import { useApiRead } from '../useApiRead'

export default function EmployeeTicketsPage() {
  // The employee endpoint supplies only tickets owned by the signed-in account.
  const { read, reload } = useApiRead('/api/employee/tickets')

  return (
    <>
      <div className="page-heading page-heading-action">
        <div>
          <h1>My tickets</h1>
          <p>Track the issues you’ve reported and read replies from the service desk.</p>
        </div>
        <Link className="button button-primary" to="/my-tickets/new"><Plus size={18} aria-hidden="true" /> Report an issue</Link>
      </div>
      {read.kind === 'loading' && <Loading>Loading your tickets…</Loading>}
      {read.kind === 'error' && <ReadError retry={reload}>{read.error.message}</ReadError>}
      {read.kind === 'ready' && <EmployeeTicketList tickets={read.data} />}
    </>
  )
}

function EmployeeTicketList({ tickets }) {
  if (tickets.length === 0) {
    return (
      <div className="empty-state panel" role="status">
        <span className="section-icon"><Inbox size={22} aria-hidden="true" /></span>
        <h2>No tickets yet</h2>
        <p>Report an issue when you need help from the service desk.</p>
      </div>
    )
  }

  return (
    <section className="panel queue-results" aria-label="Your tickets">
      <div className="results-heading">
        <h2>Reported issues</h2>
        <p role="status">{tickets.length} {tickets.length === 1 ? 'ticket' : 'tickets'}</p>
      </div>
      <div className="table-scroll" role="region" aria-label="My ticket results" tabIndex={0}>
        <table className="employee-ticket-table">
          <thead><tr><th scope="col">Ticket</th><th scope="col">Category</th><th scope="col">Status</th><th scope="col">Created</th></tr></thead>
          <tbody>
            {tickets.map((ticket) => (
              <tr key={ticket.id}>
                <td>
                  <Link className="ticket-link" to={`/my-tickets/${ticket.id}`}>
                    <span className="ticket-id">#{ticket.id}</span>
                    <span>{ticket.summary}</span>
                    <ArrowDownRight size={15} aria-hidden="true" />
                  </Link>
                </td>
                <td>{categoryLabels[ticket.category]}</td>
                <td><StatusBadge status={ticket.status} /></td>
                <td><LocalTime utc={ticket.createdAtUtc} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}
