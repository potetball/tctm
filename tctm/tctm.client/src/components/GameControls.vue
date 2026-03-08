<script setup>
/**
 * GameControls.vue — Action buttons for the live game.
 */
import { ref } from 'vue'

const props = defineProps({
  gameStatus: { type: String, default: 'NotStarted' },
  isPlayer: { type: Boolean, default: false },
  isAdmin: { type: Boolean, default: false },
  isMyTurn: { type: Boolean, default: false },
  drawOffered: { type: Boolean, default: false },
  isBlack: { type: Boolean, default: false },
  bothPlayersPresent: { type: Boolean, default: false },
})

const emit = defineEmits([
  'start-game',
  'resign',
  'offer-draw',
  'accept-draw',
  'decline-draw',
  'abort-game',
  'flip-board',
  'back-to-tournament',
])

const confirmResign = ref(false)
const confirmAbort = ref(false)

function doResign() {
  confirmResign.value = false
  emit('resign')
}

function doAbort() {
  confirmAbort.value = false
  emit('abort-game')
}
</script>

<template>
  <div class="game-controls">
    <!-- Start Game (Black or Admin, before game starts) -->
    <v-btn
      v-if="gameStatus === 'NotStarted' && (isBlack || isAdmin) && bothPlayersPresent"
      color="green"
      variant="flat"
      prepend-icon="mdi-play"
      @click="emit('start-game')"
    >
      Start Game
    </v-btn>

    <!-- Draw Offered to this player -->
    <template v-if="drawOffered">
      <v-btn
        color="amber-darken-2"
        variant="flat"
        prepend-icon="mdi-handshake"
        @click="emit('accept-draw')"
      >
        Accept Draw
      </v-btn>
      <v-btn
        variant="outlined"
        prepend-icon="mdi-close"
        @click="emit('decline-draw')"
      >
        Decline
      </v-btn>
    </template>

    <!-- Offer Draw (player's turn, in progress) -->
    <v-btn
      v-if="gameStatus === 'InProgress' && isPlayer && isMyTurn && !drawOffered"
      variant="outlined"
      prepend-icon="mdi-handshake"
      size="small"
      @click="emit('offer-draw')"
    >
      Offer Draw
    </v-btn>

    <!-- Resign -->
    <v-btn
      v-if="gameStatus === 'InProgress' && isPlayer"
      variant="outlined"
      color="red"
      prepend-icon="mdi-flag-outline"
      size="small"
      @click="confirmResign = true"
    >
      Resign
    </v-btn>

    <!-- Abort (admin) -->
    <v-btn
      v-if="isAdmin && gameStatus !== 'Completed'"
      variant="outlined"
      color="red"
      prepend-icon="mdi-cancel"
      size="small"
      @click="confirmAbort = true"
    >
      Abort
    </v-btn>

    <!-- Flip Board -->
    <v-btn
      variant="text"
      icon="mdi-rotate-3d-variant"
      size="small"
      title="Flip board"
      @click="emit('flip-board')"
    />

    <!-- Back to Tournament -->
    <v-btn
      variant="text"
      prepend-icon="mdi-arrow-left"
      size="small"
      @click="emit('back-to-tournament')"
    >
      Back
    </v-btn>

    <!-- Resign confirmation -->
    <v-dialog v-model="confirmResign" max-width="340" persistent>
      <v-card class="pa-5" rounded="xl">
        <h3 class="text-h6 font-weight-bold mb-2">Resign?</h3>
        <p class="text-body-2 mb-4">Are you sure you want to resign this game?</p>
        <div class="d-flex ga-2">
          <v-btn variant="text" @click="confirmResign = false">Cancel</v-btn>
          <v-spacer />
          <v-btn color="red" @click="doResign">Resign</v-btn>
        </div>
      </v-card>
    </v-dialog>

    <!-- Abort confirmation -->
    <v-dialog v-model="confirmAbort" max-width="340" persistent>
      <v-card class="pa-5" rounded="xl">
        <h3 class="text-h6 font-weight-bold mb-2">Abort Game?</h3>
        <p class="text-body-2 mb-4">This will cancel the game. Are you sure?</p>
        <div class="d-flex ga-2">
          <v-btn variant="text" @click="confirmAbort = false">Cancel</v-btn>
          <v-spacer />
          <v-btn color="red" @click="doAbort">Abort</v-btn>
        </div>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.game-controls {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  padding: 8px 0;
}
</style>
