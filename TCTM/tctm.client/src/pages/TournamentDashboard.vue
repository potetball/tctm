<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  tournaments,
  players,
  rounds,
  matches,
  TournamentStatus,
  TournamentFormat,
  MatchResult,
} from '@/api'

const route = useRoute()
const router = useRouter()
const slug = route.params.slug

// --- State ---
const tournament = ref(null)
const playerList = ref([])
const roundList = ref([])
const loading = ref(true)
const error = ref('')

// Join dialog state
const joinDialog = ref(false)
const displayName = ref('')
const joinLoading = ref(false)
const joinError = ref('')

// Result reporting dialog
const resultDialog = ref(false)
const selectedMatch = ref(null)
const selectedResult = ref(null)
const reportLoading = ref(false)
const reportError = ref('')

// --- Computed ---
const isAdmin = computed(() => {
  const tokens = JSON.parse(localStorage.getItem('tctm_admin_tokens') || '{}')
  return !!tokens[slug]
})

const adminToken = computed(() => {
  const tokens = JSON.parse(localStorage.getItem('tctm_admin_tokens') || '{}')
  return tokens[slug] || null
})

const myPlayerId = computed(() => {
  const playerData = JSON.parse(localStorage.getItem('tctm_players') || '{}')
  return playerData[slug]?.playerId || null
})

const myPlayerToken = computed(() => {
  const playerData = JSON.parse(localStorage.getItem('tctm_players') || '{}')
  return playerData[slug]?.playerToken || null
})

const isJoined = computed(() => !!myPlayerId.value)

const isLobby = computed(() => tournament.value?.status === TournamentStatus.Lobby)
const isInProgress = computed(() => tournament.value?.status === TournamentStatus.InProgress)
const isCompleted = computed(() => tournament.value?.status === TournamentStatus.Completed)

const isElimination = computed(() =>
  tournament.value?.format === TournamentFormat.SingleElimination ||
  tournament.value?.format === TournamentFormat.DoubleElimination
)

const currentRound = computed(() => {
  if (!roundList.value.length) return null
  // The most recent non-completed round, or the last round
  const active = roundList.value.find(r => r.status !== 'Completed')
  return active || roundList.value[roundList.value.length - 1]
})

const statusColor = computed(() => {
  if (isLobby.value) return 'blue'
  if (isInProgress.value) return 'green'
  return 'grey'
})

const statusIcon = computed(() => {
  if (isLobby.value) return 'mdi-account-group'
  if (isInProgress.value) return 'mdi-play-circle'
  return 'mdi-flag-checkered'
})

const formatLabel = computed(() => {
  const mapping = {
    RoundRobin: 'Round Robin',
    Swiss: 'Swiss System',
    SingleElimination: 'Single Elimination',
    DoubleElimination: 'Double Elimination',
  }
  return mapping[tournament.value?.format] || tournament.value?.format
})

