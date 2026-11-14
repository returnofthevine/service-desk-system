import { useState } from 'react'
import { ArrowLeft, ArrowRight } from 'lucide-react'
import { Link, useNavigate } from 'react-router'
import { writeApi } from '../api'
import { Notice } from '../components/Feedback'
import { categoryLabels } from '../labels'

export default function EmployeeTicketForm() {
  const navigate = useNavigate()
  const [pending, setPending] = useState(false)
  const [errorMessage, setErrorMessage] = useState('')

  async function submitTicket(event) {
    event.preventDefault()
    const fields = new FormData(event.currentTarget)
    setPending(true)
    setErrorMessage('')

    try {
      // The employee can describe equipment before staff link a registered asset.
      const ticket = await writeApi('/api/employee/tickets', {
        body: {
          category: fields.get('category'),
          summary: fields.get('summary').trim(),
          description: fields.get('description').trim(),
          workstationDetails: fields.get('workstationDetails').trim() || null,
        },
      })
      navigate(`/my-tickets/${ticket.id}`, {
        replace: true,
        state: { submitted: true },
      })
    } catch (error) {
      setErrorMessage(error.message)
    } finally {
      setPending(false)
    }
  }

  return (
    <>
      <Link className="back-link" to="/my-tickets"><ArrowLeft size={17} aria-hidden="true" /> Back to my tickets</Link>
      <div className="page-heading">
        <h1>Report an issue</h1>
        <p>Tell the service desk what happened and how it affects your work.</p>
      </div>
      <section className="panel detail-panel ticket-form-panel" aria-label="New ticket">
        {errorMessage && <Notice error>{errorMessage}</Notice>}
        <form className="field-stack" onSubmit={submitTicket}>
          <label>
            Category
            <select name="category" defaultValue="" required disabled={pending}>
              <option value="" disabled>Select a category</option>
              {Object.entries(categoryLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
            </select>
          </label>
          <label>
            Summary
            <input name="summary" maxLength={160} required disabled={pending} />
          </label>
          <div>
            <label htmlFor="issue-description">Description</label>
            <p className="field-hint" id="description-hint">Include when the issue began and any error message you saw.</p>
            <textarea id="issue-description" name="description" aria-describedby="description-hint" rows={6} maxLength={4000} required disabled={pending} />
          </div>
          <div>
            <label htmlFor="workstation-details">Workstation details (optional)</label>
            <p className="field-hint" id="workstation-hint">Enter the asset tag, computer name, or a short description if you know it.</p>
            <input id="workstation-details" name="workstationDetails" aria-describedby="workstation-hint" maxLength={160} disabled={pending} />
          </div>
          <button className="button button-primary" type="submit" disabled={pending}>
            {pending ? 'Submitting…' : 'Submit ticket'} <ArrowRight size={17} aria-hidden="true" />
          </button>
        </form>
      </section>
    </>
  )
}
