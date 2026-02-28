import { get, post, adminHeader } from './httpClient'

/**
 * POST /api/tournaments
 * @param {{ name: string, format: string, timeControlPreset: string, timeControlMinutes: number }} data
 * @returns {Promise<{ slug: string, name: string, inviteCode: string, adminToken: string }>}
 */
export function createTournament(data) {
  return post('/tournaments', data)
}

/**
 * GET /api/tournaments/{slug}
 * @param {string} slug
 * @returns {Promise<TournamentDto>}
 */
export function getTournament(slug) {
  return get(`/tournaments/${slug}`)
}

/**
 * GET /api/tournaments/by-invite-code/{code}
 * @param {string} code
 * @returns {Promise<TournamentDto>}
 */
export function getTournamentByInviteCode(code) {
  return get(`/tournaments/by-invite-code/${encodeURIComponent(code)}`)
}

/**
 * POST /api/tournaments/{slug}/join
 * @param {string} slug
 * @param {{ inviteCode: string, displayName: string }} data
 * @returns {Promise<{ playerId: string, playerToken: string }>}
 */
export function joinTournament(slug, data) {
  return post(`/tournaments/${slug}/join`, data)
}

/**
 * POST /api/tournaments/{slug}/start
 * @param {string} slug
 * @param {string} adminToken
 * @returns {Promise<TournamentDto>}
 */
export function startTournament(slug, adminToken) {
  return post(`/tournaments/${slug}/start`, null, {
    headers: adminHeader(adminToken),
  })
}
