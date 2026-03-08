/**
 * Lightweight HTTP client wrapper around fetch.
 * All API calls go through this module so headers, error handling,
 * and base-URL logic live in one place.
 */

const BASE_URL = '/api'

/**
 * Generic request helper.
 * @param {string}  path     – relative path (e.g. "/tournaments")
 * @param {object}  options  – fetch options override
 * @returns {Promise<any>}   – parsed JSON body (or null for 204)
 */
async function request(path, options = {}) {
  const url = `${BASE_URL}${path}`

  const headers = {
    'Content-Type': 'application/json',
    ...options.headers,
  }

  const response = await fetch(url, {
    ...options,
    headers,
  })

  // 204 No Content
  if (response.status === 204) return null

  const body = await response.json().catch(() => null)

  if (!response.ok) {
    const error = new Error(body?.error ?? `Request failed with status ${response.status}`)
    error.status = response.status
    error.body = body
    throw error
  }

  return body
}

export function get(path, options = {}) {
  return request(path, { ...options, method: 'GET' })
}

export function post(path, data, options = {}) {
  return request(path, { ...options, method: 'POST', body: JSON.stringify(data) })
}

export function put(path, data, options = {}) {
  return request(path, { ...options, method: 'PUT', body: JSON.stringify(data) })
}

export function del(path, options = {}) {
  return request(path, { ...options, method: 'DELETE' })
}

/**
 * Helper to build an auth header object.
 */
export function adminHeader(adminToken) {
  return { 'X-Admin-Token': adminToken }
}
