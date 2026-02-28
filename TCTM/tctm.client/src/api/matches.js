import { post, put } from './httpClient'

/**
 * POST /api/tournaments/{slug}/matches/{id}/result
 * Report a match result (player or admin).
 * @param {string} slug
 * @param {string} matchId
 * @param {{ result: string, token: string }} data
 * @returns {Promise<MatchDto>}
 */
export function reportResult(slug, matchId, data) {
  return post(`/tournaments/${slug}/matches/${matchId}/result`, data)
}

/**
 * PUT /api/tournaments/{slug}/matches/{id}/result
 * Override a match result (admin only).
 * @param {string} slug
 * @param {string} matchId
 * @param {{ result: string, adminToken: string }} data
 * @returns {Promise<MatchDto>}
 */
export function overrideResult(slug, matchId, data) {
  return put(`/tournaments/${slug}/matches/${matchId}/result`, data)
}
