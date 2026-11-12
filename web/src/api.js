export const sessionExpiredEvent = 'service-desk-session-expired'

export class ApiError extends Error {
  constructor(status, message, body) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.body = body
  }
}

export async function apiRequest(path, options = {}) {
  const headers = new Headers(options.headers)
  headers.set('Accept', 'application/json')

  let response
  try {
    response = await fetch(path, { ...options, headers, credentials: 'same-origin' })
  } catch (error) {
    if (options.signal?.aborted) throw error
    throw new Error('Could not reach the service desk API.')
  }

  const contentType = response.headers.get('content-type') ?? ''
  const body = contentType.includes('json') ? await response.json().catch(() => null) : null

  if (!response.ok) {
    if (response.status === 401 && !['/api/auth/sign-in', '/api/auth/me'].includes(path)) {
      window.dispatchEvent(new Event(sessionExpiredEvent))
    }

    const fieldError = Object.values(body?.errors ?? {}).flat().find((item) => typeof item === 'string')
    const message = body?.detail ?? fieldError ?? body?.title ?? `Request failed (HTTP ${response.status}).`
    throw new ApiError(response.status, message, body)
  }

  if (body === null) throw new Error('The API returned an unexpected response.')
  return body
}

export async function writeApi(path, { method = 'POST', body } = {}) {
  const { requestToken } = await apiRequest('/api/auth/csrf', { cache: 'no-store' })
  const headers = { 'X-CSRF-TOKEN': requestToken }

  if (body === undefined || body instanceof FormData) {
    return apiRequest(path, { method, headers, body })
  }

  return apiRequest(path, {
    method,
    headers: { ...headers, 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
}
