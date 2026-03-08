<script setup>
/**
 * ChessPiece.vue — Renders a chess piece from the SVG spritesheet.
 *
 * Requires the chess_pieces_1.svg spritesheet to be inlined once
 * in the root App.vue (hidden).
 */
defineProps({
  /** Piece type: K, Q, R, B, N, P */
  type: { type: String, required: true },
  /** Piece color: 'w' or 'b' */
  color: { type: String, required: true },
  /** CSS size (width & height) — number (px) or string */
  size: { type: [Number, String], default: '100%' },
})

function cssSize(s) {
  return typeof s === 'number' ? `${s}px` : s
}
</script>

<template>
  <svg
    class="chess-piece-svg"
    :width="cssSize(size)"
    :height="cssSize(size)"
    viewBox="0 0 45 45"
  >
    <use :href="`#${color}${type}`" />
  </svg>
</template>

<style scoped>
.chess-piece-svg {
  display: block;
  pointer-events: none; /* let parent handle events */
}
</style>
