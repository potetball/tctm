<script setup>
import { ref, computed, watch, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  tournaments,
  players,
  rounds,
  matches,
  TournamentStatus,
  MatchResult,
} from '@/api'
import RoundScoreboard from '@/components/RoundScoreboard.vue'
import LobbyPanel from '@/components/LobbyPanel.vue'
import { useTournamentHub } from '@/composables/useTournamentHub'

const route = useRoute()
const router = useRouter()
const slug = computed(() => route.params.slug)

// --- State ---
const tournament = ref(null)
const playerList = ref([])
const roundList = ref([])
const loading = ref(true)
const error = ref('')
const actionLoading = ref(false)
const actionError = ref('')
const actionSuccess = ref('')

// Override dialog
const overrideDialog = ref(false)
const overrideMatch = ref(null)
const overrideResult = ref(null)
const overrideLoading = ref(false)
const overrideError = ref('')

// Scoreboard after round completion
const scoreboardVisible = ref(false)
const scoreboardStandings = ref([])
const completedRoundNumber = ref(0)

// --- Computed ---
const adminToken = computed(() => {
  const tokens = JSON.parse(localStorage.getItem('tctm_admin_tokens') || '{}')
  return tokens[slug.value] || null
})

const isAuthorized = computed(() => !!adminToken.value)

const isLobby = computed(() => tournament.value?.status === TournamentStatus.Lobby)
const isInProgress = computed(() => tournament.value?.status === TournamentStatus.InProgress)
const isCompleted = computed(() => tournament.value?.status === TournamentStatus.Completed)

const canStart = computed(() => isLobby.value && playerList.value.length >= 2)

const currentRound = computed(() => {
  if (!roundList.value.length) return null
  return roundList.value[roundList.value.length - 1]
})

const allCurrentRoundComplete = computed(() => {
  if (!currentRound.value) return false
  return currentRound.value.matches.every(m => m.result !== null)
})

const resultOptions = [
  { title: 'White wins (1–0)', value: MatchResult.WhiteWin },
  { title: 'Draw (½–½)', value: MatchResult.Draw },
  { title: 'Black wins (0–1)', value: MatchResult.BlackWin },
]

// --- Methods ---
async function loadData() {
  loading.value = true
  error.value = ''

  try {
    const s = slug.value
    const [t, p, r] = await Promise.all([
      tournaments.getTournament(s),
      players.listPlayers(s),
      rounds.listRounds(s).catch(() => []),
    ])
    tournament.value = t
    playerList.value = p
    roundList.value = r
  } catch (err) {
    error.value = err.message || 'Failed to load tournament.'
  } finally {
    loading.value = false
  }
}

async function startTournament() {
  actionLoading.value = true
  actionError.value = ''
  actionSuccess.value = ''

  try {
    await tournaments.startTournament(slug.value, adminToken.value)
    actionSuccess.value = 'Tournament started!'
  } catch (err) {
    actionError.value = err.body?.error || err.message || 'Failed to start tournament.'
  } finally {
    actionLoading.value = false
  }
}

async function generateNextRound() {
  actionLoading.value = true
  actionError.value = ''
  actionSuccess.value = ''

  try {
    await rounds.generateNextRound(slug.value, adminToken.value)
    actionSuccess.value = 'Next round generated!'
  } catch (err) {
    actionError.value = err.body?.error || err.message || 'Failed to generate next round.'
  } finally {
    actionLoading.value = false
  }
}

async function completeCurrentRound() {
  if (!currentRound.value) return

  actionLoading.value = true
  actionError.value = ''
  actionSuccess.value = ''

  try {
    const updatedStandings = await rounds.completeRound(slug.value, currentRound.value.roundNumber, adminToken.value)
    completedRoundNumber.value = currentRound.value.roundNumber
    scoreboardStandings.value = updatedStandings
    actionSuccess.value = `Round ${currentRound.value.roundNumber} completed!`
    scoreboardVisible.value = true
  } catch (err) {
    actionError.value = err.body?.error || err.message || 'Failed to complete round.'
  } finally {
    actionLoading.value = false
  }
}

function openOverrideDialog(match) {
  overrideMatch.value = match
  overrideResult.value = match.result || null
  overrideError.value = ''
  overrideDialog.value = true
}

async function submitOverride() {
  if (!overrideResult.value || !overrideMatch.value) return

  overrideLoading.value = true
  overrideError.value = ''

  try {
    await matches.overrideResult(slug.value, overrideMatch.value.id, {
      result: overrideResult.value,
      adminToken: adminToken.value,
    })
    overrideDialog.value = false
  } catch (err) {
    overrideError.value = err.body?.error || err.message || 'Failed to override result.'
  } finally {
    overrideLoading.value = false
  }
}

