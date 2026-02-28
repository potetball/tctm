/**
 * Enum mirrors for backend values.
 * Keep in sync with TCTM.Server/DataModel/Enums.cs
 */

/** @enum {string} */
export const TournamentFormat = Object.freeze({
  RoundRobin: 'RoundRobin',
  Swiss: 'Swiss',
  SingleElimination: 'SingleElimination',
  DoubleElimination: 'DoubleElimination',
})

/** @enum {string} */
export const TimeControlPreset = Object.freeze({
  Bullet: 'Bullet',
  Blitz: 'Blitz',
  Rapid: 'Rapid',
})

/** @enum {string} */
export const TournamentStatus = Object.freeze({
  Lobby: 'Lobby',
  InProgress: 'InProgress',
  Completed: 'Completed',
})

/** @enum {string} */
export const RoundStatus = Object.freeze({
  Pending: 'Pending',
  InProgress: 'InProgress',
  Completed: 'Completed',
})

/** @enum {string} */
export const MatchResult = Object.freeze({
  WhiteWin: 'WhiteWin',
  BlackWin: 'BlackWin',
  Draw: 'Draw',
})

/** @enum {string} */
export const Bracket = Object.freeze({
  Winners: 'Winners',
  Losers: 'Losers',
})
