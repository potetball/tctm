<script setup>
/**
 * MoveList.vue — Move history in standard two-column chess notation.
 */
import { watch, ref, nextTick } from 'vue'

const props = defineProps({
  moves: { type: Array, default: () => [] },
  currentPly: { type: Number, default: 0 },
})

const scrollContainer = ref(null)

/** Group moves into pairs: [{ number, white, black }] */
function movePairs() {
  const pairs = []
  for (let i = 0; i < props.moves.length; i += 2) {
    pairs.push({
      number: Math.floor(i / 2) + 1,
      white: props.moves[i] || '',
      black: props.moves[i + 1] || '',
    })
  }
  return pairs
}

// Auto-scroll to latest move
watch(
  () => props.moves.length,
  async () => {
    await nextTick()
    if (scrollContainer.value) {
      scrollContainer.value.scrollTop = scrollContainer.value.scrollHeight
    }
  },
)
</script>

<template>
  <div class="move-list" ref="scrollContainer">
    <div v-if="!moves.length" class="text-body-2 text-medium-emphasis text-center pa-4">
      No moves yet
    </div>
    <table v-else class="move-table">
      <tbody>
        <tr v-for="pair in movePairs()" :key="pair.number">
          <td class="move-number">{{ pair.number }}.</td>
          <td
            class="move-san"
            :class="{ 'move-san--current': currentPly === (pair.number - 1) * 2 + 1 }"
          >
            {{ pair.white }}
          </td>
          <td
            class="move-san"
            :class="{ 'move-san--current': currentPly === (pair.number - 1) * 2 + 2 }"
          >
            {{ pair.black }}
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<style scoped>
.move-list {
  max-height: 360px;
  overflow-y: auto;
  font-family: 'Roboto Mono', monospace;
  font-size: 0.85rem;
  scrollbar-width: thin;
}

.move-table {
  width: 100%;
  border-collapse: collapse;
}

.move-number {
  color: rgba(255, 255, 255, 0.4);
  padding: 2px 6px;
  text-align: right;
  width: 36px;
  white-space: nowrap;
}

.move-san {
  padding: 2px 8px;
  cursor: default;
  border-radius: 4px;
  width: 50%;
}

.move-san:hover {
  background: rgba(255, 255, 255, 0.08);
}

.move-san--current {
  background: rgba(251, 191, 36, 0.2);
  font-weight: 700;
}
</style>
