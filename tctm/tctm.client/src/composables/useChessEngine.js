import { ref, computed } from 'vue'

// ─── Constants ───────────────────────────────────────────────────────────────

const STARTING_FEN = 'rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1'

const PIECE_VALUES = { P: 1, N: 3, B: 3, R: 5, Q: 9, K: 0 }

const STARTING_MATERIAL = { P: 8, N: 2, B: 2, R: 2, Q: 1, K: 1 }

// ─── Helpers ─────────────────────────────────────────────────────────────────

/**
 * Convert algebraic square string to board indices.
 * board[0][0] = a8, board[7][7] = h1 (from White's perspective).
 * Row 0 = rank 8, Row 7 = rank 1.
 * Col 0 = file a, Col 7 = file h.
 */
function squareToIndices(sq) {
  const col = sq.charCodeAt(0) - 97 // 'a' = 0
  const row = 8 - parseInt(sq[1])   // '8' = 0, '1' = 7
  return { row, col }
}

function indicesToSquare(row, col) {
  return String.fromCharCode(97 + col) + (8 - row)
}

function inBounds(r, c) {
  return r >= 0 && r < 8 && c >= 0 && c < 8
}

function oppositeColor(color) {
  return color === 'w' ? 'b' : 'w'
}

function cloneBoard(board) {
  return board.map(row => row.map(p => p ? { ...p } : null))
}

// ─── FEN ─────────────────────────────────────────────────────────────────────

function parseFen(fen) {
  const parts = fen.split(' ')
  const rows = parts[0].split('/')
  const board = Array.from({ length: 8 }, () => Array(8).fill(null))

  for (let r = 0; r < 8; r++) {
    let c = 0
    for (const ch of rows[r]) {
      if (ch >= '1' && ch <= '8') {
        c += parseInt(ch)
      } else {
        const color = ch === ch.toUpperCase() ? 'w' : 'b'
        const type = ch.toUpperCase()
        board[r][c] = { type, color }
        c++
      }
    }
  }

  const turn = parts[1] || 'w'
  const castling = parts[2] || '-'
  const enPassantSq = parts[3] && parts[3] !== '-' ? parts[3] : null
  const halfmoveClock = parseInt(parts[4]) || 0
  const fullmoveNumber = parseInt(parts[5]) || 1

  return {
    board,
    turn,
    castling: {
      K: castling.includes('K'),
      Q: castling.includes('Q'),
      k: castling.includes('k'),
      q: castling.includes('q'),
    },
    enPassantSq,
    halfmoveClock,
    fullmoveNumber,
  }
}

function boardToFen(board, turn, castling, enPassantSq, halfmoveClock, fullmoveNumber) {
  let fen = ''
  for (let r = 0; r < 8; r++) {
    let empty = 0
    for (let c = 0; c < 8; c++) {
      const p = board[r][c]
      if (!p) {
        empty++
      } else {
        if (empty > 0) { fen += empty; empty = 0 }
        fen += p.color === 'w' ? p.type : p.type.toLowerCase()
      }
    }
    if (empty > 0) fen += empty
    if (r < 7) fen += '/'
  }

  let castleStr = ''
  if (castling.K) castleStr += 'K'
  if (castling.Q) castleStr += 'Q'
  if (castling.k) castleStr += 'k'
  if (castling.q) castleStr += 'q'
  if (!castleStr) castleStr = '-'

  return `${fen} ${turn} ${castleStr} ${enPassantSq || '-'} ${halfmoveClock} ${fullmoveNumber}`
}

// ─── Attack / Check detection ────────────────────────────────────────────────

/**
 * Does any piece of `attackerColor` attack the square (row, col)?
 */
