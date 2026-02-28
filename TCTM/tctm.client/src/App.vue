<script setup>
import { ref } from 'vue'
import CreateTournament from '@/components/CreateTournament.vue'

const currentView = ref('home') // 'home' | 'create' | 'created'
const inviteCode = ref('')
const createdTournament = ref(null)

function onTournamentCreated(result) {
  createdTournament.value = result
  currentView.value = 'created'
}

function goHome() {
  currentView.value = 'home'
  createdTournament.value = null
}
</script>

<template>
  <v-app>
    <v-app-bar color="grey-darken-4" flat>
      <v-app-bar-title class="cursor-pointer" @click="goHome">
        <v-icon icon="mdi-chess-knight" class="mr-2" />
        Tiny Chess Tournament Manager
      </v-app-bar-title>
    </v-app-bar>

    <v-main>
      <!-- Home / Landing -->
      <v-container v-if="currentView === 'home'" class="d-flex align-center justify-center" style="min-height: 80vh">
        <v-card max-width="520" width="100%" class="pa-6" elevation="8" rounded="xl">
          <div class="text-center mb-6">
            <v-icon icon="mdi-chess-queen" size="64" color="amber-darken-2" />
            <h1 class="text-h4 font-weight-bold mt-4">Welcome to TCTM</h1>
            <p class="text-body-1 text-medium-emphasis mt-2">
              Organise and manage small chess tournaments with ease.
              Create a new tournament or join an existing one.
            </p>
          </div>

          <v-divider class="mb-6" />

          <div class="d-flex flex-column ga-4">
            <v-btn
              color="amber-darken-2"
              size="x-large"
              block
              rounded="lg"
              prepend-icon="mdi-plus-circle-outline"
              @click="currentView = 'create'"
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
            />

            <v-btn
              color="grey-darken-3"
              size="large"
              block
              rounded="lg"
              variant="tonal"
              prepend-icon="mdi-login"
              :disabled="inviteCode.length === 0"
            >
              Join Tournament
            </v-btn>
          </div>

          <div class="text-center mt-8">
            <p class="text-caption text-disabled">
              No account required — just pick a name and play.
            </p>
          </div>
        </v-card>
      </v-container>

      <!-- Create Tournament Form -->
      <CreateTournament
        v-else-if="currentView === 'create'"
        @created="onTournamentCreated"
        @cancel="goHome"
      />

      <!-- Tournament Created Success -->
      <v-container v-else-if="currentView === 'created'" class="d-flex align-center justify-center" style="min-height: 80vh">
        <v-card max-width="520" width="100%" class="pa-6" elevation="8" rounded="xl">
          <div class="text-center mb-6">
            <v-icon icon="mdi-check-circle-outline" size="64" color="success" />
            <h1 class="text-h4 font-weight-bold mt-4">Tournament Created!</h1>
            <p class="text-body-1 text-medium-emphasis mt-2">
              Share the invite code below with your players so they can join.
            </p>
          </div>

          <v-divider class="mb-6" />

          <div class="d-flex flex-column ga-4">
            <v-text-field
              :model-value="createdTournament?.name"
              label="Tournament"
              variant="outlined"
              density="comfortable"
              prepend-inner-icon="mdi-trophy-outline"
              readonly
            />

            <v-text-field
              :model-value="createdTournament?.inviteCode"
              label="Invite Code"
              variant="outlined"
              density="comfortable"
              prepend-inner-icon="mdi-ticket-confirmation-outline"
              readonly
              hint="Share this with your players"
              persistent-hint
            />

            <v-text-field
              :model-value="createdTournament?.adminToken"
              label="Admin Token"
              variant="outlined"
              density="comfortable"
              prepend-inner-icon="mdi-shield-key-outline"
              readonly
              hint="Keep this secret — you'll need it to manage the tournament"
              persistent-hint
            />

            <v-btn
              color="amber-darken-2"
              size="large"
              block
              rounded="lg"
              prepend-icon="mdi-home"
              class="mt-2"
              @click="goHome"
            >
              Back to Home
            </v-btn>
          </div>
        </v-card>
      </v-container>
    </v-main>
  </v-app>
</template>
