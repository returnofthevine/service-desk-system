import { useState } from 'react'
import { MessageSquare, Send } from 'lucide-react'
import { LocalTime } from './TicketFields'

export default function TicketConversation({ replies, onSend, pending, disabled = pending, staff = false }) {
  const [body, setBody] = useState('')
  const [isInternal, setIsInternal] = useState(false)

  async function submit(event) {
    event.preventDefault()
    // Keep the draft available when the request fails.
    if (await onSend(body.trim(), isInternal)) setBody('')
  }

  return (
    <section className="panel detail-panel conversation-panel">
      <div className="section-heading">
        <span className="section-icon"><MessageSquare size={18} aria-hidden="true" /></span>
        <h2>{staff ? 'Ticket conversation' : 'Replies'}</h2>
      </div>
      {replies.length === 0 ? <p className="muted">No replies yet.</p> : (
        <ol className="conversation-list">
          {replies.map((reply) => (
            <li key={reply.id} data-internal={reply.isInternal || undefined}>
              <div className="reply-meta">
                <strong>{reply.author}</strong>
                {staff && <span className="reply-visibility">{reply.isInternal ? 'Internal note' : 'Employee-visible'}</span>}
                <span className="muted"><LocalTime utc={reply.occurredAtUtc} /></span>
              </div>
              <p>{reply.body}</p>
            </li>
          ))}
        </ol>
      )}
      {onSend && (
        <form className="field-stack reply-form" onSubmit={submit}>
          {staff && (
            <label>
              Visibility
              <select value={isInternal ? 'internal' : 'public'} onChange={(event) => setIsInternal(event.target.value === 'internal')} disabled={disabled}>
                <option value="public">Reply to employee</option>
                <option value="internal">Internal note</option>
              </select>
            </label>
          )}
          <label>
            {staff ? 'Message' : 'Reply'}
            <textarea value={body} onChange={(event) => setBody(event.target.value)} rows={4} maxLength={2000} required disabled={disabled} />
          </label>
          <button className="button button-primary" type="submit" disabled={disabled || !body.trim()}>
            {pending ? 'Sending…' : isInternal ? 'Add internal note' : 'Send reply'}
            <Send size={15} aria-hidden="true" />
          </button>
        </form>
      )}
    </section>
  )
}
