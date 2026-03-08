import { get, post, adminHeader } from './httpClient'

/**
 * GET /api/tournaments/{slug}/rounds
 * @param {string} slug
 * @returns {Promise<RoundDto[]>}
 */
export function listRounds(slug) {
  return get(`/tournaments/${slug}/rounds`)
}

/**
 * POST /api/tournaments/{slug}/rounds/next
 * Generate the next round (admin).
 * @param {string} slug
 * @param {string} adminToken
 * @returns {Promise<RoundDto>}
 */
export function generateNextRound(slug, adminToken) {
  return post(`/tournaments/${slug}/rounds/next`, null, {
    headers: adminHeader(adminToken),
  })
}

/**
 * POST /api/tournaments/{slug}/rounds/{roundNumber}/complete
 * Complete a round and recalculate standings (admin).
 * @param {string} slug
 * @param {number} roundNumber
 * @param {string} adminToken
 * @returns {Promise<StandingDto[]>}
 */
export function completeRound(slug, roundNumber, adminToken) {
  return post(`/tournaments/${slug}/rounds/${roundNumber}/complete`, null, {
    headers: adminHeader(adminToken),
  })
}
