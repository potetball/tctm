<script setup>
/**
 * ChessSpritesheet.vue — Inlines the chess pieces SVG spritesheet (hidden).
 * Include this component once at the app root so all <use href="#wK"> etc. resolve.
 */
import spritesheetUrl from '@/assets/chess_pieces_1.svg?raw'
</script>

<template>
  <!-- Hidden container: renders the <svg> with all <symbol> defs into the DOM -->
  <div v-once aria-hidden="true" style="position: absolute; width: 0; height: 0; overflow: hidden;" v-html="spritesheetUrl" />
</template>
