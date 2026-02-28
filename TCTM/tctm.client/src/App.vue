<script setup>
import { ref } from 'vue'

const inviteCode = ref('')
</script>

<template>
  <v-app>
    <v-app-bar color="grey-darken-4" flat>
      <v-app-bar-title>
        <v-icon icon="mdi-chess-knight" class="mr-2" />
        Tiny Chess Tournament Manager
      </v-app-bar-title>
    </v-app-bar>

    <v-main>
      <v-container class="d-flex align-center justify-center" style="min-height: 80vh">
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
    </v-main>
  </v-app>
</template>
