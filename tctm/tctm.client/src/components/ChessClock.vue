<script setup>
/**
 * ChessClock.vue — Ticking clock display for one player.
 */
import { ref, watch, onBeforeUnmount } from 'vue'

const props = defineProps({
  remainingMs: { type: Number, required: true },
  active: { type: Boolean, default: false },
  initialMs: { type: Number, default: 300000 },
})

const displayMs = ref(props.remainingMs)
let animFrame = null
let lastTimestamp = null

function tick(timestamp) {
  if (!lastTimestamp) lastTimestamp = timestamp
  const delta = timestamp - lastTimestamp
  lastTimestamp = timestamp

  displayMs.value = Math.max(0, displayMs.value - delta)

  if (displayMs.value > 0 && props.active) {
    animFrame = requestAnimationFrame(tick)
  }
}

function startTicking() {
  stopTicking()
  lastTimestamp = null
  animFrame = requestAnimationFrame(tick)
}

function stopTicking() {
  if (animFrame) {
    cancelAnimationFrame(animFrame)
    animFrame = null
  }
  lastTimestamp = null
}

// Sync with incoming server time
watch(() => props.remainingMs, (val) => {
  displayMs.value = val
})

watch(() => props.active, (val) => {
  if (val) startTicking()
  else stopTicking()
}, { immediate: true })

onBeforeUnmount(() => stopTicking())

function formatTime(ms) {
  if (ms <= 0) return '0:00'
  const totalSeconds = Math.floor(ms / 1000)
  const tenths = Math.floor((ms % 1000) / 100)
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60

  if (totalSeconds < 10) {
    return `${seconds}.${tenths}`
  }
  return `${minutes}:${seconds.toString().padStart(2, '0')}`
}

function isLow() {
  return displayMs.value < 30000
}

function isCritical() {
  return displayMs.value < 10000
}
</script>

<template>
  <div
    class="chess-clock"
    :class="{
      'clock--active': active,
      'clock--low': isLow(),
      'clock--critical': isCritical(),
    }"
  >
    <span class="clock-time">{{ formatTime(displayMs) }}</span>
  </div>
</template>

<style scoped>
.chess-clock {
  font-family: 'Roboto Mono', monospace;
  font-size: 1.25rem;
  font-weight: 700;
  padding: 4px 12px;
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.08);
  color: rgba(255, 255, 255, 0.7);
  min-width: 72px;
  text-align: center;
  transition: all 0.2s ease;
}

.clock--active {
  background: rgba(255, 255, 255, 0.15);
  color: #ffffff;
}

.clock--low {
  color: #ef4444;
  background: rgba(239, 68, 68, 0.15);
}

.clock--critical {
  animation: pulse-clock 0.5s ease-in-out infinite;
}

@keyframes pulse-clock {
  0%, 100% { opacity: 1; transform: scale(1); }
  50% { opacity: 0.7; transform: scale(1.03); }
}

.clock-time {
  white-space: nowrap;
}
</style>