function isSquareAttacked(board, row, col, attackerColor) {
  // Pawn attacks
  const pawnDir = attackerColor === 'w' ? 1 : -1 // white pawns move up (lower row index) but attack row-1
  const pawnRow = row + pawnDir
  if (inBounds(pawnRow, col - 1)) {
    const p = board[pawnRow][col - 1]
    if (p && p.color === attackerColor && p.type === 'P') return true
  }
  if (inBounds(pawnRow, col + 1)) {
    const p = board[pawnRow][col + 1]
    if (p && p.color === attackerColor && p.type === 'P') return true
  }

  // Knight attacks
  const knightMoves = [[-2, -1], [-2, 1], [-1, -2], [-1, 2], [1, -2], [1, 2], [2, -1], [2, 1]]
  for (const [dr, dc] of knightMoves) {
    const nr = row + dr, nc = col + dc
    if (inBounds(nr, nc)) {
      const p = board[nr][nc]
      if (p && p.color === attackerColor && p.type === 'N') return true
    }
  }

  // King attacks (for preventing king from moving into king's range)
  for (let dr = -1; dr <= 1; dr++) {
    for (let dc = -1; dc <= 1; dc++) {
      if (dr === 0 && dc === 0) continue
      const nr = row + dr, nc = col + dc
      if (inBounds(nr, nc)) {
        const p = board[nr][nc]
        if (p && p.color === attackerColor && p.type === 'K') return true
      }
    }
  }

  // Sliding pieces: rook/queen on ranks/files
  const rookDirs = [[-1, 0], [1, 0], [0, -1], [0, 1]]
  for (const [dr, dc] of rookDirs) {
    let nr = row + dr, nc = col + dc
    while (inBounds(nr, nc)) {
      const p = board[nr][nc]
      if (p) {
        if (p.color === attackerColor && (p.type === 'R' || p.type === 'Q')) return true
        break
      }
      nr += dr; nc += dc
    }
  }

  // Sliding pieces: bishop/queen on diagonals
  const bishopDirs = [[-1, -1], [-1, 1], [1, -1], [1, 1]]
  for (const [dr, dc] of bishopDirs) {
    let nr = row + dr, nc = col + dc
    while (inBounds(nr, nc)) {
      const p = board[nr][nc]
      if (p) {
        if (p.color === attackerColor && (p.type === 'B' || p.type === 'Q')) return true
        break
      }
      nr += dr; nc += dc
    }
  }

  return false
}

function findKing(board, color) {
  for (let r = 0; r < 8; r++) {
    for (let c = 0; c < 8; c++) {
      const p = board[r][c]
      if (p && p.type === 'K' && p.color === color) return { row: r, col: c }
    }
  }
  return null
}

function isKingInCheck(board, color) {
  const king = findKing(board, color)
  if (!king) return false
  return isSquareAttacked(board, king.row, king.col, oppositeColor(color))
}

// ─── Move simulation (to check legality: does the move leave own king in check?) ─

/**
 * Apply a move to a clone of the board and return the new board state.
 * This handles captures, en passant, castling, and promotion.
 */
function applyMoveToBoard(board, from, to, castling, enPassantSq, promotion) {
  const b = cloneBoard(board)
  const piece = b[from.row][from.col]
  if (!piece) return { board: b, castling: { ...castling }, enPassantSq: null }

  const newCastling = { ...castling }
  let newEnPassant = null

  // En passant capture
  if (piece.type === 'P' && to.col !== from.col && !b[to.row][to.col]) {
    // Capturing en passant: remove the captured pawn
    b[from.row][to.col] = null
  }

  // Move the piece
  b[to.row][to.col] = piece
  b[from.row][from.col] = null

  // Pawn promotion
  if (piece.type === 'P' && (to.row === 0 || to.row === 7)) {
    b[to.row][to.col] = { type: promotion || 'Q', color: piece.color }
  }

  // Castling: move the rook too
  if (piece.type === 'K' && Math.abs(to.col - from.col) === 2) {
    if (to.col === 6) {
      // Kingside
      b[from.row][5] = b[from.row][7]
      b[from.row][7] = null
    } else if (to.col === 2) {
      // Queenside
      b[from.row][3] = b[from.row][0]
      b[from.row][0] = null
    }
  }

  // Update castling rights
  if (piece.type === 'K') {
    if (piece.color === 'w') { newCastling.K = false; newCastling.Q = false }
    else { newCastling.k = false; newCastling.q = false }
  }
  if (piece.type === 'R') {
    if (from.row === 7 && from.col === 0) newCastling.Q = false
    if (from.row === 7 && from.col === 7) newCastling.K = false
    if (from.row === 0 && from.col === 0) newCastling.q = false
    if (from.row === 0 && from.col === 7) newCastling.k = false
  }
  // If a rook is captured on its home square
  if (to.row === 7 && to.col === 0) newCastling.Q = false
  if (to.row === 7 && to.col === 7) newCastling.K = false
  if (to.row === 0 && to.col === 0) newCastling.q = false
  if (to.row === 0 && to.col === 7) newCastling.k = false

  // En passant target square
  if (piece.type === 'P' && Math.abs(to.row - from.row) === 2) {
    const epRow = (from.row + to.row) / 2
    newEnPassant = indicesToSquare(epRow, from.col)
  }

  return { board: b, castling: newCastling, enPassantSq: newEnPassant }
}

