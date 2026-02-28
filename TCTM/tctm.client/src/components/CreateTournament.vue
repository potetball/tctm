<script setup>
import { ref, computed } from 'vue'
import { tournaments, TournamentFormat, TimeControlPreset } from '@/api'

const emit = defineEmits(['created', 'cancel'])

const name = ref('')
const format = ref(TournamentFormat.RoundRobin)
const timeControlPreset = ref(TimeControlPreset.Rapid)
const timeControlMinutes = ref(15)
const loading = ref(false)
const error = ref('')

const formatOptions = [
  { title: 'Round Robin', value: TournamentFormat.RoundRobin, description: 'Every player plays against every other player. Best for small groups where everyone gets maximum games.' },
  { title: 'Swiss', value: TournamentFormat.Swiss, description: 'Players are paired each round based on similar scores. Efficient for large groups with fewer rounds needed.' },
  { title: 'Single Elimination', value: TournamentFormat.SingleElimination, description: 'Lose once and you\'re out. Fast and dramatic — perfect for quick knockout brackets.' },
  { title: 'Double Elimination', value: TournamentFormat.DoubleElimination, description: 'Players must lose twice to be eliminated. Includes a losers bracket for a second chance.' },
]

const timePresetOptions = [
  { title: 'Bullet', value: TimeControlPreset.Bullet, description: 'Ultra-fast games under 3 minutes per side. Tests instinct and speed over deep calculation.' },
  { title: 'Blitz', value: TimeControlPreset.Blitz, description: 'Fast-paced games of 3–10 minutes per side. A popular balance of speed and strategy.' },
  { title: 'Rapid', value: TimeControlPreset.Rapid, description: 'Longer games of 10+ minutes per side. Allows more thoughtful, strategic play.' },
]

const selectedFormatDescription = computed(() =>
  formatOptions.find(o => o.value === format.value)?.description ?? ''
)

const selectedPresetDescription = computed(() =>
  timePresetOptions.find(o => o.value === timeControlPreset.value)?.description ?? ''
)

const isValid = computed(() => name.value.trim().length > 0 && timeControlMinutes.value > 0)

async function submit() {
  if (!isValid.value) return

  loading.value = true
  error.value = ''

  try {
    const result = await tournaments.createTournament({
      name: name.value.trim(),
      format: format.value,
      timeControlPreset: timeControlPreset.value,
      timeControlMinutes: timeControlMinutes.value,
    })
    emit('created', result)
  } catch (err) {
    error.value = err.message || 'Failed to create tournament'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <v-container class="d-flex align-center justify-center" style="max-width: 1200px; margin: 0 auto; min-height: 80vh;">
    <v-card max-width="560" width="100%" class="pa-6" elevation="8" rounded="xl">
      <div class="text-center mb-6">
        <v-icon icon="mdi-trophy-outline" size="64" color="amber-darken-2" />
        <h1 class="text-h4 font-weight-bold mt-4">Create Tournament</h1>
        <p class="text-body-1 text-medium-emphasis mt-2">
          Set up a new chess tournament. Share the invite code with your players once it's created.
        </p>
      </div>

      <v-divider class="mb-6" />

      <v-alert v-if="error" type="error" variant="tonal" class="mb-4" closable @click:close="error = ''">
        {{ error }}
      </v-alert>

      <v-form @submit.prevent="submit">
        <div class="d-flex flex-column ga-4">
          <v-text-field
            v-model="name"
            label="Tournament Name"
            placeholder="e.g. Friday Night Blitz"
            variant="outlined"
            density="comfortable"
            prepend-inner-icon="mdi-rename"
            :rules="[v => !!v.trim() || 'Name is required']"
            maxlength="100"
            counter
          />

          <div>
            <v-select
              v-model="format"
              :items="formatOptions"
              label="Format"
              variant="outlined"
              density="comfortable"
              prepend-inner-icon="mdi-format-list-bulleted"
            />
            <p class="text-body-2 text-medium-emphasis mt-n2 mb-1 mx-3">
              {{ selectedFormatDescription }}
            </p>
          </div>

          <div>
            <v-select
              v-model="timeControlPreset"
              :items="timePresetOptions"
              label="Time Control"
              variant="outlined"
              density="comfortable"
              prepend-inner-icon="mdi-timer-outline"
            />
            <p class="text-body-2 text-medium-emphasis mt-n2 mb-1 mx-3">
              {{ selectedPresetDescription }}
            </p>
          </div>

          <v-text-field
            v-model.number="timeControlMinutes"
            label="Minutes per Side"
            type="number"
            variant="outlined"
            density="comfortable"
            prepend-inner-icon="mdi-clock-outline"
            :rules="[v => v > 0 || 'Must be at least 1 minute']"
            min="1"
            max="180"
          />

          <v-btn
            color="amber-darken-2"
            size="x-large"
            block
            rounded="lg"
            type="submit"
            :loading="loading"
            :disabled="!isValid"
            prepend-icon="mdi-plus-circle-outline"
          >
            Create Tournament
          </v-btn>

          <v-btn
            variant="text"
            size="large"
            block
            rounded="lg"
            prepend-icon="mdi-arrow-left"
            @click="$emit('cancel')"
          >
            Back
          </v-btn>
        </div>
      </v-form>
    </v-card>
  </v-container>
</template>
