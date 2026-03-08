<script setup>
/**
 * ChessBoard.vue — 8×8 board grid with orientation, highlights, and interaction.
 */
import { computed } from 'vue'
import BoardSquare from './BoardSquare.vue'

const props = defineProps({
  board: { type: Array, required: true },
  orientation: { type: String, default: 'w' },
  interactive: { type: Boolean, default: false },
  myColor: { type: String, default: null },
  lastMove: { type: Object, default: null },
  selectedSquare: { type: String, default: null },
  legalSquares: { type: Array, default: () => [] },
  checkSquare: { type: String, default: null },
})

const emit = defineEmits(['square-click', 'piece-drop', 'drag-start', 'drag-end'])

/**
 * Convert board array indices to the correct display order based on orientation.
 * Returns an array of 64 items with { row, col, square, piece, isLight }.
 */
const squares = computed(() => {
  const result = []
  for (let displayRow = 0; displayRow < 8; displayRow++) {
    for (let displayCol = 0; displayCol < 8; displayCol++) {
      let row, col
      if (props.orientation === 'w') {
        row = displayRow
        col = displayCol
      } else {
        row = 7 - displayRow
        col = 7 - displayCol
      }

      const file = String.fromCharCode(97 + col) // a-h
      const rank = 8 - row // 1-8
      const square = `${file}${rank}`
      const piece = props.board[row]?.[col] || null
      const isLight = (row + col) % 2 === 1

      result.push({ row, col, square, piece, isLight })
    }
  }
  return result
})

/** File labels (a-h or h-a) for the bottom edge. */
const fileLabels = computed(() => {
  const files = ['a', 'b', 'c', 'd', 'e', 'f', 'g', 'h']
  return props.orientation === 'w' ? files : [...files].reverse()
})

/** Rank labels (8-1 or 1-8) for the left edge. */
const rankLabels = computed(() => {
  const ranks = ['8', '7', '6', '5', '4', '3', '2', '1']
  return props.orientation === 'w' ? ranks : [...ranks].reverse()
})

function isSelected(square) {
  return props.selectedSquare === square
}

function isLegal(square) {
  return props.legalSquares.includes(square)
}

function isLastMove(square) {
  if (!props.lastMove) return false
  return props.lastMove.from === square || props.lastMove.to === square
}

function isCheck(square) {
  return props.checkSquare === square
}

function isDraggable(piece) {
  if (!props.interactive || !piece || !props.myColor) return false
  return piece.color === props.myColor
}

function onSquareClick(payload) {
  emit('square-click', payload)
}

function onPieceDrop(payload) {
  emit('piece-drop', payload)
}

function onDragStart(payload) {
  emit('drag-start', payload)
}

function onDragEnd() {
  emit('drag-end')
}
</script>

<template>
  <div class="chess-board-wrapper">
    <!-- Rank labels (left side) -->
    <div class="rank-labels">
      <div v-for="r in rankLabels" :key="r" class="rank-label">{{ r }}</div>
    </div>

    <!-- Board grid -->
    <div class="chess-board">
      <BoardSquare
        v-for="sq in squares"
        :key="sq.square"
        :piece="sq.piece"
        :square="sq.square"
        :is-light="sq.isLight"
        :is-selected="isSelected(sq.square)"
        :is-legal="isLegal(sq.square)"
        :is-last-move="isLastMove(sq.square)"
        :is-check="isCheck(sq.square)"
        :interactive="interactive"
        :draggable="isDraggable(sq.piece)"
        @square-click="onSquareClick"
        @piece-drop="onPieceDrop"
        @drag-start="onDragStart"
        @drag-end="onDragEnd"
      />
    </div>

    <!-- File labels (bottom) -->
    <div class="file-labels">
      <div class="rank-label-spacer" />
      <div class="file-labels-row">
        <div v-for="f in fileLabels" :key="f" class="file-label">{{ f }}</div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.chess-board-wrapper {
  display: flex;
  flex-direction: column;
  width: 100%;
}

.rank-labels {
  position: absolute;
  left: 0;
  top: 0;
  bottom: 0;
  width: 20px;
  display: flex;
  flex-direction: column;
}

.rank-label {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 0.7rem;
  color: rgba(255, 255, 255, 0.5);
  user-select: none;
}

.chess-board-wrapper {
  position: relative;
  padding-left: 20px;
}

.chess-board {
  display: grid;
  grid-template-columns: repeat(8, 1fr);
  grid-template-rows: repeat(8, 1fr);
  aspect-ratio: 1;
  border-radius: 4px;
  overflow: hidden;
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.3);
}

.file-labels {
  display: flex;
}

.rank-label-spacer {
  width: 0px;
}

.file-labels-row {
  flex: 1;
  display: flex;
}

.file-label {
  flex: 1;
  text-align: center;
  font-size: 0.7rem;
  color: rgba(255, 255, 255, 0.5);
  padding-top: 2px;
  user-select: none;
}
</style>
