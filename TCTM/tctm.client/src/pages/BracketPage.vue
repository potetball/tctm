<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { tournaments, rounds, TournamentFormat, MatchResult } from '@/api'

const route = useRoute()
const router = useRouter()
const slug = route.params.slug

const tournament = ref(null)
const roundList = ref([])
const loading = ref(true)
const error = ref('')

const isDoubleElim = computed(() =>
  tournament.value?.format === TournamentFormat.DoubleElimination
)

const winnersRounds = computed(() => {
  if (!isDoubleElim.value) return roundList.value
  return roundList.value.map(r => ({
    ...r,
    matches: r.matches.filter(m => m.bracket !== 'Losers'),
  })).filter(r => r.matches.length)
})

const losersRounds = computed(() => {
  if (!isDoubleElim.value) return []
  return roundList.value.map(r => ({
    ...r,
    matches: r.matches.filter(m => m.bracket === 'Losers'),
  })).filter(r => r.matches.length)
})

function resultLabel(result) {
  if (!result) return '—'
  if (result === MatchResult.WhiteWin) return '1–0'
  if (result === MatchResult.BlackWin) return '0–1'
  if (result === MatchResult.Draw) return '½–½'
  return result
}

function matchColor(match) {
  if (!match.result) return ''
  if (match.disputed) return 'red-lighten-5'
  return 'green-lighten-5'
}

async function loadData() {
  loading.value = true
  error.value = ''

  try {
    const [t, r] = await Promise.all([
      tournaments.getTournament(slug),
      rounds.listRounds(slug).catch(() => []),
    ])
    tournament.value = t
    roundList.value = r

    // Redirect if not elimination format
    if (
      t.format !== TournamentFormat.SingleElimination &&
      t.format !== TournamentFormat.DoubleElimination
    ) {
      router.replace({ name: 'tournament', params: { slug } })
    }
  } catch (err) {
    error.value = err.message || 'Failed to load bracket.'
  } finally {
    loading.value = false
  }
}

onMounted(loadData)
</script>

<template>
  <v-container fluid style="max-width: 1200px; margin: 0 auto;">
    <v-btn
      variant="text"
      prepend-icon="mdi-arrow-left"
      class="mb-4"
      @click="router.push({ name: 'tournament', params: { slug } })"
    >
      Back to Dashboard
    </v-btn>

    <v-card class="pa-6" elevation="4" rounded="xl">
      <div class="text-center mb-4">
        <v-icon icon="mdi-tournament" size="48" color="amber-darken-2" />
        <h1 class="text-h4 font-weight-bold mt-2">Bracket</h1>
        <p v-if="tournament" class="text-body-1 text-medium-emphasis mt-1">{{ tournament.name }}</p>
      </div>

      <v-divider class="mb-4" />

      <div v-if="loading" class="text-center py-8">
        <v-progress-circular indeterminate color="amber-darken-2" />
      </div>

      <v-alert v-else-if="error" type="error" variant="tonal">
        {{ error }}
      </v-alert>

      <template v-else-if="roundList.length">
        <!-- Winners Bracket (or main bracket for single elim) -->
        <h3 v-if="isDoubleElim" class="text-h6 font-weight-bold mb-3">
          <v-icon icon="mdi-trophy" class="mr-1" color="amber-darken-2" />
          Winners Bracket
        </h3>

        <div class="bracket-scroll mb-6">
          <div class="d-flex ga-6 align-start">
            <div v-for="round in winnersRounds" :key="round.id" class="bracket-round">
              <h4 class="text-subtitle-2 font-weight-bold text-center mb-2">
                Round {{ round.roundNumber }}
              </h4>
              <div class="d-flex flex-column ga-3">
                <v-card
                  v-for="match in round.matches"
                  :key="match.id"
                  variant="outlined"
                  rounded="lg"
                  :color="matchColor(match)"
                  class="bracket-match"
                >
                  <div class="pa-3">
                    <div class="d-flex justify-space-between align-center mb-1">
                      <span
                        class="text-body-2"
                        :class="{ 'font-weight-bold': match.result === 'WhiteWin' }"
                      >
                        {{ match.whitePlayerName || 'BYE' }}
                      </span>
                      <span v-if="match.result === 'WhiteWin'" class="text-caption font-weight-bold">✓</span>
                    </div>
                    <v-divider />
                    <div class="d-flex justify-space-between align-center mt-1">
                      <span
                        class="text-body-2"
                        :class="{ 'font-weight-bold': match.result === 'BlackWin' }"
                      >
                        {{ match.blackPlayerName || 'BYE' }}
                      </span>
                      <span v-if="match.result === 'BlackWin'" class="text-caption font-weight-bold">✓</span>
                    </div>
                    <div v-if="match.result" class="text-center mt-2">
                      <v-chip size="x-small" :color="match.disputed ? 'red' : 'default'" variant="flat">
                        {{ resultLabel(match.result) }}
                      </v-chip>
                    </div>
                  </div>
                </v-card>
              </div>
            </div>
          </div>
        </div>

        <!-- Losers Bracket (double elim only) -->
        <template v-if="isDoubleElim && losersRounds.length">
          <v-divider class="my-6" />
          <h3 class="text-h6 font-weight-bold mb-3">
            <v-icon icon="mdi-arrow-down-bold" class="mr-1" color="grey" />
            Losers Bracket
          </h3>

          <div class="bracket-scroll">
            <div class="d-flex ga-6 align-start">
              <div v-for="round in losersRounds" :key="round.id" class="bracket-round">
                <h4 class="text-subtitle-2 font-weight-bold text-center mb-2">
                  Round {{ round.roundNumber }}
                </h4>
                <div class="d-flex flex-column ga-3">
                  <v-card
                    v-for="match in round.matches"
                    :key="match.id"
                    variant="outlined"
                    rounded="lg"
                    :color="matchColor(match)"
                    class="bracket-match"
                  >
                    <div class="pa-3">
                      <div class="d-flex justify-space-between align-center mb-1">
                        <span
                          class="text-body-2"
                          :class="{ 'font-weight-bold': match.result === 'WhiteWin' }"
                        >
                          {{ match.whitePlayerName || 'BYE' }}
                        </span>
                        <span v-if="match.result === 'WhiteWin'" class="text-caption font-weight-bold">✓</span>
                      </div>
                      <v-divider />
                      <div class="d-flex justify-space-between align-center mt-1">
                        <span
                          class="text-body-2"
                          :class="{ 'font-weight-bold': match.result === 'BlackWin' }"
                        >
                          {{ match.blackPlayerName || 'BYE' }}
                        </span>
                        <span v-if="match.result === 'BlackWin'" class="text-caption font-weight-bold">✓</span>
                      </div>
                      <div v-if="match.result" class="text-center mt-2">
                        <v-chip size="x-small" :color="match.disputed ? 'red' : 'default'" variant="flat">
                          {{ resultLabel(match.result) }}
                        </v-chip>
                      </div>
                    </div>
                  </v-card>
                </div>
              </div>
            </div>
          </div>
        </template>
      </template>

      <p v-else class="text-body-1 text-medium-emphasis text-center">
        No rounds have been generated yet.
      </p>
    </v-card>
  </v-container>
</template>

<style scoped>
.bracket-scroll {
  overflow-x: auto;
}

.bracket-round {
  min-width: 200px;
  flex-shrink: 0;
}

.bracket-match {
  min-width: 180px;
}
</style>