function resultLabel(result) {
  if (!result) return '—'
  if (result === MatchResult.WhiteWin) return '1–0'
  if (result === MatchResult.BlackWin) return '0–1'
  if (result === MatchResult.Draw) return '½–½'
  return result
}

watch(slug, loadData, { immediate: true })

// --- SignalR real-time updates ---
const hub = useTournamentHub()
const unsubs = []

watch(slug, async (newSlug) => {
  if (newSlug) {
    await hub.joinTournament(newSlug)
  }
}, { immediate: true })

// Player joined: add to list in-place
unsubs.push(hub.on('PlayerJoined', (player) => {
  if (!playerList.value.some(p => p.id === player.id)) {
    playerList.value = [...playerList.value, player]
  }
}))

// Player removed: remove from list in-place
unsubs.push(hub.on('PlayerRemoved', (playerId) => {
  playerList.value = playerList.value.filter(p => p.id !== playerId)
}))

// Tournament started: update status
unsubs.push(hub.on('TournamentStarted', (updatedTournament) => {
  tournament.value = updatedTournament
}))

// New round created: add to round list
unsubs.push(hub.on('RoundCreated', (round) => {
  const idx = roundList.value.findIndex(r => r.id === round.id)
  if (idx >= 0) {
    roundList.value[idx] = round
  } else {
    roundList.value = [...roundList.value, round]
  }
}))

// Round completed: update round in-place
unsubs.push(hub.on('RoundCompleted', (round, _standings) => {
  const idx = roundList.value.findIndex(r => r.id === round.id)
  if (idx >= 0) {
    roundList.value[idx] = round
  }
}))

// Match result updated: update match in the relevant round
unsubs.push(hub.on('MatchUpdated', (match) => {
  for (const round of roundList.value) {
    const idx = round.matches.findIndex(m => m.id === match.id)
    if (idx >= 0) {
      round.matches[idx] = match
      break
    }
  }
}))

// Seed order updated: replace player list
unsubs.push(hub.on('SeedOrderUpdated', (updatedPlayers) => {
  playerList.value = updatedPlayers
}))

onUnmounted(() => {
  unsubs.forEach(fn => fn())
  hub.leaveTournament()
})
</script>

