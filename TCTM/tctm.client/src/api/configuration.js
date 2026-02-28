import { get } from './httpClient'

/**
 * GET /api/configuration
 * @returns {Promise<{ applicationUrl: string }>}
 */
export function getConfiguration() {
  return get('/configuration')
}
