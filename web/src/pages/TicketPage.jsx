import { useState } from 'react'
import { ArrowLeft, ArrowRight, Check, History, Monitor, SlidersHorizontal } from 'lucide-react'
import { Link, useLocation, useParams } from 'react-router'
import { writeApi } from '../api'
import { Loading, Notice, ReadError } from '../components/Feedback'
import TicketConversation from '../components/TicketConversation'
import { LocalTime, PriorityBadge, StatusBadge } from '../components/TicketFields'
import { bitLockerLabels, categoryLabels, priorities, statusLabels, tpmLabels } from '../labels'
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
  const [saving, setSaving] = useState(null)
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

  // Use the server's returned ticket as the source for status, triage, and conversation.
  async function saveTicket(action, endpoint, options, message) {
    setSaving(action)
    setNotice(null)
    try {
      const updated = await writeApi(`${path}/${endpoint}`, options)
      replace(updated)
      setNotice({ error: false, text: typeof message === 'function' ? message(updated) : message })
      return true
    } catch (error) {
      if (error.status === 409) {
        // Refresh the version and status after a concurrent change.
        setNotice({ error: true, text: 'This ticket changed. Review the latest details before trying again.' })
        reload()
      } else {
        setNotice({ error: true, text: error.message })
      }
      return false
    } finally {
      setSaving(null)
    }
  }

  async function changeStatus(status) {
    const saved = await saveTicket(
      'status',
      'status',
      {
        method: 'PATCH',
        body: {
          expectedStatus: ticket.status,
          expectedVersion: ticket.version,
          status,
          note: status === 'resolved' ? note : null,
        },
      },
      (updated) => `Saved as ${statusLabels[updated.status]}.`,
    )
    if (saved) setNote('')
  }

  function updateTriage(fields) {
    return saveTicket('triage', 'triage', {
      method: 'PATCH',
      body: { ...fields, expectedVersion: ticket.version },
    }, 'Ticket updated.')
  }

  function sendReply(body, isInternal) {
    return saveTicket('reply', 'replies', { body: { body, isInternal } }, isInternal ? 'Internal note added.' : 'Reply sent.')
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
          {ticket.description && <p className="ticket-description">{ticket.description}</p>}
        </div>
        <dl className="ticket-meta">
          <div><dt>Category</dt><dd>{categoryLabels[ticket.category]}</dd></div>
          <div><dt>Requester</dt><dd>{ticket.requesterName ?? '—'}</dd></div>
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
          {ticket.workstationDetails && <div><dt>Reported workstation</dt><dd>{ticket.workstationDetails}</dd></div>}
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
                <p className="muted">
                  {ticket.assignedTechnician ? 'Starting work keeps the current assignment and records the status change.' : 'Starting work assigns this ticket to you and records the status change.'}
                </p>
                <button className="button button-primary" type="button" disabled={Boolean(saving)} onClick={() => changeStatus('in-progress')}>
                  {saving === 'status' ? 'Saving…' : 'Start work'} <ArrowRight size={17} aria-hidden="true" />
                </button>
              </>
            )}
            {ticket.status === 'in-progress' && (
              <form onSubmit={(event) => {
                event.preventDefault()
                changeStatus('resolved')
              }}>
                <label htmlFor="resolution-note">Resolution note</label>
              <p id="resolution-help" className="field-hint">Employees can see the resolution. Describe the outcome in up to 500 characters.</p>
                <textarea
                  id="resolution-note"
                  aria-describedby="resolution-help"
                  value={note}
                  onChange={(event) => setNote(event.target.value)}
                  rows={4}
                  maxLength={500}
                  disabled={Boolean(saving)}
                />
                <button className="button button-primary" type="submit" disabled={Boolean(saving)}>
                  {saving === 'status' ? 'Saving…' : 'Resolve ticket'} <Check size={17} aria-hidden="true" />
                </button>
              </form>
            )}
            {ticket.status === 'resolved' && <p className="muted">The resolution note is saved in the ticket history.</p>}
          </section>
        )}
      </div>

      {canEdit && (
        <TicketTriage
          key={ticket.version}
          ticket={ticket}
          onUpdate={updateTriage}
          disabled={Boolean(saving)}
          saving={saving === 'triage'}
        />
      )}
      <TicketConversation
        replies={ticket.replies}
        onSend={canEdit ? sendReply : undefined}
        pending={saving === 'reply'}
        disabled={Boolean(saving)}
        staff
      />
      <TicketHistory history={ticket.history} />
      <TriageHistory history={ticket.triageHistory} />
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
      {asset ? <dl className="workstation-meta">
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
      </dl> : <p className="muted">No registered workstation is linked.</p>}
    </section>
  )
}

