import { createRouter, createWebHistory } from 'vue-router'

import HomePage from '@/pages/HomePage.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'home',
      component: HomePage,
    },
    {
      path: '/create',
      name: 'create-tournament',
      component: () => import('@/pages/CreateTournamentPage.vue'),
    },
    {
      path: '/t/:slug',
      name: 'tournament',
      component: () => import('@/pages/TournamentDashboard.vue'),
    },
    {
      path: '/t/:slug/standings',
      name: 'standings',
      component: () => import('@/pages/StandingsPage.vue'),
    },
    {
      path: '/t/:slug/bracket',
      name: 'bracket',
      component: () => import('@/pages/BracketPage.vue'),
    },
    {
      path: '/t/:slug/admin',
      name: 'admin',
      component: () => import('@/pages/AdminPage.vue'),
    },
    {
      path: '/t/:slug/game/:matchId',
      name: 'live-game',
      component: () => import('@/pages/LiveGamePage.vue'),
    },
  ],
})

export default router
