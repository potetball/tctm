<script setup>
/**
 * PromotionDialog.vue — Pawn promotion picker.
 */
import ChessPiece from './ChessPiece.vue'

const props = defineProps({
  color: { type: String, default: 'w' },
  show: { type: Boolean, default: false },
})

const emit = defineEmits(['update:show', 'promotion-select'])

const pieces = ['Q', 'R', 'B', 'N']

function select(piece) {
  emit('promotion-select', { piece })
  emit('update:show', false)
}
</script>

<template>
  <v-dialog :model-value="show" @update:model-value="emit('update:show', $event)" max-width="260" persistent>
    <v-card class="pa-4 text-center" rounded="xl">
      <div class="text-subtitle-1 font-weight-bold mb-3">Choose promotion</div>
      <div class="d-flex justify-center ga-2">
        <v-btn
          v-for="p in pieces"
          :key="p"
          size="x-large"
          variant="outlined"
          rounded="lg"
          class="promotion-btn"
          @click="select(p)"
        >
          <ChessPiece :type="p" :color="color" :size="40" />
        </v-btn>
      </div>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.promotion-btn {
  min-width: 52px !important;
  min-height: 52px !important;
}
</style>
