<script setup>
/**
 * PlayerBar.vue — Player info bar with captured pieces, material advantage, and clock.
 */
import ChessClock from './ChessClock.vue'
import ChessPiece from './ChessPiece.vue'

const props = defineProps({
  player: { type: Object, default: () => ({ id: '', displayName: '?' }) },
  color: { type: String, default: 'w' },
  clockMs: { type: Number, default: 0 },
  clockActive: { type: Boolean, default: false },
  initialClockMs: { type: Number, default: 300000 },
  capturedPieces: { type: Array, default: () => [] },
  materialAdvantage: { type: Number, default: 0 },
  isCurrentUser: { type: Boolean, default: false },
})

/** Color of the captured pieces (the opponent's color). */
function capturedColor() {
  return props.color === 'w' ? 'b' : 'w'
}
</script>

<template>
  <div
    class="player-bar"
    :class="{ 'player-bar--active': isCurrentUser }"
  >
    <div class="player-bar__info">
      <span class="captured-pieces">
        <span v-for="(p, i) in capturedPieces" :key="i" class="captured-piece">
          <ChessPiece :type="p" :color="capturedColor()" size="1em" />
        </span>
        <span v-if="materialAdvantage > 0" class="material-adv">+{{ materialAdvantage }}</span>
      </span>
      <span class="player-name" :class="{ 'font-weight-bold': isCurrentUser }">
        {{ player.displayName || '?' }}
      </span>
    </div>
    <ChessClock
      :remaining-ms="clockMs"
      :active="clockActive"
      :initial-ms="initialClockMs"
    />
  </div>
</template>

<style scoped>
.player-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 12px;
  border-radius: 10px;
  background: rgba(255, 255, 255, 0.05);
  min-height: 48px;
  gap: 8px;
}

.player-bar--active {
  border: 1px solid rgba(251, 191, 36, 0.4);
}

.player-bar__info {
  display: flex;
  align-items: center;
  gap: 8px;
  overflow: hidden;
  flex: 1;
  min-width: 0;
}

.player-name {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  font-size: 0.95rem;
}

.captured-pieces {
  display: flex;
  align-items: center;
  gap: 1px;
  flex-shrink: 0;
}

.captured-piece {
  font-size: 0.85rem;
  line-height: 1;
  opacity: 0.8;
  display: inline-flex;
  align-items: center;
  width: 1.1em;
  height: 1.1em;
}

.material-adv {
  font-size: 0.75rem;
  font-weight: 700;
  color: rgba(255, 255, 255, 0.5);
  margin-left: 2px;
}
</style>