<template>
  <v-container style="max-width: 1200px; margin: 0 auto;">
    <v-btn
      variant="text"
      prepend-icon="mdi-arrow-left"
      class="mb-4"
      @click="router.push({ name: 'tournament', params: { slug } })"
    >
      Back to Dashboard
    </v-btn>

    <!-- Not authorized -->
    <v-alert v-if="!isAuthorized" type="warning" variant="tonal" class="my-8">
      You don't have admin access to this tournament. Only the organiser who created it can access this panel.
    </v-alert>

    <template v-else>
      <v-card class="pa-6 mb-6" elevation="4" rounded="xl">
        <div class="text-center mb-4">
          <v-icon icon="mdi-shield-crown-outline" size="48" color="amber-darken-2" />
          <h1 class="text-h4 font-weight-bold mt-2">Admin Panel</h1>
          <p v-if="tournament" class="text-body-1 text-medium-emphasis mt-1">{{ tournament.name }}</p>
        </div>

        <v-divider class="mb-4" />

        <div v-if="loading" class="text-center py-8">
          <v-progress-circular indeterminate color="amber-darken-2" />
        </div>

        <v-alert v-else-if="error" type="error" variant="tonal">
          {{ error }}
        </v-alert>

        <template v-else>
          <!-- Action feedback -->
          <v-alert v-if="actionError" type="error" variant="tonal" class="mb-4" closable @click:close="actionError = ''">
            {{ actionError }}
          </v-alert>
          <v-alert v-if="actionSuccess" type="success" variant="tonal" class="mb-4" closable @click:close="actionSuccess = ''">
            {{ actionSuccess }}
          </v-alert>

          <!-- Tournament Controls -->
          <div class="d-flex flex-wrap ga-3 mb-6">
            <v-btn
              v-if="isLobby"
              color="green"
              :loading="actionLoading"
              :disabled="!canStart"
              prepend-icon="mdi-play"
              @click="startTournament"
            >
              Start Tournament
              <v-tooltip v-if="!canStart" activator="parent" location="bottom">
                Need at least 2 players to start
              </v-tooltip>
            </v-btn>

            <v-btn
              v-if="isInProgress && currentRound && currentRound.status !== 'Completed'"
              color="green"
              :loading="actionLoading"
              :disabled="!allCurrentRoundComplete"
              prepend-icon="mdi-check-circle"
              @click="completeCurrentRound"
            >
              Complete Round {{ currentRound?.roundNumber }}
              <v-tooltip v-if="!allCurrentRoundComplete" activator="parent" location="bottom">
                All match results must be reported first
              </v-tooltip>
            </v-btn>

            <v-btn
              v-if="isInProgress && (!currentRound || currentRound.status === 'Completed')"
              color="amber-darken-2"
              :loading="actionLoading"
              prepend-icon="mdi-skip-next"
              @click="generateNextRound"
            >
              Generate Next Round
            </v-btn>

            <v-chip v-if="tournament" :color="isLobby ? 'blue' : isInProgress ? 'green' : 'grey'" variant="flat">
              Status: {{ tournament.status }}
            </v-chip>
          </div>

          <!-- Player Management (lobby only) -->
          <LobbyPanel
            v-if="isLobby"
            :player-list="playerList"
            :slug="slug"
            :admin-token="adminToken"
            @player-removed="() => {}"
          />

          <!-- Rounds & Match Overrides -->
          <template v-if="roundList.length">
            <h3 class="text-h6 font-weight-bold mb-3">
              <v-icon icon="mdi-sword-cross" class="mr-1" />
              Rounds
            </h3>

            <v-expansion-panels variant="accordion">
              <v-expansion-panel v-for="round in roundList" :key="round.id">
                <v-expansion-panel-title>
                  <div class="d-flex align-center ga-2">
                    <strong>Round {{ round.roundNumber }}</strong>
                    <v-chip :color="round.status === 'Completed' ? 'grey' : 'green'" size="x-small" variant="flat">
                      {{ round.status }}
                    </v-chip>
                  </div>
                </v-expansion-panel-title>
                <v-expansion-panel-text>
                  <v-table density="compact">
                    <thead>
                      <tr>
                        <th>White</th>
                        <th class="text-center">Result</th>
                        <th class="text-right">Black</th>
                        <th class="text-center">Override</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr v-for="match in round.matches" :key="match.id">
                        <td>
                          <span :class="{ 'font-weight-bold': match.result === 'WhiteWin' }">
                            {{ match.whitePlayerName || 'BYE' }}
                          </span>
                        </td>
                        <td class="text-center">
                          <v-chip
                            v-if="match.result"
                            size="x-small"
                            :color="match.disputed ? 'red' : 'default'"
                            :variant="match.disputed ? 'flat' : 'outlined'"
                          >
                            {{ resultLabel(match.result) }}
                            <v-icon v-if="match.disputed" icon="mdi-alert" size="x-small" class="ml-1" />
                          </v-chip>
                          <span v-else class="text-medium-emphasis">—</span>
                        </td>
                        <td class="text-right">
                          <span :class="{ 'font-weight-bold': match.result === 'BlackWin' }">
                            {{ match.blackPlayerName || 'BYE' }}
                          </span>
                        </td>
                        <td class="text-center">
                          <v-btn
                            size="x-small"
                            variant="tonal"
                            color="amber-darken-2"
                            icon="mdi-pencil"
                            @click="openOverrideDialog(match)"
                          />
                        </td>
                      </tr>
                    </tbody>
                  </v-table>
                </v-expansion-panel-text>
              </v-expansion-panel>
            </v-expansion-panels>
          </template>
        </template>
      </v-card>
    </template>

    <!-- Override Result Dialog -->
    <v-dialog v-model="overrideDialog" max-width="420" persistent>
      <v-card class="pa-6" rounded="xl">
        <h3 class="text-h6 font-weight-bold mb-2">Override Result</h3>
        <p v-if="overrideMatch" class="text-body-2 text-medium-emphasis mb-4">
          {{ overrideMatch.whitePlayerName || 'BYE' }} vs {{ overrideMatch.blackPlayerName || 'BYE' }}
        </p>
        <v-alert v-if="overrideError" type="error" variant="tonal" class="mb-4" density="compact">
          {{ overrideError }}
        </v-alert>
        <v-radio-group v-model="overrideResult" class="mb-2">
          <v-radio
            v-for="opt in resultOptions"
            :key="opt.value"
            :label="opt.title"
            :value="opt.value"
          />
        </v-radio-group>
        <div class="d-flex ga-2 mt-2">
          <v-btn variant="text" @click="overrideDialog = false">Cancel</v-btn>
          <v-spacer />
          <v-btn
            color="amber-darken-2"
            :loading="overrideLoading"
            :disabled="!overrideResult"
            @click="submitOverride"
          >
            Override
          </v-btn>
        </div>
      </v-card>
    </v-dialog>

    <!-- Round Scoreboard -->
    <RoundScoreboard
      v-model="scoreboardVisible"
      :standings="scoreboardStandings"
      :round-number="completedRoundNumber"
    />

  </v-container>
</template>