function TicketTriage({ ticket, onUpdate, disabled, saving }) {
  const { read, reload } = useApiRead('/api/tickets/options')

  function submit(event) {
    event.preventDefault()
    const fields = new FormData(event.currentTarget)
    const assetId = fields.get('assetId')
    onUpdate({
      priority: fields.get('priority'),
      assignedTechnicianId: fields.get('assignedTechnicianId') || null,
      assetId: assetId ? Number(assetId) : null,
    })
  }

  // Seeded assignments may have a display name from before account IDs were stored.
  const assignee = ticket.assignedTechnicianId ?? (ticket.assignedTechnician ? 'legacy' : '')

  return (
    <section className="panel detail-panel triage-panel">
      <div className="section-heading">
        <span className="section-icon"><SlidersHorizontal size={18} aria-hidden="true" /></span>
        <h2>Ticket triage</h2>
      </div>
      {read.kind === 'loading' && <Loading>Loading triage options…</Loading>}
      {read.kind === 'error' && <ReadError retry={reload}>{read.error.message}</ReadError>}
      {read.kind === 'ready' && (
        <form className="triage-form" onSubmit={submit}>
          <label>
            Priority
            <select name="priority" defaultValue={ticket.priority} disabled={disabled}>
              {priorities.map((priority) => <option key={priority}>{priority}</option>)}
            </select>
          </label>
          <label>
            Assigned technician
            <select name="assignedTechnicianId" defaultValue={assignee} disabled={disabled}>
              <option value="">Unassigned</option>
              {assignee === 'legacy' && <option value="legacy">{ticket.assignedTechnician} (existing assignment)</option>}
              {assignee && assignee !== 'legacy' && !read.data.technicians.some(({ id }) => id === assignee) &&
                <option value={assignee}>{ticket.assignedTechnician} (current assignment)</option>}
              {read.data.technicians.map(({ id, userName }) => <option key={id} value={id}>{userName}</option>)}
            </select>
          </label>
          <label>
            Registered workstation
            <select name="assetId" defaultValue={ticket.asset?.id ?? ''} disabled={disabled}>
              <option value="">Unlinked</option>
              {read.data.assets.map(({ id, assetTag, hostname }) => <option key={id} value={id}>{assetTag} · {hostname}</option>)}
            </select>
          </label>
          <button className="button button-primary" type="submit" disabled={disabled}>{saving ? 'Saving…' : 'Update ticket'}</button>
        </form>
      )}
    </section>
  )
}

const triageFields = { priority: 'Priority', assignedTechnician: 'Assigned technician', asset: 'Workstation' }

function TriageHistory({ history }) {
  if (history.length === 0) return null

  return (
    <section className="panel detail-panel history-panel">
      <div className="section-heading">
        <span className="section-icon"><History size={18} aria-hidden="true" /></span>
        <h2>Triage history</h2>
      </div>
      <ol className="timeline">
        {history.map((entry) => (
          <li key={entry.id}>
            <strong>{triageFields[entry.field] ?? entry.field}</strong>
            <p>{entry.previousValue ?? '—'} → {entry.newValue ?? '—'}</p>
            <p className="muted">{entry.technician} · <LocalTime utc={entry.occurredAtUtc} /></p>
          </li>
        ))}
      </ol>
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
