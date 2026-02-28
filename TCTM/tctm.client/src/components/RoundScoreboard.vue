<script setup>
import { computed } from 'vue'

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  standings: { type: Array, default: () => [] },
  roundNumber: { type: Number, default: 0 },
})

const emit = defineEmits(['update:modelValue'])

const show = computed({
  get: () => props.modelValue,
  set: (v) => emit('update:modelValue', v),
})

const top3 = computed(() => props.standings.slice(0, 3))
const rest = computed(() => props.standings.slice(3))

// Podium order: 2nd, 1st, 3rd (left, center, right)
const podiumOrder = computed(() => {
  const t = top3.value
  if (t.length === 0) return []
  if (t.length === 1) return [{ ...t[0], place: 1 }]
  if (t.length === 2) return [{ ...t[1], place: 2 }, { ...t[0], place: 1 }]
  return [{ ...t[1], place: 2 }, { ...t[0], place: 1 }, { ...t[2], place: 3 }]
})

function placeColor(place) {
  if (place === 1) return '#FFD700'
  if (place === 2) return '#C0C0C0'
  return '#CD7F32'
}

function placeIcon(place) {
  if (place === 1) return 'mdi-trophy'
  if (place === 2) return 'mdi-medal'
  return 'mdi-medal-outline'
}

function podiumHeight(place) {
  if (place === 1) return '140px'
  if (place === 2) return '100px'
  return '70px'
}
</script>

<template>
  <v-dialog v-model="show" max-width="720" persistent>
    <v-card class="scoreboard-card pa-0" rounded="xl" elevation="12">
      <!-- Header -->
      <div class="scoreboard-header text-center py-6 px-4">
        <div class="confetti-container">
          <span v-for="i in 30" :key="i" class="confetti-piece" :style="{ '--i': i }" />
        </div>
        <v-icon icon="mdi-flag-checkered" size="36" color="white" class="mb-2" />
        <h2 class="text-h4 font-weight-black text-white">Round {{ roundNumber }} Complete!</h2>
        <p class="text-body-2 text-white mt-1" style="opacity: 0.8;">Here are the current standings</p>
      </div>

      <!-- Podium -->
      <div class="podium-section px-4 pt-6 pb-2" v-if="podiumOrder.length">
        <div class="podium-row d-flex justify-center align-end ga-3">
          <div
            v-for="entry in podiumOrder"
            :key="entry.playerId"
            class="podium-entry text-center"
            :class="{ 'podium-winner': entry.place === 1 }"
          >
            <!-- Player avatar & name -->
            <div class="podium-player mb-2">
              <v-avatar
                :size="entry.place === 1 ? 72 : 56"
                :color="placeColor(entry.place)"
                class="podium-avatar elevation-4"
              >
                <v-icon :icon="placeIcon(entry.place)" :size="entry.place === 1 ? 36 : 28" color="white" />
              </v-avatar>
              <div class="mt-2">
                <div class="text-body-1 font-weight-bold" :class="{ 'text-h6': entry.place === 1 }">
                  {{ entry.displayName }}
                </div>
                <div class="text-h6 font-weight-black" :style="{ color: placeColor(entry.place) }">
                  {{ entry.points }} pts
                </div>
              </div>
            </div>
            <!-- Podium block -->
            <div
              class="podium-block"
              :style="{
                height: podiumHeight(entry.place),
                background: `linear-gradient(180deg, ${placeColor(entry.place)}33 0%, ${placeColor(entry.place)}11 100%)`,
                borderTop: `3px solid ${placeColor(entry.place)}`,
              }"
            >
              <span class="podium-place text-h4 font-weight-black" :style="{ color: placeColor(entry.place) }">
                {{ entry.place }}
              </span>
            </div>
          </div>
        </div>
      </div>

      <!-- Scrollable standings table -->
      <div class="px-4 pb-2" v-if="rest.length">
        <v-divider class="my-3" />
        <div style="max-height: 240px; overflow-y: auto;">
          <v-table density="compact" class="standings-table">
            <thead>
              <tr>
                <th class="text-center" style="width: 50px;">#</th>
                <th>Player</th>
                <th class="text-center">Pts</th>
                <th class="text-center">W</th>
                <th class="text-center">D</th>
                <th class="text-center">L</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(s, index) in rest" :key="s.playerId">
                <td class="text-center text-medium-emphasis">{{ index + 4 }}</td>
                <td>{{ s.displayName }}</td>
                <td class="text-center font-weight-bold">{{ s.points }}</td>
                <td class="text-center">{{ s.wins }}</td>
                <td class="text-center">{{ s.draws }}</td>
                <td class="text-center">{{ s.losses }}</td>
              </tr>
            </tbody>
          </v-table>
        </div>
      </div>

      <!-- Close button -->
      <div class="text-center pb-6 pt-3 px-4">
        <v-btn
          color="amber-darken-2"
          size="large"
          rounded="lg"
          min-width="200"
          @click="show = false"
        >
          <v-icon icon="mdi-check" class="mr-1" /> Got it!
        </v-btn>
      </div>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.scoreboard-card {
  overflow: hidden;
}

.scoreboard-header {
  background: linear-gradient(135deg, #1a1a2e 0%, #16213e 50%, #0f3460 100%);
  position: relative;
  overflow: hidden;
}

.confetti-container {
  position: absolute;
  inset: 0;
  pointer-events: none;
  overflow: hidden;
}

.confetti-piece {
  position: absolute;
  width: 8px;
  height: 8px;
  top: -10px;
  left: calc(var(--i) * 3.3%);
  opacity: 0;
  animation: confetti-fall 3s ease-in-out infinite;
  animation-delay: calc(var(--i) * 0.1s);
}

.confetti-piece:nth-child(3n)   { background: #FFD700; border-radius: 50%; }
.confetti-piece:nth-child(3n+1) { background: #C0C0C0; border-radius: 2px; transform: rotate(45deg); }
.confetti-piece:nth-child(3n+2) { background: #CD7F32; border-radius: 2px; }

@keyframes confetti-fall {
  0%   { top: -10px; opacity: 1; transform: rotate(0deg) translateX(0); }
  100% { top: 110%; opacity: 0; transform: rotate(360deg) translateX(20px); }
}

.podium-section {
  background: linear-gradient(180deg, rgba(255,215,0,0.03) 0%, transparent 100%);
}

.podium-row {
  min-height: 280px;
}

.podium-entry {
  flex: 1;
  max-width: 180px;
  display: flex;
  flex-direction: column;
  justify-content: flex-end;
}

.podium-avatar {
  border: 3px solid rgba(255,255,255,0.3);
}

.podium-winner .podium-avatar {
  animation: pulse-gold 2s ease-in-out infinite;
}

@keyframes pulse-gold {
  0%, 100% { box-shadow: 0 0 0 0 rgba(255, 215, 0, 0.4); }
  50%      { box-shadow: 0 0 20px 8px rgba(255, 215, 0, 0.2); }
}

.podium-block {
  border-radius: 8px 8px 0 0;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: height 0.5s ease;
}

.podium-place {
  opacity: 0.4;
}

.standings-table {
  background: transparent !important;
}
</style>
