<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { tournaments } from '@/api'
import { useTournamentStore } from '@/composables/useTournamentStore'
import logo from '@/assets/tctm-logo.png'

const router = useRouter()
const { tournaments: knownTournaments, addTournament, removeTournament } = useTournamentStore()
const inviteCode = ref('')
const loading = ref(false)
const error = ref('')

// Re-auth state
const playerToken = ref('')
const reauthLoading = ref(false)
const reauthError = ref('')

async function joinTournament() {
  if (!inviteCode.value.trim()) return

  loading.value = true
  error.value = ''

  try {
    const tournament = await tournaments.getTournamentByInviteCode(inviteCode.value.trim())
    router.push({ name: 'tournament', params: { slug: tournament.slug } })
  } catch (err) {
    error.value = err.status === 404
      ? 'No tournament found with that invite code.'
      : (err.message || 'Failed to look up tournament.')
  } finally {
    loading.value = false
  }
}

async function reauthenticate() {
  if (!playerToken.value.trim()) return

  reauthLoading.value = true
  reauthError.value = ''

  try {
    const result = await tournaments.reauthenticate(playerToken.value.trim())

    // Store player credentials in localStorage
    const playerData = JSON.parse(localStorage.getItem('tctm_players') || '{}')
    playerData[result.slug] = {
      playerId: result.playerId,
      playerToken: playerToken.value.trim(),
      displayName: result.displayName,
    }
    localStorage.setItem('tctm_players', JSON.stringify(playerData))

    // Register in tournament store (name will be updated when dashboard loads)
    addTournament(result.slug, result.slug, 'player')

    // Navigate to the tournament
    router.push({ name: 'tournament', params: { slug: result.slug } })
  } catch (err) {
    reauthError.value = err.status === 401
      ? 'Invalid player token.'
      : (err.message || 'Failed to authenticate.')
  } finally {
    reauthLoading.value = false
  }
}
</script>

<template>
  <v-container class="d-flex flex-column align-center justify-center" style="max-width: 1200px; margin: 0 auto; min-height: 80vh;">
    <!-- My Tournaments quick-access -->
    <v-card
      v-if="knownTournaments.length"
      max-width="520"
      width="100%"
      class="pa-4 mb-4"
      elevation="4"
      rounded="xl"
    >
      <h3 class="text-subtitle-1 font-weight-bold mb-2">
        <v-icon icon="mdi-trophy-outline" class="mr-1" />
        My Tournaments
      </h3>
      <v-list density="compact" nav>
        <v-list-item
          v-for="t in knownTournaments"
          :key="t.slug"
          @click="router.push({ name: 'tournament', params: { slug: t.slug } })"
        >
          <template #prepend>
            <v-icon
              :icon="t.role === 'admin' ? 'mdi-shield-crown-outline' : t.role === 'player' ? 'mdi-account-check' : 'mdi-eye-outline'"
              size="small"
              class="mr-2"
            />
          </template>
          <v-list-item-title>{{ t.name }}</v-list-item-title>
          <v-list-item-subtitle class="text-caption">
            {{ t.role }} · {{ t.slug }}
          </v-list-item-subtitle>
          <template #append>
            <v-btn
              icon="mdi-close"
              variant="text"
              size="x-small"
              color="red-lighten-1"
              title="Remove from list"
              @click.stop="removeTournament(t.slug)"
            />
          </template>
        </v-list-item>
      </v-list>
    </v-card>

    <v-card max-width="520" width="100%" class="pa-6" elevation="8" rounded="xl">
      <div class="text-center mb-6">
        <img :src="logo" alt="TCTM Logo" height="164" />
        <h1 class="text-h4 font-weight-bold mt-4">Welcome to TCTM</h1>
        <p class="text-body-1 text-medium-emphasis mt-2">
          Organise and manage small chess tournaments with ease.
          Create a new tournament or join an existing one.
        </p>
      </div>

      <v-divider class="mb-6" />

      <v-alert v-if="error" type="error" variant="tonal" class="mb-4" closable @click:close="error = ''">
        {{ error }}
      </v-alert>

      <div class="d-flex flex-column ga-4">
        <v-btn
          color="amber-darken-2"
          size="x-large"
          block
          rounded="lg"
          prepend-icon="mdi-plus-circle-outline"
          @click="router.push({ name: 'create-tournament' })"
        >
          Create Tournament
        </v-btn>

        <div class="text-center text-body-2 text-medium-emphasis">
          — or join with an invite code —
        </div>

        <v-text-field
          v-model="inviteCode"
          label="Invite Code"
          placeholder="e.g. ABC123"
          variant="outlined"
          density="comfortable"
          prepend-inner-icon="mdi-ticket-confirmation-outline"
          maxlength="6"
          hide-details
          @keyup.enter="joinTournament"
        />

        <v-btn
          color="primary"
          size="large"
          block
          rounded="lg"
          variant="tonal"
          prepend-icon="mdi-login"
          :disabled="inviteCode.length === 0"
          :loading="loading"
          @click="joinTournament"
        >
          Join Tournament
        </v-btn>

        <v-divider class="my-2" />

        <div class="text-center text-body-2 text-medium-emphasis">
          — or re-authenticate with your player token —
        </div>

        <v-alert v-if="reauthError" type="error" variant="tonal" class="mb-0" density="compact" closable @click:close="reauthError = ''">
          {{ reauthError }}
        </v-alert>

        <v-text-field
          v-model="playerToken"
          label="Player Token"
          placeholder="Paste your token here"
          variant="outlined"
          density="comfortable"
          prepend-inner-icon="mdi-key"
          hide-details
          @keyup.enter="reauthenticate"
        />

        <v-btn
          color="primary"
          size="large"
          block
          rounded="lg"
          variant="tonal"
          prepend-icon="mdi-account-key"
          :disabled="playerToken.length === 0"
          :loading="reauthLoading"
          @click="reauthenticate"
        >
          Authenticate
        </v-btn>
      </div>

      <div class="text-center mt-8">
        <p class="text-caption text-disabled">
          No account required — just pick a name and play.
        </p>
      </div>
    </v-card>
  </v-container>
</template>
