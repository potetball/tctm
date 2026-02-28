<script setup>
import { useRouter } from 'vue-router'

const router = useRouter()
</script>

<template>
  <v-app>
    <v-app-bar color="grey-darken-4" flat>
      <v-app-bar-title class="cursor-pointer" @click="router.push({ name: 'home' })">
        <v-icon icon="mdi-chess-knight" class="mr-2" />
        Tiny Chess Tournament Manager
      </v-app-bar-title>
    </v-app-bar>
    <v-main>
      <router-view />
    </v-main>
  </v-app>
</template>
