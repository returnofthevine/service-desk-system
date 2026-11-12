import { useCallback, useEffect, useState } from 'react'
import { apiRequest } from './api'

export function useApiRead(path) {
  const [read, setRead] = useState({ kind: 'loading' })
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      try {
        const data = await apiRequest(path, { signal: controller.signal })
        if (!controller.signal.aborted) setRead({ kind: 'ready', data })
      } catch (error) {
        if (!controller.signal.aborted) setRead({ kind: 'error', error })
      }
    }

    load()
    return () => controller.abort()
  }, [path, attempt])

  const reload = useCallback(() => {
    setRead({ kind: 'loading' })
    setAttempt((current) => current + 1)
  }, [])

  const replace = useCallback((data) => {
    setRead({ kind: 'ready', data })
  }, [])

  return { read, reload, replace }
}
