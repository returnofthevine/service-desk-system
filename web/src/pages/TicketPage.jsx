import { useState } from 'react'
import { ArrowLeft, ArrowRight, Check, History, Monitor } from 'lucide-react'
import { Link, useLocation, useParams } from 'react-router'
import { writeApi } from '../api'
import { Loading, Notice, ReadError } from '../components/Feedback'
import { LocalTime, PriorityBadge, StatusBadge } from '../components/TicketFields'
import { bitLockerLabels, statusLabels, tpmLabels } from '../labels'
import { useApiRead } from '../useApiRead'

export default function TicketPage({ canEdit }) {
  const { id } = useParams()
  const { search } = useLocation()

  return (
    <>
      <Link className="back-link" to={{ pathname: '/', search }}>
        <ArrowLeft size={17} aria-hidden="true" /> Back to queue
      </Link>
      <TicketDetails key={id} id={id} canEdit={canEdit} />
    </>
  )
}

function TicketDetails({ id, canEdit }) {
  const path = `/api/tickets/${encodeURIComponent(id)}`
  const { read, reload, replace } = useApiRead(path)
  const [note, setNote] = useState('')
  const [saving, setSaving] = useState(false)
  const [notice, setNotice] = useState(null)
  const feedback = notice && <Notice error={notice.error}>{notice.text}</Notice>

  if (read.kind === 'loading') return <>{feedback}<Loading>Loading ticket…</Loading></>

  if (read.kind === 'error') {
    const missing = read.error.status === 404
    return (
      <>
        {feedback}
        <ReadError retry={missing ? undefined : reload}>
          {missing ? 'Ticket not found.' : `Could not load the ticket. ${read.error.message}`}
        </ReadError>
      </>
    )
  }

  const ticket = read.data

  async function changeStatus(status) {
    setSaving(true)
    setNotice(null)
    try {
      const updated = await writeApi(`${path}/status`, {
        method: 'PATCH',
        body: {
          expectedStatus: ticket.status,
          status,
          note: status === 'resolved' ? note : null,
        },
      })
      replace(updated)
      setNote('')
      setNotice({ error: false, text: `Saved as ${statusLabels[updated.status]}.` })
    } catch (error) {
      if (error.status === 409) {
        setNotice({ error: true, text: 'This ticket changed. Review its latest status before trying again.' })
        reload()
      } else {
        setNotice({ error: true, text: error.message })
      }
    } finally {
      setSaving(false)
    }
  }

  return (
    <>
      {feedback}
      <section className="panel ticket-overview">
        <div className="ticket-heading">
          <div className="ticket-heading-labels">
            <span className="ticket-number">Ticket #{ticket.id}</span>
            <PriorityBadge priority={ticket.priority} />
            <StatusBadge status={ticket.status} />
          </div>
          <h1>{ticket.summary}</h1>
        </div>
        <dl className="ticket-meta">
          <div>
            <dt>Technician</dt>
            <dd>{ticket.assignedTechnician ?? 'Unassigned'}</dd>
          </div>
          <div>
            <dt>Created</dt>
            <dd><LocalTime utc={ticket.createdAtUtc} /></dd>
          </div>
          <div>
            <dt>Resolved</dt>
            <dd>{ticket.resolvedAtUtc ? <LocalTime utc={ticket.resolvedAtUtc} /> : 'Pending'}</dd>
          </div>
        </dl>
      </section>

      <div className={canEdit ? 'detail-grid' : 'detail-grid detail-grid-single'}>
        <Workstation asset={ticket.asset} />
        {canEdit && (
          <section className="panel detail-panel">
            <div className="section-heading">
              <span className="section-icon"><ArrowRight size={18} aria-hidden="true" /></span>
              <h2>Next action</h2>
            </div>
            {ticket.status === 'open' && (
              <>
                <p className="muted">Starting work assigns this ticket to you and adds an entry to its history.</p>
                <button className="button button-primary" type="button" disabled={saving} onClick={() => changeStatus('in-progress')}>
                  {saving ? 'Saving…' : 'Start work'} <ArrowRight size={17} aria-hidden="true" />
                </button>
              </>
            )}
            {ticket.status === 'in-progress' && (
              <form onSubmit={(event) => {
                event.preventDefault()
                changeStatus('resolved')
              }}>
                <label htmlFor="resolution-note">Resolution note</label>
                <p id="resolution-help" className="field-hint">Describe the outcome in up to 500 characters.</p>
                <textarea
                  id="resolution-note"
                  aria-describedby="resolution-help"
                  value={note}
                  onChange={(event) => setNote(event.target.value)}
                  rows={4}
                  maxLength={500}
                  disabled={saving}
                />
                <button className="button button-primary" type="submit" disabled={saving}>
                  {saving ? 'Saving…' : 'Resolve ticket'} <Check size={17} aria-hidden="true" />
                </button>
              </form>
            )}
            {ticket.status === 'resolved' && <p className="muted">The resolution note is saved in the ticket history.</p>}
          </section>
        )}
      </div>

      <TicketHistory history={ticket.history} />
    </>
  )
}

function Workstation({ asset }) {
  return (
    <section className="panel detail-panel">
      <div className="section-heading">
        <span className="section-icon"><Monitor size={18} aria-hidden="true" /></span>
        <h2>Workstation</h2>
      </div>
      <dl className="workstation-meta">
        <dt>Asset tag</dt>
        <dd className="asset-tag">{asset.assetTag}</dd>
        <dt>Hostname</dt>
        <dd>{asset.hostname}</dd>
        <dt>BitLocker</dt>
        <dd><span className="asset-state" data-state={asset.bitLockerState}>{bitLockerLabels[asset.bitLockerState]}</span></dd>
        <dt>TPM</dt>
        <dd><span className="asset-state" data-state={asset.tpmState}>{tpmLabels[asset.tpmState]}</span></dd>
        <dt>Last reported check</dt>
        <dd><LocalTime utc={asset.lastCheckedAtUtc} /></dd>
      </dl>
    </section>
  )
}

function TicketHistory({ history }) {
  return (
    <section className="panel detail-panel history-panel">
      <div className="section-heading">
        <span className="section-icon"><History size={18} aria-hidden="true" /></span>
        <h2>Ticket history</h2>
      </div>
      {history.length === 0 ? <p className="muted">No status changes yet.</p> : (
        <ol className="timeline">
          {history.map((entry) => (
            <li key={entry.id}>
              <strong className="transition">
                {statusLabels[entry.previousStatus]} <ArrowRight size={14} aria-label="to" /> {statusLabels[entry.newStatus]}
              </strong>
              <p className="muted">{entry.technician} · <LocalTime utc={entry.occurredAtUtc} /></p>
              {entry.note && <p className="timeline-note">{entry.note}</p>}
            </li>
          ))}
        </ol>
      )}
    </section>
  )
}
