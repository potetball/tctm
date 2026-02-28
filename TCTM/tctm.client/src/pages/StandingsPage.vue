<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { standings, tournaments, TournamentFormat } from '@/api'

const route = useRoute()
const router = useRouter()
const slug = route.params.slug

const tournament = ref(null)
const standingsList = ref([])
const loading = ref(true)
const error = ref('')

const showBuchholz = computed(() =>
  tournament.value?.format === TournamentFormat.Swiss
)

const showSB = computed(() =>
  tournament.value?.format === TournamentFormat.RoundRobin ||
  tournament.value?.format === TournamentFormat.Swiss
)

async function loadData() {
  loading.value = true
  error.value = ''

  try {
    const [t, s] = await Promise.all([
      tournaments.getTournament(slug),
      standings.getStandings(slug),
    ])
    tournament.value = t
    standingsList.value = s
  } catch (err) {
    error.value = err.message || 'Failed to load standings.'
  } finally {
    loading.value = false
  }
}

onMounted(loadData)
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

    <v-card class="pa-6" elevation="4" rounded="xl">
      <div class="text-center mb-4">
        <v-icon icon="mdi-podium" size="48" color="amber-darken-2" />
        <h1 class="text-h4 font-weight-bold mt-2">Standings</h1>
        <p v-if="tournament" class="text-body-1 text-medium-emphasis mt-1">{{ tournament.name }}</p>
      </div>

      <v-divider class="mb-4" />

      <div v-if="loading" class="text-center py-8">
        <v-progress-circular indeterminate color="amber-darken-2" />
      </div>

      <v-alert v-else-if="error" type="error" variant="tonal">
        {{ error }}
      </v-alert>

      <template v-else-if="standingsList.length">
        <v-table density="comfortable">
          <thead>
            <tr>
              <th class="text-center">#</th>
              <th>Player</th>
              <th class="text-center">Points</th>
              <th class="text-center">W</th>
              <th class="text-center">D</th>
              <th class="text-center">L</th>
              <th v-if="showBuchholz" class="text-center">Buchholz</th>
              <th v-if="showSB" class="text-center">SB</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(s, index) in standingsList" :key="s.playerId">
              <td class="text-center font-weight-bold">{{ index + 1 }}</td>
              <td>{{ s.displayName }}</td>
              <td class="text-center font-weight-bold">{{ s.points }}</td>
              <td class="text-center">{{ s.wins }}</td>
              <td class="text-center">{{ s.draws }}</td>
              <td class="text-center">{{ s.losses }}</td>
              <td v-if="showBuchholz" class="text-center">{{ s.buchholz.toFixed(1) }}</td>
              <td v-if="showSB" class="text-center">{{ s.sonnebornBerger.toFixed(1) }}</td>
            </tr>
          </tbody>
        </v-table>
      </template>

      <p v-else class="text-body-1 text-medium-emphasis text-center">
        No standings available yet. Play some games first!
      </p>
    </v-card>
  </v-container>
</template>