// ─── Pseudo-legal move generation (inspired by C# ChessGenerations.cs) ──────

/**
 * Generate all pseudo-legal destination indices for a piece at (row, col).
 * Does NOT check if the move leaves the king in check.
 */
function generatePseudoMoves(board, row, col, castling, enPassantSq) {
  const piece = board[row][col]
  if (!piece) return []

  const moves = []

  switch (piece.type) {
    case 'P':
      generatePawnMoves(board, row, col, piece.color, enPassantSq, moves)
      break
    case 'N':
      generateKnightMoves(board, row, col, piece.color, moves)
      break
    case 'B':
      generateSlidingMoves(board, row, col, piece.color, [[-1, -1], [-1, 1], [1, -1], [1, 1]], moves)
      break
    case 'R':
      generateSlidingMoves(board, row, col, piece.color, [[-1, 0], [1, 0], [0, -1], [0, 1]], moves)
      break
    case 'Q':
      generateSlidingMoves(board, row, col, piece.color, [[-1, -1], [-1, 1], [1, -1], [1, 1], [-1, 0], [1, 0], [0, -1], [0, 1]], moves)
      break
    case 'K':
      generateKingMoves(board, row, col, piece.color, castling, moves)
      break
  }

  return moves
}

function generatePawnMoves(board, row, col, color, enPassantSq, moves) {
  const dir = color === 'w' ? -1 : 1 // white moves up (decreasing row)
  const startRow = color === 'w' ? 6 : 1

  // Single push
  const r1 = row + dir
  if (inBounds(r1, col) && !board[r1][col]) {
    moves.push({ row: r1, col })
    // Double push
    const r2 = row + 2 * dir
    if (row === startRow && !board[r2][col]) {
      moves.push({ row: r2, col })
    }
  }

  // Captures
  for (const dc of [-1, 1]) {
    const nc = col + dc
    if (!inBounds(r1, nc)) continue
    const target = board[r1][nc]
    if (target && target.color !== color) {
      moves.push({ row: r1, col: nc })
    }
    // En passant
    if (enPassantSq && indicesToSquare(r1, nc) === enPassantSq) {
      moves.push({ row: r1, col: nc })
    }
  }
}

function generateKnightMoves(board, row, col, color, moves) {
  const offsets = [[-2, -1], [-2, 1], [-1, -2], [-1, 2], [1, -2], [1, 2], [2, -1], [2, 1]]
  for (const [dr, dc] of offsets) {
    const nr = row + dr, nc = col + dc
    if (inBounds(nr, nc)) {
      const p = board[nr][nc]
      if (!p || p.color !== color) {
        moves.push({ row: nr, col: nc })
      }
    }
  }
}

function generateSlidingMoves(board, row, col, color, directions, moves) {
  for (const [dr, dc] of directions) {
    let nr = row + dr, nc = col + dc
    while (inBounds(nr, nc)) {
      const p = board[nr][nc]
      if (p) {
        if (p.color !== color) moves.push({ row: nr, col: nc })
        break
      }
      moves.push({ row: nr, col: nc })
      nr += dr; nc += dc
    }
  }
}

