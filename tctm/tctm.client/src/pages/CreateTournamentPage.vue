<script setup>
import { useRouter } from 'vue-router'
import { useTournamentStore } from '@/composables/useTournamentStore'
import CreateTournament from '@/components/CreateTournament.vue'

const router = useRouter()
const { addTournament } = useTournamentStore()

function onCreated(result) {
  // Store admin token for this tournament
  const adminTokens = JSON.parse(localStorage.getItem('tctm_admin_tokens') || '{}')
  adminTokens[result.slug] = result.adminToken
  localStorage.setItem('tctm_admin_tokens', JSON.stringify(adminTokens))

  // Register in tournament store
  addTournament(result.slug, result.name, 'admin')

  // Navigate to the tournament dashboard
  router.push({ name: 'tournament', params: { slug: result.slug } })
}

function onCancel() {
  router.push({ name: 'home' })
}
</script>

<template>
  <CreateTournament @created="onCreated" @cancel="onCancel" />
</template>
