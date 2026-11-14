import { useState } from 'react'
import { ArrowRight, LockKeyhole } from 'lucide-react'
import { Notice } from '../components/Feedback'

export default function SignInPage({ onSignIn, setupResult }) {
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')

  async function submit(event) {
    event.preventDefault()
    // Read the password from the form for this request only.
    const values = new FormData(event.currentTarget)
    setPending(true)
    setError('')
    try {
      await onSignIn({ userName: values.get('userName').trim(), password: values.get('password') })
    } catch (error) {
      setError(error.message)
    } finally {
      setPending(false)
    }
  }

  return (
    <section className="panel sign-in" aria-labelledby="sign-in-heading">
      <span className="section-icon"><LockKeyhole size={21} aria-hidden="true" /></span>
      <h1 id="sign-in-heading">Sign in</h1>
      <p className="muted sign-in-description">Use your service desk account to open your portal.</p>
      {setupResult && <Notice>{setupResult.message}</Notice>}
      {error && <Notice error>{error}</Notice>}
      <form className="field-stack" onSubmit={submit}>
        <label>
          Username
          <input
            name="userName"
            autoComplete="username"
            autoCapitalize="none"
            spellCheck={false}
            defaultValue={setupResult?.userName}
            required
            disabled={pending}
          />
        </label>
        <label>
          Password
          <input name="password" type="password" autoComplete="current-password" required disabled={pending} />
        </label>
        <button className="button button-primary" type="submit" disabled={pending}>
          {pending ? 'Signing in…' : 'Sign in'} <ArrowRight size={17} aria-hidden="true" />
        </button>
      </form>
    </section>
  )
}
