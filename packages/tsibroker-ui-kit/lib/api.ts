const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? ''

export class ApiError extends Error {
  constructor(
    public status: number,
    public body?: unknown,
  ) {
    super(`API request failed with status ${status}`)
  }
}

export async function apiFetch(path: string, init: RequestInit = {}): Promise<Response> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      ...init.headers,
    },
  })

  if (!response.ok) {
    let body: unknown
    try {
      body = await response.clone().json()
    } catch {
      // Response has no JSON body — leave `body` undefined.
    }
    throw new ApiError(response.status, body)
  }

  return response
}
