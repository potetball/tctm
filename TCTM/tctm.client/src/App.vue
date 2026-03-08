<script setup>
import { computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useTournamentStore } from '@/composables/useTournamentStore'
import logo from '@/assets/tctm-logo.png'
import ChessSpritesheet from '@/components/ChessSpritesheet.vue'

const router = useRouter()
const route = useRoute()
const { tournaments, removeTournament } = useTournamentStore()

/** The slug of the tournament currently being viewed (if any). */
const activeSlug = computed(() => route.params.slug || null)

/** The other tournaments (not the one currently being viewed). */
const otherTournaments = computed(() =>
  tournaments.value.filter((t) => t.slug !== activeSlug.value),
)

function switchTo(slug) {
  router.push({ name: 'tournament', params: { slug } })
}

function leave(slug) {
  removeTournament(slug)
  // If we're currently viewing the tournament we just removed, go home
  if (activeSlug.value === slug) {
    router.push({ name: 'home' })
  }
}

const roleIcon = {
  admin: 'mdi-shield-crown-outline',
  player: 'mdi-account-check',
  spectator: 'mdi-eye-outline',
}
</script>

<template>
  <v-app>
    <!-- Chess pieces SVG spritesheet (hidden, loaded once) -->
    <ChessSpritesheet />

    <v-app-bar color="grey-darken-4" flat>
      <v-app-bar-title class="cursor-pointer" @click="router.push({ name: 'home' })">
        <img :src="logo" alt="TCTM Logo" height="36" class="mr-2" style="vertical-align: middle" />
        Tiny Chess Tournament Manager
      </v-app-bar-title>

      <template #append>
        <v-menu v-if="tournaments.length" location="bottom end" :close-on-content-click="false">
          <template #activator="{ props }">
            <v-btn v-bind="props" icon variant="text" size="small">
              <v-badge
                :content="tournaments.length"
                color="amber-darken-2"
                floating
              >
                <v-icon>mdi-trophy-outline</v-icon>
              </v-badge>
            </v-btn>
          </template>

          <v-card min-width="300" max-width="380" rounded="lg">
            <v-card-title class="text-subtitle-1 font-weight-bold pb-1">
              My Tournaments
            </v-card-title>

            <v-list density="compact" nav>
              <v-list-item
                v-for="t in tournaments"
                :key="t.slug"
                :active="t.slug === activeSlug"
                active-color="amber-darken-2"
                @click="switchTo(t.slug)"
              >
                <template #prepend>
                  <v-icon :icon="roleIcon[t.role] || 'mdi-eye-outline'" size="small" class="mr-2" />
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
                    @click.stop="leave(t.slug)"
                  />
                </template>
              </v-list-item>
            </v-list>
          </v-card>
        </v-menu>
      </template>
    </v-app-bar>
    <v-main>
      <router-view />
    </v-main>
  </v-app>
</template>
