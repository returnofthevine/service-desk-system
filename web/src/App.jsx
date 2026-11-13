import { useEffect, useState } from 'react'
import { ChartNoAxesCombined, List, LogOut, MonitorCheck, Upload } from 'lucide-react'
import { Link, Navigate, NavLink, Route, Routes, useLocation } from 'react-router'
import { apiRequest, sessionExpiredEvent, writeApi } from './api'
import { Loading, Notice, ReadError } from './components/Feedback'
import AssetsImportPage from './pages/AssetsImportPage'
import DashboardPage from './pages/DashboardPage'
import QueuePage from './pages/QueuePage'
import SetupPage from './pages/SetupPage'
import SignInPage from './pages/SignInPage'
import TicketPage from './pages/TicketPage'
import { useApiRead } from './useApiRead'

export default function App() {
  const { read, reload, replace } = useApiRead('/api/auth/me')
  const [signingOut, setSigningOut] = useState(false)
  const [signOutError, setSignOutError] = useState('')

  useEffect(() => {
    const expireSession = () => replace(null)
    window.addEventListener(sessionExpiredEvent, expireSession)
    return () => window.removeEventListener(sessionExpiredEvent, expireSession)
  }, [replace])

  async function signIn(body) {
    await writeApi('/api/auth/sign-in', { body })
    const user = await apiRequest('/api/auth/me')
    setSignOutError('')
    replace(user)
  }

  async function signOut() {
    setSigningOut(true)
    setSignOutError('')
    try {
      await writeApi('/api/auth/sign-out')
      replace(null)
    } catch (error) {
      setSignOutError(error.message)
    } finally {
      setSigningOut(false)
    }
  }

  if (read.kind === 'loading') return <SessionFrame><Loading>Checking your session…</Loading></SessionFrame>

  if (read.kind === 'error' && read.error.status !== 401) {
    return <SessionFrame><ReadError retry={reload}>{read.error.message}</ReadError></SessionFrame>
  }

  const user = read.kind === 'ready' ? read.data : null
  if (!user) return <SessionFrame><SignedOutRoutes onSignIn={signIn} /></SessionFrame>

  const canEdit = user.roles.includes('Technician')

  return (
    <>
      <a className="skip-link" href="#main-content">Skip to content</a>
      <AppHeader user={user} canEdit={canEdit} signingOut={signingOut} onSignOut={signOut} />
      <main id="main-content" className="app" tabIndex={-1}>
        {signOutError && <Notice error>{signOutError}</Notice>}
        <Routes>
          <Route path="/" element={<QueuePage />} />
          <Route path="/tickets/:id" element={<TicketPage canEdit={canEdit} />} />
          <Route path="/dashboard" element={<DashboardPage />} />
          <Route path="/assets/import" element={canEdit ? <AssetsImportPage /> : <Navigate to="/" replace />} />
          <Route path="/sign-in" element={<Navigate to="/" replace />} />
          <Route path="/setup" element={<Navigate to="/" replace />} />
          <Route path="*" element={<><h1>Page not found</h1><Link to="/">Go to the queue</Link></>} />
        </Routes>
      </main>
    </>
  )
}

function SignedOutRoutes({ onSignIn }) {
  const { read, reload, replace } = useApiRead('/api/setup/status')
  const [setupResult, setSetupResult] = useState(null)

  async function createTechnician(body) {
    try {
      await writeApi('/api/setup/technician', { body })
      setSetupResult({
        userName: body.userName,
        message: 'Technician account created. Sign in to continue.',
      })
    } catch (error) {
      if (error.status !== 409) throw error
      setSetupResult({ message: 'Setup is complete. Sign in with the technician account.' })
    }
    replace({ required: false })
  }

  if (read.kind === 'loading') return <Loading>Checking setup…</Loading>
  if (read.kind === 'error') return <ReadError retry={reload}>{read.error.message}</ReadError>

  if (read.data.required) {
    return (
      <Routes>
        <Route path="/setup" element={<SetupPage onCreateTechnician={createTechnician} />} />
        <Route path="*" element={<Navigate to="/setup" replace />} />
      </Routes>
    )
  }

  return (
    <Routes>
      <Route path="/sign-in" element={<SignInPage onSignIn={onSignIn} setupResult={setupResult} />} />
      <Route path="*" element={<Navigate to="/sign-in" replace />} />
    </Routes>
  )
}

function Brand() {
  return (
    <Link className="brand" to="/">
      <span className="brand-mark"><MonitorCheck size={21} aria-hidden="true" /></span>
      <span>Endpoint Service Desk</span>
    </Link>
  )
}

function SessionFrame({ children }) {
  return (
    <div className="session-frame">
      <header className="app-header">
        <div className="header-content"><Brand /></div>
      </header>
      <main className="app session-main">{children}</main>
    </div>
  )
}

function AppHeader({ user, canEdit, signingOut, onSignOut }) {
  const { pathname } = useLocation()

  return (
    <header className="app-header">
      <div className="header-content">
        <Brand />
        <nav className="app-nav" aria-label="Main navigation">
          <NavLink to="/" className={({ isActive }) => isActive || pathname.startsWith('/tickets/') ? 'active' : undefined}>
            <List size={17} aria-hidden="true" /> Queue
          </NavLink>
          <NavLink to="/dashboard"><ChartNoAxesCombined size={17} aria-hidden="true" /> Dashboard</NavLink>
          {canEdit && <NavLink to="/assets/import"><Upload size={17} aria-hidden="true" /> Import assets</NavLink>}
        </nav>
        <div className="account">
          <div className="account-identity">
            <strong>{user.userName}</strong>
            <span>{user.roles.join(', ')}</span>
          </div>
          <button className="sign-out" type="button" disabled={signingOut} onClick={onSignOut}>
            <LogOut size={17} aria-hidden="true" />
            {signingOut ? 'Signing out…' : 'Sign out'}
          </button>
        </div>
      </div>
    </header>
  )
}
