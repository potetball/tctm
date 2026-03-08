<script setup>
/**
 * GetReadyOverlay.vue — Shown to both players when both are present and the game hasn't started.
 * White sees "Waiting for opponent to start the clock…"
 * Black sees "Press Start Game to begin!"
 */

defineProps({
  show: { type: Boolean, default: false },
  myColor: { type: String, default: null },
})

const emit = defineEmits(['start-game'])
</script>

<template>
  <Transition name="fade">
    <div v-if="show" class="get-ready-overlay">
      <div class="get-ready-card">
        <div class="crown">{{ myColor === 'b' ? '♚' : '♔' }}</div>
        <h2 class="title">Get Ready</h2>
        <p class="subtitle">
          {{ myColor === 'b' ? 'You have the black pieces' : 'Waiting for your opponent to start the clock…' }}
        </p>
        <button
          v-if="myColor === 'b'"
          class="start-btn"
          @click="emit('start-game')"
        >
          ▶ Start Game
        </button>
      </div>
    </div>
  </Transition>
</template>

<style scoped>
.get-ready-overlay {
  position: absolute;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 10;
  border-radius: inherit;
}

.get-ready-card {
  background: white;
  border-radius: 16px;
  box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.5);
  padding: 2rem;
  text-align: center;
  max-width: 280px;
}

.crown {
  font-size: 4rem;
  line-height: 1;
  margin-bottom: 0.25rem;
}

.title {
  font-size: 1.5rem;
  font-weight: 700;
  margin-top: 0.5rem;
  color: #1a1a2e;
}

.subtitle {
  font-size: 0.875rem;
  color: #6b7280;
  margin-top: 0.5rem;
  animation: pulse-text 2s ease-in-out infinite;
}

.start-btn {
  margin-top: 1rem;
  padding: 0.75rem 2rem;
  background: #2e7d32;
  color: white;
  border: none;
  border-radius: 12px;
  font-size: 1.1rem;
  font-weight: 700;
  cursor: pointer;
  transition: background 0.2s, transform 0.1s;
  box-shadow: 0 4px 12px rgba(46, 125, 50, 0.4);
}

.start-btn:hover {
  background: #388e3c;
  transform: scale(1.03);
}

.start-btn:active {
  transform: scale(0.97);
}

@keyframes pulse-text {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.4s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>
