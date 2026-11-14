import { useState } from 'react'
import { ArrowLeft, History } from 'lucide-react'
import { Link, useLocation, useParams } from 'react-router'
import { writeApi } from '../api'
import { Loading, Notice, ReadError } from '../components/Feedback'
import TicketConversation from '../components/TicketConversation'
import { LocalTime, StatusBadge } from '../components/TicketFields'
import { categoryLabels, statusLabels } from '../labels'
import { useApiRead } from '../useApiRead'

export default function EmployeeTicketPage() {
  const { id } = useParams()
  const { state } = useLocation()

  return (
    <>
      <Link className="back-link" to="/my-tickets"><ArrowLeft size={17} aria-hidden="true" /> Back to my tickets</Link>
      <EmployeeTicket key={id} id={id} submitted={state?.submitted} />
    </>
  )
}

function EmployeeTicket({ id, submitted }) {
  // Read the employee projection, which includes public replies and resolution notes.
  const path = `/api/employee/tickets/${encodeURIComponent(id)}`
  const { read, reload, replace } = useApiRead(path)
  const [pending, setPending] = useState(false)
  const [notice, setNotice] = useState(submitted ? { message: 'Your ticket has been submitted.' } : null)

  if (read.kind === 'loading') return <Loading>Loading ticket…</Loading>
  if (read.kind === 'error') {
    const missing = read.error.status === 404
    return <ReadError retry={missing ? undefined : reload}>{missing ? 'Ticket not found.' : read.error.message}</ReadError>
  }

  const ticket = read.data

  async function sendReply(body) {
    setPending(true)
    setNotice(null)
    try {
      replace(await writeApi(`${path}/replies`, { body: { body } }))
      setNotice({ message: 'Reply sent.' })
      return true
    } catch (error) {
      setNotice({ error: true, message: error.message })
      return false
    } finally {
      setPending(false)
    }
  }

  return (
    <>
      {notice && <Notice error={notice.error}>{notice.message}</Notice>}
      <section className="panel ticket-overview">
        <div className="ticket-heading">
          <div className="ticket-heading-labels">
            <span className="ticket-number">Ticket #{ticket.id}</span>
            <StatusBadge status={ticket.status} />
          </div>
          <h1>{ticket.summary}</h1>
          <p className="ticket-description">{ticket.description}</p>
        </div>
        <dl className="ticket-meta">
          <div><dt>Category</dt><dd>{categoryLabels[ticket.category]}</dd></div>
          <div><dt>Created</dt><dd><LocalTime utc={ticket.createdAtUtc} /></dd></div>
          <div><dt>Resolved</dt><dd>{ticket.resolvedAtUtc ? <LocalTime utc={ticket.resolvedAtUtc} /> : 'Pending'}</dd></div>
          {ticket.workstationDetails && <div><dt>Reported workstation</dt><dd>{ticket.workstationDetails}</dd></div>}
        </dl>
      </section>
      <TicketConversation replies={ticket.replies} onSend={sendReply} pending={pending} />
      <section className="panel detail-panel history-panel">
        <div className="section-heading">
          <span className="section-icon"><History size={18} aria-hidden="true" /></span>
          <h2>Status history</h2>
        </div>
        {ticket.statusHistory.length === 0 ? <p className="muted">No status changes yet.</p> : (
          <ol className="timeline">
            {ticket.statusHistory.map((event) => (
              <li key={event.id}>
                <strong>{statusLabels[event.newStatus]}</strong>
                <p className="muted"><LocalTime utc={event.occurredAtUtc} /></p>
                {event.resolutionNote && <p className="timeline-note">{event.resolutionNote}</p>}
              </li>
            ))}
          </ol>
        )}
      </section>
    </>
  )
}
