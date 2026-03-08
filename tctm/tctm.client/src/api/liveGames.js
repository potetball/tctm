import { get, post } from './httpClient'

/**
 * GET /api/tournaments/{slug}/matches/{matchId}/live
 * Returns the LiveGame state. Auto-creates the LiveGame if the caller is a
 * match participant and no LiveGame exists yet.
 * @param {string} slug
 * @param {string} matchId
 * @param {object} [params] – query parameters (e.g. { token })
 * @returns {Promise<LiveGameDto>}
 */
export function getLiveGame(slug, matchId, params = {}) {
  const query = new URLSearchParams(params).toString()
  const qs = query ? `?${query}` : ''
  return get(`/tournaments/${slug}/matches/${matchId}/live${qs}`)
}

/**
 * GET /api/tournaments/{slug}/live-games
 * @param {string} slug
 * @returns {Promise<LiveGameDto[]>}
 */
export function listLiveGames(slug) {
  return get(`/tournaments/${slug}/live-games`)
}

/**
 * POST /api/tournaments/{slug}/matches/{matchId}/live/abort
 * @param {string} slug
 * @param {string} matchId
 * @param {object} data
 * @returns {Promise<any>}
 */
export function abortLiveGame(slug, matchId, data) {
  return post(`/tournaments/${slug}/matches/${matchId}/live/abort`, data)
}