const timeControlLabel = computed(() => {
  if (!tournament.value) return ''
  return `${tournament.value.timeControlPreset} · ${tournament.value.timeControlMinutes} min`
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
    const [t, p, r] = await Promise.all([
      tournaments.getTournament(slug),
      players.listPlayers(slug),
      rounds.listRounds(slug).catch(() => []),
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

async function joinTournament() {
  if (!displayName.value.trim()) return

  joinLoading.value = true
  joinError.value = ''

  try {
    const result = await tournaments.joinTournament(slug, {
      inviteCode: tournament.value.inviteCode,
      displayName: displayName.value.trim(),
    })

    // Store player credentials
    const playerData = JSON.parse(localStorage.getItem('tctm_players') || '{}')
    playerData[slug] = {
      playerId: result.playerId,
      playerToken: result.playerToken,
      displayName: displayName.value.trim(),
    }
    localStorage.setItem('tctm_players', JSON.stringify(playerData))

    joinDialog.value = false
    displayName.value = ''
    await loadData()
  } catch (err) {
    joinError.value = err.body?.error || err.message || 'Failed to join tournament.'
  } finally {
    joinLoading.value = false
  }
}

function openResultDialog(match) {
  selectedMatch.value = match
  selectedResult.value = null
  reportError.value = ''
  resultDialog.value = true
}

async function submitResult() {
  if (!selectedResult.value || !selectedMatch.value) return

  reportLoading.value = true
  reportError.value = ''

  try {
    const token = isAdmin.value ? adminToken.value : myPlayerToken.value
    if (isAdmin.value) {
      await matches.overrideResult(slug, selectedMatch.value.id, {
        result: selectedResult.value,
        adminToken: token,
      })
    } else {
      await matches.reportResult(slug, selectedMatch.value.id, {
        result: selectedResult.value,
        token,
      })
    }
    resultDialog.value = false
    await loadData()
  } catch (err) {
    reportError.value = err.body?.error || err.message || 'Failed to report result.'
  } finally {
    reportLoading.value = false
  }
}

function resultLabel(result) {
  if (!result) return '—'
  if (result === MatchResult.WhiteWin) return '1–0'
  if (result === MatchResult.BlackWin) return '0–1'
  if (result === MatchResult.Draw) return '½–½'
  return result
}

function canReport(match) {
  if (match.result && !isAdmin.value) return false
  if (isAdmin.value) return true
  if (!myPlayerId.value) return false
  return match.whitePlayerId === myPlayerId.value || match.blackPlayerId === myPlayerId.value
}

function copyInviteLink() {
  const url = `${window.location.origin}/t/${slug}`
  navigator.clipboard.writeText(url)
}

onMounted(loadData)
</script>

<template>
  <v-container style="max-width: 1200px; margin: 0 auto;">
    <!-- Loading / Error -->
    <div v-if="loading" class="text-center py-16">
      <v-progress-circular indeterminate color="amber-darken-2" size="64" />
    </div>

    <v-alert v-else-if="error" type="error" variant="tonal" class="my-8">
      {{ error }}
    </v-alert>

    <template v-else-if="tournament">
      <!-- Header Card -->
      <v-card class="pa-6 mb-6" elevation="4" rounded="xl">
        <div class="d-flex align-center justify-space-between flex-wrap ga-4">
          <div>
            <div class="d-flex align-center ga-2 mb-2">
              <v-icon icon="mdi-trophy" size="32" color="amber-darken-2" />
              <h1 class="text-h4 font-weight-bold">{{ tournament.name }}</h1>
            </div>
            <div class="d-flex align-center ga-3 flex-wrap">
              <v-chip :color="statusColor" variant="flat" size="small" :prepend-icon="statusIcon">
                {{ tournament.status }}
              </v-chip>
              <v-chip variant="outlined" size="small" prepend-icon="mdi-format-list-bulleted">
                {{ formatLabel }}
              </v-chip>
              <v-chip variant="outlined" size="small" prepend-icon="mdi-timer-outline">
                {{ timeControlLabel }}
              </v-chip>
              <v-chip variant="outlined" size="small" prepend-icon="mdi-account-group">
                {{ playerList.length }} player{{ playerList.length !== 1 ? 's' : '' }}
              </v-chip>
            </div>
          </div>

          <div class="d-flex ga-2 flex-wrap">
            <v-btn
              v-if="isInProgress || isCompleted"
              variant="tonal"
              color="amber-darken-2"
              prepend-icon="mdi-podium"
              @click="router.push({ name: 'standings', params: { slug } })"
            >
              Standings
            </v-btn>
            <v-btn
              v-if="isElimination && (isInProgress || isCompleted)"
              variant="tonal"
              color="amber-darken-2"
              prepend-icon="mdi-tournament"
              @click="router.push({ name: 'bracket', params: { slug } })"
            >
              Bracket
            </v-btn>
            <v-btn
              v-if="isAdmin"
              variant="tonal"
              color="grey"
              prepend-icon="mdi-shield-crown-outline"
              @click="router.push({ name: 'admin', params: { slug } })"
            >
              Admin
            </v-btn>
          </div>
        </div>
      </v-card>

      <!-- Lobby: Invite & Join -->
      <v-row v-if="isLobby" class="mb-6">
        <v-col cols="12" md="6">
          <v-card class="pa-5 h-100" variant="outlined" rounded="xl">
            <h3 class="text-h6 font-weight-bold mb-3">
              <v-icon icon="mdi-ticket-confirmation-outline" class="mr-1" />
              Invite Players
            </h3>
            <div class="d-flex align-center ga-2 mb-3">
              <v-text-field
                :model-value="tournament.inviteCode"
                label="Invite Code"
                variant="outlined"
                density="compact"
                readonly
                hide-details
              />
              <v-btn icon="mdi-content-copy" variant="text" size="small" @click="copyInviteLink" />
            </div>
            <p class="text-body-2 text-medium-emphasis">
              Share this code or the tournament link with your players.
            </p>
          </v-card>
        </v-col>

        <v-col cols="12" md="6">
          <v-card class="pa-5 h-100" variant="outlined" rounded="xl">
            <h3 class="text-h6 font-weight-bold mb-3">
              <v-icon icon="mdi-account-plus" class="mr-1" />
              Join Tournament
            </h3>
            <template v-if="isJoined">
              <v-alert type="success" variant="tonal" density="compact">
                You've already joined this tournament.
              </v-alert>
            </template>
            <template v-else>
              <p class="text-body-2 text-medium-emphasis mb-3">
                Pick a display name to join as a player.
              </p>
              <v-btn
                color="amber-darken-2"
                block
                rounded="lg"
                prepend-icon="mdi-login"
                @click="joinDialog = true"
              >
                Join
              </v-btn>
            </template>
          </v-card>
        </v-col>
      </v-row>

      <!-- Player List -->
      <v-card class="pa-5 mb-6" variant="outlined" rounded="xl">
        <h3 class="text-h6 font-weight-bold mb-3">
          <v-icon icon="mdi-account-group" class="mr-1" />
          Players
        </h3>
        <v-list v-if="playerList.length" density="compact">
          <v-list-item
            v-for="player in playerList"
            :key="player.id"
            :title="player.displayName"
            :prepend-icon="player.id === myPlayerId ? 'mdi-account-check' : 'mdi-account'"
          >
            <template #append>
              <v-chip v-if="player.seed" size="x-small" variant="outlined">
                Seed #{{ player.seed }}
              </v-chip>
            </template>
          </v-list-item>
        </v-list>
        <p v-else class="text-body-2 text-medium-emphasis">
          No players have joined yet.
        </p>
      </v-card>

      <!-- Rounds & Matches -->
      <template v-if="roundList.length">
        <v-card v-for="round in roundList" :key="round.id" class="pa-5 mb-4" variant="outlined" rounded="xl">
          <div class="d-flex align-center justify-space-between mb-3">
            <h3 class="text-h6 font-weight-bold">
              <v-icon icon="mdi-sword-cross" class="mr-1" />
              Round {{ round.roundNumber }}
            </h3>
            <v-chip :color="round.status === 'Completed' ? 'grey' : 'green'" size="small" variant="flat">
              {{ round.status }}
            </v-chip>
          </div>

          <v-table density="comfortable">
            <thead>
              <tr>
                <th>White</th>
                <th class="text-center">Result</th>
                <th class="text-right">Black</th>
                <th v-if="round.matches.some(m => m.bracket)" class="text-center">Bracket</th>
                <th class="text-center">Actions</th>
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
                    size="small"
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
                <td v-if="round.matches.some(m => m.bracket)" class="text-center">
                  <v-chip v-if="match.bracket" size="x-small" variant="outlined">
                    {{ match.bracket }}
                  </v-chip>
                </td>
                <td class="text-center">
                  <v-btn
                    v-if="canReport(match)"
                    size="small"
                    variant="tonal"
                    color="amber-darken-2"
                    @click="openResultDialog(match)"
                  >
                    {{ match.result ? 'Edit' : 'Report' }}
                  </v-btn>
                </td>
              </tr>
            </tbody>
          </v-table>
        </v-card>
      </template>

      <v-card v-else-if="isInProgress" class="pa-5 mb-6" variant="outlined" rounded="xl">
        <p class="text-body-1 text-medium-emphasis text-center">
          No rounds generated yet. The organiser needs to advance rounds from the admin panel.
        </p>
      </v-card>

      <!-- Join Dialog -->
      <v-dialog v-model="joinDialog" max-width="400" persistent>
        <v-card class="pa-6" rounded="xl">
          <h3 class="text-h6 font-weight-bold mb-4">Join Tournament</h3>
          <v-alert v-if="joinError" type="error" variant="tonal" class="mb-4" density="compact">
            {{ joinError }}
          </v-alert>
          <v-text-field
            v-model="displayName"
            label="Display Name"
            placeholder="Your name"
            variant="outlined"
            density="comfortable"
            prepend-inner-icon="mdi-account"
            maxlength="30"
            counter
            autofocus
            @keyup.enter="joinTournament"
          />
          <div class="d-flex ga-2 mt-4">
            <v-btn
              variant="text"
              @click="joinDialog = false; joinError = ''"
            >
              Cancel
            </v-btn>
            <v-spacer />
            <v-btn
              color="amber-darken-2"
              :loading="joinLoading"
              :disabled="!displayName.trim()"
              @click="joinTournament"
            >
              Join
            </v-btn>
          </div>
        </v-card>
      </v-dialog>

      <!-- Report Result Dialog -->
      <v-dialog v-model="resultDialog" max-width="420" persistent>
        <v-card class="pa-6" rounded="xl">
          <h3 class="text-h6 font-weight-bold mb-2">Report Result</h3>
          <p v-if="selectedMatch" class="text-body-2 text-medium-emphasis mb-4">
            {{ selectedMatch.whitePlayerName || 'BYE' }} vs {{ selectedMatch.blackPlayerName || 'BYE' }}
          </p>
          <v-alert v-if="reportError" type="error" variant="tonal" class="mb-4" density="compact">
            {{ reportError }}
          </v-alert>
          <v-radio-group v-model="selectedResult" class="mb-2">
            <v-radio
              v-for="opt in resultOptions"
              :key="opt.value"
              :label="opt.title"
              :value="opt.value"
            />
          </v-radio-group>
          <div class="d-flex ga-2 mt-2">
            <v-btn
              variant="text"
              @click="resultDialog = false"
            >
              Cancel
            </v-btn>
            <v-spacer />
            <v-btn
              color="amber-darken-2"
              :loading="reportLoading"
              :disabled="!selectedResult"
              @click="submitResult"
            >
              Submit
            </v-btn>
          </div>
        </v-card>
      </v-dialog>
    </template>
  </v-container>
</template>