function generateKingMoves(board, row, col, color, castling, moves) {
  for (let dr = -1; dr <= 1; dr++) {
    for (let dc = -1; dc <= 1; dc++) {
      if (dr === 0 && dc === 0) continue
      const nr = row + dr, nc = col + dc
      if (inBounds(nr, nc)) {
        const p = board[nr][nc]
        if (!p || p.color !== color) {
          moves.push({ row: nr, col: nc })
        }
      }
    }
  }

  // Castling (inspired by C# KingValidation)
  const enemy = oppositeColor(color)
  // King must be on its start square and not in check
  const homeRow = color === 'w' ? 7 : 0
  if (row !== homeRow || col !== 4) return
  if (isSquareAttacked(board, row, col, enemy)) return

  // Kingside
  const kFlag = color === 'w' ? 'K' : 'k'
  if (castling[kFlag]) {
    const rook = board[homeRow][7]
    if (rook && rook.type === 'R' && rook.color === color &&
        !board[homeRow][5] && !board[homeRow][6] &&
        !isSquareAttacked(board, homeRow, 5, enemy) &&
        !isSquareAttacked(board, homeRow, 6, enemy)) {
      moves.push({ row: homeRow, col: 6 })
    }
  }

  // Queenside
  const qFlag = color === 'w' ? 'Q' : 'q'
  if (castling[qFlag]) {
    const rook = board[homeRow][0]
    if (rook && rook.type === 'R' && rook.color === color &&
        !board[homeRow][1] && !board[homeRow][2] && !board[homeRow][3] &&
        !isSquareAttacked(board, homeRow, 2, enemy) &&
        !isSquareAttacked(board, homeRow, 3, enemy)) {
      moves.push({ row: homeRow, col: 2 })
    }
  }
}

// ─── Legal move generation (filters pseudo-legal by king safety) ─────────────

function getLegalMovesForSquare(boardArr, row, col, turn, castling, enPassantSq) {
  const piece = boardArr[row][col]
  if (!piece || piece.color !== turn) return []

  const pseudos = generatePseudoMoves(boardArr, row, col, castling, enPassantSq)
  const legal = []

  for (const dest of pseudos) {
    // Check if this is a pawn promotion
    const isPromotion = piece.type === 'P' && (dest.row === 0 || dest.row === 7)
    const promoType = isPromotion ? 'Q' : undefined // test with queen; if legal with queen it's legal with all

    const result = applyMoveToBoard(
      boardArr,
      { row, col },
      dest,
      castling,
      enPassantSq,
      promoType,
    )

    if (!isKingInCheck(result.board, turn)) {
      legal.push(indicesToSquare(dest.row, dest.col))
    }
  }

  return legal
}

/**
 * Does the given side have any legal moves?
 */
function hasLegalMoves(boardArr, color, castling, enPassantSq) {
  for (let r = 0; r < 8; r++) {
    for (let c = 0; c < 8; c++) {
      const p = boardArr[r][c]
      if (!p || p.color !== color) continue
      const moves = getLegalMovesForSquare(boardArr, r, c, color, castling, enPassantSq)
      if (moves.length > 0) return true
    }
  }
  return false
}

// ─── SAN generation (inspired by C# SanBuilder) ────────────────────────────

function generateSan(boardArr, from, to, turn, castling, enPassantSq, promotion) {
  const piece = boardArr[from.row][from.col]
  if (!piece) return null

  // Castling
  if (piece.type === 'K' && Math.abs(to.col - from.col) === 2) {
    const result = applyMoveToBoard(boardArr, from, to, castling, enPassantSq)
    const inCheck = isKingInCheck(result.board, oppositeColor(turn))
    const noMoves = !hasLegalMoves(result.board, oppositeColor(turn), result.castling, result.enPassantSq)
    const suffix = inCheck ? (noMoves ? '#' : '+') : ''
    return (to.col === 6 ? 'O-O' : 'O-O-O') + suffix
  }

  let san = ''
  const isCapture = !!boardArr[to.row][to.col] ||
    (piece.type === 'P' && to.col !== from.col) // en passant

  if (piece.type === 'P') {
    if (isCapture) {
      san += String.fromCharCode(97 + from.col) + 'x'
    }
    san += indicesToSquare(to.row, to.col)
    if (promotion) {
      san += '=' + promotion
    }
  } else {
    san += piece.type

    // Disambiguation: find other pieces of same type/color that can move to same square
    const disambigPieces = []
    for (let r = 0; r < 8; r++) {
      for (let c = 0; c < 8; c++) {
        if (r === from.row && c === from.col) continue
        const other = boardArr[r][c]
        if (!other || other.type !== piece.type || other.color !== piece.color) continue
        const otherLegal = getLegalMovesForSquare(boardArr, r, c, turn, castling, enPassantSq)
        if (otherLegal.includes(indicesToSquare(to.row, to.col))) {
          disambigPieces.push({ row: r, col: c })
        }
      }
    }

    if (disambigPieces.length > 0) {
      const sameFile = disambigPieces.some(p => p.col === from.col)
      const sameRank = disambigPieces.some(p => p.row === from.row)
      if (!sameFile) {
        san += String.fromCharCode(97 + from.col)
      } else if (!sameRank) {
        san += (8 - from.row)
      } else {
        san += String.fromCharCode(97 + from.col) + (8 - from.row)
      }
    }

    if (isCapture) san += 'x'
    san += indicesToSquare(to.row, to.col)
  }

  // Check / mate detection
  const result = applyMoveToBoard(boardArr, from, to, castling, enPassantSq, promotion)
  const enemy = oppositeColor(turn)
  const inCheck = isKingInCheck(result.board, enemy)
  const noMoves = !hasLegalMoves(result.board, enemy, result.castling, result.enPassantSq)

  if (inCheck && noMoves) san += '#'
  else if (inCheck) san += '+'

  return san
}

