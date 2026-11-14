import { useState } from 'react'
import { ArrowRight, KeyRound } from 'lucide-react'
import { Notice } from '../components/Feedback'

export default function SetupPage({ onCreateTechnician }) {
  const [pending, setPending] = useState(false)
  const [errorMessage, setErrorMessage] = useState('')

  async function submit(event) {
    event.preventDefault()
    // The terminal supplies the one-time code. It stays in the request body.
    const values = new FormData(event.currentTarget)
    setPending(true)
    setErrorMessage('')
    try {
      await onCreateTechnician({
        userName: values.get('userName').trim(),
        password: values.get('password'),
        setupCode: values.get('setupCode').trim(),
      })
    } catch (error) {
      setErrorMessage(error.message)
    } finally {
      setPending(false)
    }
  }

  return (
    <section className="panel sign-in" aria-labelledby="setup-heading">
      <span className="section-icon"><KeyRound size={21} aria-hidden="true" /></span>
      <h1 id="setup-heading">Create your technician account</h1>
      <p className="muted sign-in-description">Use the one-time setup code shown in the terminal window that started Service Desk System.</p>
      {errorMessage && <Notice error>{errorMessage}</Notice>}
      <form className="field-stack" onSubmit={submit}>
        <label>
          Setup code
          <input name="setupCode" autoComplete="off" autoCapitalize="none" spellCheck={false} required disabled={pending} />
        </label>
        <label>
          Username
          <input name="userName" autoComplete="username" autoCapitalize="none" spellCheck={false} maxLength={100} required disabled={pending} />
        </label>
        <div>
          <label htmlFor="setup-password">Password</label>
          <input
            id="setup-password"
            name="password"
            type="password"
            autoComplete="new-password"
            aria-describedby="setup-password-hint"
            minLength={6}
            maxLength={128}
            required
            disabled={pending}
          />
          <p className="field-hint" id="setup-password-hint">
            Use at least 6 characters, including uppercase and lowercase letters, a number, and a symbol.
          </p>
        </div>
        <button className="button button-primary" type="submit" disabled={pending}>
          {pending ? 'Creating account…' : 'Create account'} <ArrowRight size={17} aria-hidden="true" />
        </button>
      </form>
    </section>
  )
}
