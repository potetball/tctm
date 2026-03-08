/**
 * Central barrel export for the TCTM API client.
 *
 * Usage:
 *   import { tournaments, players, rounds, matches, standings } from '@/api'
 *   const t = await tournaments.createTournament({ ... })
 */

export * as tournaments from './tournaments'
export * as players from './players'
export * as rounds from './rounds'
export * as matches from './matches'
export * as standings from './standings'
export * as configuration from './configuration'
export * as liveGames from './liveGames'
export * from './enums'
