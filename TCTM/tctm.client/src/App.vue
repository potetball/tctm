<script setup>
import { useRouter } from 'vue-router'
import logo from '@/assets/tctm-logo.png'

const router = useRouter()
</script>

<template>
  <v-app>
    <v-app-bar color="grey-darken-4" flat>
      <v-app-bar-title class="cursor-pointer" @click="router.push({ name: 'home' })">
        <img :src="logo" alt="TCTM Logo" height="36" class="mr-2" style="vertical-align: middle" />
        Tiny Chess Tournament Manager
      </v-app-bar-title>
    </v-app-bar>
    <v-main>
      <router-view />
    </v-main>
  </v-app>
</template>
