import { useState } from 'react'
import { UserPlus } from 'lucide-react'
import { writeApi } from '../api'
import { Loading, Notice, ReadError } from '../components/Feedback'
import { useApiRead } from '../useApiRead'

export default function AccountsPage() {
  const [role, setRole] = useState('employee')

  return (
    <>
      <div className="page-heading">
        <h1>Accounts</h1>
        <p>Create accounts for employees and technicians.</p>
      </div>
      <div className="panel filters">
        <label>
          Account type
          <select value={role} onChange={(event) => setRole(event.target.value)}>
            <option value="employee">Employee</option>
            <option value="technician">Technician</option>
          </select>
        </label>
      </div>
      {/* Changing role discards form values and reloads that role's account list. */}
      <AccountRole key={role} role={role} />
    </>
  )
}

function AccountRole({ role }) {
  const path = `/api/accounts/${role}s`
  const roleLabel = role === 'employee' ? 'Employee' : 'Technician'
  const { read, reload } = useApiRead(path)
  const [pending, setPending] = useState(false)
  const [notice, setNotice] = useState(null)

  async function createAccount(event) {
    event.preventDefault()
    const form = event.currentTarget
    const fields = new FormData(form)
    setPending(true)
    setNotice(null)

    try {
      const account = await writeApi(path, {
        body: { userName: fields.get('userName').trim(), password: fields.get('password') },
      })
      form.reset()
      setNotice({ message: `${roleLabel} account created for ${account.userName}.` })
      reload()
    } catch (error) {
      setNotice({ error: true, message: error.message })
    } finally {
      setPending(false)
    }
  }

  return (
    <div className="portal-grid">
      <section className="panel detail-panel">
        <div className="section-heading">
          <span className="section-icon"><UserPlus size={19} aria-hidden="true" /></span>
          <h2>Create {role}</h2>
        </div>
        {notice && <Notice error={notice.error}>{notice.message}</Notice>}
        <form className="field-stack" onSubmit={createAccount}>
          <label>
            Username
            <input name="userName" autoComplete="off" autoCapitalize="none" spellCheck={false} required disabled={pending} />
          </label>
          <div>
            <label htmlFor="new-account-password">Password</label>
            <input
              id="new-account-password"
              name="password"
              type="password"
              autoComplete="new-password"
              aria-describedby="new-account-password-hint"
              minLength={6}
              required
              disabled={pending}
            />
            <p className="field-hint" id="new-account-password-hint">
              Use at least 6 characters, including uppercase and lowercase letters, a number, and a symbol.
            </p>
          </div>
          <button className="button button-primary" type="submit" disabled={pending}>
            {pending ? 'Creating account…' : `Create ${role}`}
          </button>
        </form>
      </section>
      <section className="panel detail-panel" aria-label={`${roleLabel} accounts`}>
        <div className="section-heading"><h2>{roleLabel} accounts</h2></div>
        {read.kind === 'loading' && <Loading>Loading accounts…</Loading>}
        {read.kind === 'error' && <ReadError retry={reload}>{read.error.message}</ReadError>}
        {read.kind === 'ready' && (read.data.length === 0
          ? <p className="muted">No {role} accounts yet.</p>
          : <ul className="account-list">{read.data.map(({ id, userName }) => <li key={id}>{userName}</li>)}</ul>)}
      </section>
    </div>
  )
}
