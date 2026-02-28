import { get, put, del, adminHeader } from './httpClient'

/**
 * GET /api/tournaments/{slug}/players
 * @param {string} slug
 * @returns {Promise<PlayerDto[]>}
 */
export function listPlayers(slug) {
  return get(`/tournaments/${slug}/players`)
}

/**
 * DELETE /api/tournaments/{slug}/players/{id}
 * @param {string} slug
 * @param {string} playerId
 * @param {string} adminToken
 * @returns {Promise<null>}
 */
export function removePlayer(slug, playerId, adminToken) {
  return del(`/tournaments/${slug}/players/${playerId}`, {
    headers: adminHeader(adminToken),
  })
}

/**
 * PUT /api/tournaments/{slug}/players/seed
 * @param {string} slug
 * @param {string[]} playerIds – ordered list of player IDs (first = seed 1)
 * @param {string} adminToken
 * @returns {Promise<PlayerDto[]>}
 */
export function setSeedOrder(slug, playerIds, adminToken) {
  return put(`/tournaments/${slug}/players/seed`, { playerIds }, {
    headers: adminHeader(adminToken),
  })
}