// ─── SAN parsing ─────────────────────────────────────────────────────────────

/**
 * Parse a SAN string and return { from, to, promotion } indices.
 * This finds the matching legal move on the current board.
 */
function parseSan(boardArr, san, turn, castling, enPassantSq) {
  const originalSan = san
  // Strip check/mate symbols
  san = san.replace(/[+#!?]+$/, '')

  // Castling
  if (san === 'O-O' || san === 'O-O-O') {
    const homeRow = turn === 'w' ? 7 : 0
    const from = { row: homeRow, col: 4 }
    const to = { row: homeRow, col: san === 'O-O' ? 6 : 2 }
    return { from, to, promotion: null }
  }

  let promotion = null
  // Check for promotion: e.g. e8=Q
  const promoMatch = san.match(/=([QRBN])$/i)
  if (promoMatch) {
    promotion = promoMatch[1].toUpperCase()
    san = san.replace(/=[QRBN]$/i, '')
  }

  // Remove 'x' for captures
  san = san.replace('x', '')

  let pieceType = 'P'
  if (san[0] >= 'A' && san[0] <= 'Z' && san[0] !== 'O') {
    pieceType = san[0]
    san = san.slice(1)
  }

  // Destination square is always the last two characters
  const destSq = san.slice(-2)
  const dest = squareToIndices(destSq)
  san = san.slice(0, -2)

  // Remaining chars are disambiguation (file, rank, or both)
  let disambigFile = null
  let disambigRank = null
  for (const ch of san) {
    if (ch >= 'a' && ch <= 'h') disambigFile = ch.charCodeAt(0) - 97
    else if (ch >= '1' && ch <= '8') disambigRank = 8 - parseInt(ch)
  }

  // Find the matching piece
  for (let r = 0; r < 8; r++) {
    for (let c = 0; c < 8; c++) {
      const p = boardArr[r][c]
      if (!p || p.type !== pieceType || p.color !== turn) continue
      if (disambigFile !== null && c !== disambigFile) continue
      if (disambigRank !== null && r !== disambigRank) continue

      const legal = getLegalMovesForSquare(boardArr, r, c, turn, castling, enPassantSq)
      if (legal.includes(destSq)) {
        return { from: { row: r, col: c }, to: dest, promotion }
      }
    }
  }

  return null // Couldn't parse
}

// ─── Insufficient material detection ────────────────────────────────────────

function isInsufficientMaterial(board) {
  const pieces = { w: [], b: [] }
  for (let r = 0; r < 8; r++) {
    for (let c = 0; c < 8; c++) {
      const p = board[r][c]
      if (p) pieces[p.color].push({ type: p.type, row: r, col: c })
    }
  }

  const w = pieces.w.filter(p => p.type !== 'K')
  const b = pieces.b.filter(p => p.type !== 'K')

  // K vs K
  if (w.length === 0 && b.length === 0) return true
  // K+B vs K or K+N vs K
  if (w.length === 0 && b.length === 1 && (b[0].type === 'B' || b[0].type === 'N')) return true
  if (b.length === 0 && w.length === 1 && (w[0].type === 'B' || w[0].type === 'N')) return true
  // K+B vs K+B (same colored bishops)
  if (w.length === 1 && b.length === 1 && w[0].type === 'B' && b[0].type === 'B') {
    const wBishopColor = (w[0].row + w[0].col) % 2
    const bBishopColor = (b[0].row + b[0].col) % 2
    if (wBishopColor === bBishopColor) return true
  }

  return false
}

// ─── Captured pieces & material advantage ───────────────────────────────────

function computeCaptured(boardArr) {
  const counts = {
    w: { P: 0, N: 0, B: 0, R: 0, Q: 0, K: 0 },
    b: { P: 0, N: 0, B: 0, R: 0, Q: 0, K: 0 },
  }

  for (let r = 0; r < 8; r++) {
    for (let c = 0; c < 8; c++) {
      const p = boardArr[r][c]
      if (p) counts[p.color][p.type]++
    }
  }

  const capturedByWhite = [] // pieces White captured (Black pieces off the board)
  const capturedByBlack = [] // pieces Black captured (White pieces off the board)

  for (const type of ['Q', 'R', 'B', 'N', 'P']) {
    const wMissing = STARTING_MATERIAL[type] - counts.w[type]
    const bMissing = STARTING_MATERIAL[type] - counts.b[type]
    for (let i = 0; i < Math.max(0, bMissing); i++) capturedByWhite.push(type)
    for (let i = 0; i < Math.max(0, wMissing); i++) capturedByBlack.push(type)
  }

  const whiteMaterial = Object.entries(counts.w).reduce((sum, [t, n]) => sum + PIECE_VALUES[t] * n, 0)
  const blackMaterial = Object.entries(counts.b).reduce((sum, [t, n]) => sum + PIECE_VALUES[t] * n, 0)

  return {
    capturedByWhite,
    capturedByBlack,
    whiteMaterialAdvantage: whiteMaterial - blackMaterial,
  }
}

// ─── Composable ─────────────────────────────────────────────────────────────

/**
 * Client-side chess engine composable.
 * Manages board state, validates moves, generates legal moves, generates SAN.
 */
export function useChessEngine() {
  // Internal state
  let _board = parseFen(STARTING_FEN).board
  let _turn = 'w'
  let _castling = { K: true, Q: true, k: true, q: true }
  let _enPassantSq = null
  let _halfmoveClock = 0
  let _fullmoveNumber = 1

  // State history for undo
  const _stateHistory = []

  // Reactive state
  const board = ref(cloneBoard(_board))
  const fen = ref(STARTING_FEN)
  const turn = ref('w')
  const isCheck = ref(false)
  const isCheckmate = ref(false)
  const isStalemate = ref(false)
  const isDraw = ref(false)
  const moveHistory = ref([])
  const lastMove = ref(null) // { from: 'e2', to: 'e4' }

  function syncReactive() {
    board.value = cloneBoard(_board)
    fen.value = boardToFen(_board, _turn, _castling, _enPassantSq, _halfmoveClock, _fullmoveNumber)
    turn.value = _turn
    isCheck.value = isKingInCheck(_board, _turn)
    const hasMoves = hasLegalMoves(_board, _turn, _castling, _enPassantSq)
    isCheckmate.value = isCheck.value && !hasMoves
    isStalemate.value = !isCheck.value && !hasMoves
    isDraw.value = isStalemate.value || isInsufficientMaterial(_board) || _halfmoveClock >= 100
  }

  /**
   * Get legal destination squares for a piece on the given algebraic square.
   */
  function legalMoves(square) {
    const { row, col } = squareToIndices(square)
    return getLegalMovesForSquare(_board, row, col, _turn, _castling, _enPassantSq)
  }

  /**
   * Make a move from one square to another.
   * Returns { san } on success or null if illegal.
   */
  function makeMove(fromSq, toSq, promotion) {
    const from = squareToIndices(fromSq)
    const to = squareToIndices(toSq)
    const piece = _board[from.row][from.col]

    if (!piece || piece.color !== _turn) return null

    // Check legality
    const legal = getLegalMovesForSquare(_board, from.row, from.col, _turn, _castling, _enPassantSq)
    if (!legal.includes(toSq)) return null

    // Auto-detect promotion need
    if (piece.type === 'P' && (to.row === 0 || to.row === 7) && !promotion) {
      return null // Caller must provide promotion choice
    }

    // Generate SAN before applying the move
    const san = generateSan(_board, from, to, _turn, _castling, _enPassantSq, promotion)

    // Save state for undo
    _stateHistory.push({
      board: cloneBoard(_board),
      turn: _turn,
      castling: { ..._castling },
      enPassantSq: _enPassantSq,
      halfmoveClock: _halfmoveClock,
      fullmoveNumber: _fullmoveNumber,
      lastMove: lastMove.value,
    })

    // Apply move
    const isCapture = !!_board[to.row][to.col] || (piece.type === 'P' && to.col !== from.col)
    const result = applyMoveToBoard(_board, from, to, _castling, _enPassantSq, promotion)
    _board = result.board
    _castling = result.castling
    _enPassantSq = result.enPassantSq

    // Update clocks
    if (piece.type === 'P' || isCapture) {
      _halfmoveClock = 0
    } else {
      _halfmoveClock++
    }
    if (_turn === 'b') _fullmoveNumber++
    _turn = oppositeColor(_turn)

    lastMove.value = { from: fromSq, to: toSq }
    moveHistory.value = [...moveHistory.value, san]

    syncReactive()
    return { san }
  }

  /**
   * Apply a SAN move string (used when receiving opponent's move from server).
   */
  function applySanMove(san) {
    const parsed = parseSan(_board, san, _turn, _castling, _enPassantSq)
    if (!parsed) return null

    const fromSq = indicesToSquare(parsed.from.row, parsed.from.col)
    const toSq = indicesToSquare(parsed.to.row, parsed.to.col)
    return makeMove(fromSq, toSq, parsed.promotion)
  }

  /**
   * Load a FEN string.
   */
  function loadFen(fenStr) {
    const state = parseFen(fenStr)
    _board = state.board
    _turn = state.turn
    _castling = state.castling
    _enPassantSq = state.enPassantSq
    _halfmoveClock = state.halfmoveClock
    _fullmoveNumber = state.fullmoveNumber
    moveHistory.value = []
    lastMove.value = null
    _stateHistory.length = 0
    syncReactive()
  }

  /**
   * Load from encoded move data string (pipe-delimited tokens).
   * Replays all moves from the starting position.
   */
  function loadMoveData(moveData) {
    reset()
    if (!moveData) return
    const tokens = moveData.split('|')
    for (const token of tokens) {
      const parts = token.split(':')
      const san = parts[1]
      // Skip control tokens
      if (['resign', 'timeout', 'draw-offer', 'draw-accept', 'abort'].includes(san)) continue
      applySanMove(san)
    }
  }

  /**
   * Reset to starting position.
   */
  function reset() {
    const state = parseFen(STARTING_FEN)
    _board = state.board
    _turn = state.turn
    _castling = state.castling
    _enPassantSq = state.enPassantSq
    _halfmoveClock = state.halfmoveClock
    _fullmoveNumber = state.fullmoveNumber
    moveHistory.value = []
    lastMove.value = null
    _stateHistory.length = 0
    syncReactive()
  }

  /**
   * Undo the last move (for reverting optimistic updates on server rejection).
   */
  function undoLastMove() {
    if (_stateHistory.length === 0) return

    const prev = _stateHistory.pop()
    _board = prev.board
    _turn = prev.turn
    _castling = prev.castling
    _enPassantSq = prev.enPassantSq
    _halfmoveClock = prev.halfmoveClock
    _fullmoveNumber = prev.fullmoveNumber
    lastMove.value = prev.lastMove

    moveHistory.value = moveHistory.value.slice(0, -1)
    syncReactive()
  }

  /**
   * Get the king square for the side currently in check (for highlighting).
   */
  const checkSquare = computed(() => {
    if (!isCheck.value) return null
    const king = findKing(board.value, turn.value)
    if (!king) return null
    return indicesToSquare(king.row, king.col)
  })

  /**
   * Compute captured pieces and material advantage.
   */
  const captured = computed(() => computeCaptured(board.value))

  // Initialize reactive state
  syncReactive()

  return {
    board,
    fen,
    turn,
    isCheck,
    isCheckmate,
    isStalemate,
    isDraw,
    moveHistory,
    lastMove,
    checkSquare,
    captured,
    legalMoves,
    makeMove,
    applySanMove,
    loadFen,
    loadMoveData,
    reset,
    undoLastMove,
  }
}
