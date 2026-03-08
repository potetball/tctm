<script setup>
import { ref } from 'vue'
import { players } from '@/api'

const props = defineProps({
  playerList: { type: Array, required: true },
  slug: { type: String, required: true },
  adminToken: { type: String, required: true },
})

const emit = defineEmits(['player-removed'])

// Remove player dialog
const removeDialog = ref(false)
const removePlayer = ref(null)
const removeLoading = ref(false)

// Reset token dialog
const resetTokenDialog = ref(false)
const resetTokenPlayer = ref(null)
const resetTokenLoading = ref(false)
const resetTokenError = ref('')

// Token result dialog (shown after successful reset)
const tokenResultDialog = ref(false)
const tokenResultPlayerName = ref('')
const tokenResultValue = ref('')
const tokenCopied = ref(false)

function confirmRemovePlayer(player) {
  removePlayer.value = player
  removeDialog.value = true
}

async function doRemovePlayer() {
  if (!removePlayer.value) return

  removeLoading.value = true
  try {
    await players.removePlayer(props.slug, removePlayer.value.id, props.adminToken)
    removeDialog.value = false
    emit('player-removed')
  } catch (err) {
    // Let the parent handle top-level errors if needed
    console.error('Failed to remove player:', err)
  } finally {
    removeLoading.value = false
  }
}

function confirmResetToken(player) {
  resetTokenPlayer.value = player
  resetTokenError.value = ''
  resetTokenDialog.value = true
}

async function doResetToken() {
  if (!resetTokenPlayer.value) return

  resetTokenLoading.value = true
  resetTokenError.value = ''

  try {
    const result = await players.resetPlayerToken(
      props.slug,
      resetTokenPlayer.value.id,
      props.adminToken,
    )
    resetTokenDialog.value = false
    tokenResultPlayerName.value = result.displayName
    tokenResultValue.value = result.playerToken
    tokenCopied.value = false
    tokenResultDialog.value = true
  } catch (err) {
    resetTokenError.value = err.body?.error || err.message || 'Failed to reset token.'
  } finally {
    resetTokenLoading.value = false
  }
}

function copyToken() {
  navigator.clipboard.writeText(tokenResultValue.value)
  tokenCopied.value = true
}
</script>

<template>
  <div>
    <h3 class="text-h6 font-weight-bold mb-3">
      <v-icon icon="mdi-account-group" class="mr-1" />
      Players ({{ playerList.length }})
    </h3>

    <v-list v-if="playerList.length" density="compact" class="mb-6">
      <v-list-item
        v-for="player in playerList"
        :key="player.id"
        :title="player.displayName"
        prepend-icon="mdi-account"
      >
        <template #append>
          <v-chip v-if="player.seed" size="x-small" variant="outlined" class="mr-2">
            Seed #{{ player.seed }}
          </v-chip>
          <v-tooltip text="Reset player token" location="top">
            <template #activator="{ props: tooltipProps }">
              <v-btn
                v-bind="tooltipProps"
                icon="mdi-key-chain"
                size="x-small"
                variant="text"
                color="amber-darken-2"
                class="mr-1"
                @click="confirmResetToken(player)"
              />
            </template>
          </v-tooltip>
          <v-tooltip text="Remove player" location="top">
            <template #activator="{ props: tooltipProps }">
              <v-btn
                v-bind="tooltipProps"
                icon="mdi-close"
                size="x-small"
                variant="text"
                color="red"
                @click="confirmRemovePlayer(player)"
              />
            </template>
          </v-tooltip>
        </template>
      </v-list-item>
    </v-list>
    <p v-else class="text-body-2 text-medium-emphasis mb-6">No players yet.</p>

    <!-- Remove Player Dialog -->
    <v-dialog v-model="removeDialog" max-width="380" persistent>
      <v-card class="pa-6" rounded="xl">
        <h3 class="text-h6 font-weight-bold mb-2">Remove Player</h3>
        <p class="text-body-2 mb-4">
          Are you sure you want to remove <strong>{{ removePlayer?.displayName }}</strong>?
        </p>
        <div class="d-flex ga-2">
          <v-btn variant="text" @click="removeDialog = false">Cancel</v-btn>
          <v-spacer />
          <v-btn color="red" :loading="removeLoading" @click="doRemovePlayer">Remove</v-btn>
        </div>
      </v-card>
    </v-dialog>

    <!-- Reset Token Confirmation Dialog -->
    <v-dialog v-model="resetTokenDialog" max-width="420" persistent>
      <v-card class="pa-6" rounded="xl">
        <h3 class="text-h6 font-weight-bold mb-2">Reset Player Token</h3>
        <p class="text-body-2 mb-4">
          This will generate a new token for <strong>{{ resetTokenPlayer?.displayName }}</strong>.
          Their old token will no longer work.
        </p>
        <v-alert v-if="resetTokenError" type="error" variant="tonal" class="mb-4" density="compact">
          {{ resetTokenError }}
        </v-alert>
        <div class="d-flex ga-2">
          <v-btn variant="text" @click="resetTokenDialog = false">Cancel</v-btn>
          <v-spacer />
          <v-btn color="amber-darken-2" :loading="resetTokenLoading" @click="doResetToken">
            Reset Token
          </v-btn>
        </div>
      </v-card>
    </v-dialog>

    <!-- Token Result Dialog -->
    <v-dialog v-model="tokenResultDialog" max-width="460" persistent>
      <v-card class="pa-6" rounded="xl">
        <div class="text-center mb-4">
          <v-icon icon="mdi-key" size="48" color="amber-darken-2" />
          <h3 class="text-h6 font-weight-bold mt-2">New Token for {{ tokenResultPlayerName }}</h3>
          <p class="text-body-2 text-medium-emphasis mt-1">
            Copy this token and send it to the player. It won't be shown again.
          </p>
        </div>
        <v-text-field
          :model-value="tokenResultValue"
          label="Player Token"
          variant="outlined"
          density="comfortable"
          readonly
          prepend-inner-icon="mdi-key"
          append-inner-icon="mdi-content-copy"
          @click:append-inner="copyToken"
        />
        <v-alert v-if="tokenCopied" type="success" variant="tonal" density="compact" class="mb-3">
          Copied to clipboard!
        </v-alert>
        <v-btn
          color="amber-darken-2"
          block
          rounded="lg"
          class="mt-2"
          @click="tokenResultDialog = false"
        >
          Done
        </v-btn>
      </v-card>
    </v-dialog>
  </div>
</template>
