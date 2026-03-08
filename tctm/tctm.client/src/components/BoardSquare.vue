<script setup>
/**
 * BoardSquare.vue — A single square on the chessboard.
 */
import ChessPiece from './ChessPiece.vue'

const props = defineProps({
  piece: { type: Object, default: null },
  square: { type: String, required: true },
  isLight: { type: Boolean, required: true },
  isSelected: { type: Boolean, default: false },
  isLegal: { type: Boolean, default: false },
  isLastMove: { type: Boolean, default: false },
  isCheck: { type: Boolean, default: false },
  interactive: { type: Boolean, default: false },
  draggable: { type: Boolean, default: false },
})

const emit = defineEmits(['square-click', 'piece-drop', 'drag-start', 'drag-end'])

function onClick() {
  emit('square-click', { square: props.square })
}

function onDragStart(e) {
  if (!props.draggable) {
    e.preventDefault()
    return
  }
  e.dataTransfer.setData('text/plain', props.square)
  e.dataTransfer.effectAllowed = 'move'
  emit('drag-start', { square: props.square })
}

function onDragEnd() {
  emit('drag-end')
}

function onDragOver(e) {
  if (props.isLegal || props.interactive) {
    e.preventDefault()
    e.dataTransfer.dropEffect = 'move'
  }
}

function onDrop(e) {
  e.preventDefault()
  const from = e.dataTransfer.getData('text/plain')
  if (from && from !== props.square) {
    emit('piece-drop', { from, to: props.square })
  }
}

// Label for accessibility
function ariaLabel() {
  const pieceName = props.piece
    ? `${props.piece.color === 'w' ? 'white' : 'black'} ${pieceName_(props.piece.type)}`
    : 'empty'
  return `${props.square}, ${pieceName}`
}

function pieceName_(type) {
  const names = { K: 'king', Q: 'queen', R: 'rook', B: 'bishop', N: 'knight', P: 'pawn' }
  return names[type] || type
}
</script>

<template>
  <div
    class="board-square"
    :class="{
      'board-square--light': isLight,
      'board-square--dark': !isLight,
      'board-square--selected': isSelected,
      'board-square--last-move': isLastMove,
      'board-square--check': isCheck,
      'board-square--legal-empty': isLegal && !piece,
      'board-square--legal-capture': isLegal && !!piece,
    }"
    :aria-label="ariaLabel()"
    role="button"
    tabindex="0"
    @click="onClick"
    @dragover="onDragOver"
    @drop="onDrop"
    @keydown.enter="onClick"
  >
    <!-- Legal move indicator (empty square) -->
    <span v-if="isLegal && !piece" class="legal-dot" />

    <!-- Legal capture ring -->
    <span v-if="isLegal && piece" class="legal-ring" />

    <!-- Piece -->
    <span
      v-if="piece"
      class="piece-svg-wrap"
      :class="{ 'cursor-grab': draggable }"
      :draggable="draggable"
      @dragstart="onDragStart"
      @dragend="onDragEnd"
    >
      <ChessPiece :type="piece.type" :color="piece.color" />
    </span>
  </div>
</template>

<style scoped>
.board-square {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  aspect-ratio: 1;
  user-select: none;
  transition: background-color 0.15s ease;
}

.board-square--light {
  background-color: rgb(235, 236, 211);
}

.board-square--dark {
  background-color: rgb(124, 147, 93);
}

.board-square--selected {
  box-shadow: inset 0 0 0 3px #3b82f6; /* blue-500 ring */
}

.board-square--last-move {
  background-color: rgba(253, 224, 71, 0.4) !important; /* yellow-300/40 */
}

.board-square--check {
  background: radial-gradient(circle, rgba(239, 68, 68, 0.6) 0%, rgba(239, 68, 68, 0.2) 60%, transparent 80%) !important;
}

.board-square--legal-capture {
  box-shadow: inset 0 0 0 3px rgba(0, 0, 0, 0.25);
  border-radius: 0;
}

.piece-svg-wrap {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 85%;
  height: 85%;
  z-index: 1;
  pointer-events: auto;
}

.cursor-grab {
  cursor: grab;
}

.cursor-grab:active {
  cursor: grabbing;
}

.legal-dot {
  position: absolute;
  width: 30%;
  height: 30%;
  border-radius: 50%;
  background-color: rgba(0, 0, 0, 0.2);
  z-index: 2;
  pointer-events: none;
}

.legal-ring {
  position: absolute;
  inset: 0;
  border-radius: 50%;
  box-shadow: inset 0 0 0 4px rgba(0, 0, 0, 0.25);
  z-index: 0;
  pointer-events: none;
}

@keyframes shake {
  0%, 100% { transform: translateX(0); }
  20% { transform: translateX(-4px); }
  40% { transform: translateX(4px); }
  60% { transform: translateX(-3px); }
  80% { transform: translateX(3px); }
}

.board-square--shake {
  animation: shake 0.3s ease;
}
</style>
