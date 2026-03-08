import { get } from './httpClient'

/**
 * GET /api/tournaments/{slug}/standings
 * @param {string} slug
 * @returns {Promise<StandingDto[]>}
 */
export function getStandings(slug) {
  return get(`/tournaments/${slug}/standings`)
}
