<script setup>
import { useRouter } from 'vue-router'

const router = useRouter()

const handleLogout = () => {
  localStorage.removeItem('token')
  router.push('/login')
}
</script>

<template>
  <div>
    <!-- Header with Logout -->
    <div class="flex justify-between items-center mb-8">
      <div>
        <h1 class="text-3xl font-bold text-gray-900">Time Management Dashboard</h1>
        <p class="text-gray-600 mt-1">Track and manage your time efficiently</p>
      </div>
      <button
        @click="handleLogout"
        class="px-4 py-2 bg-red-600 hover:bg-red-700 text-white font-medium rounded-lg transition duration-200"
      >
        Logout
      </button>
    </div>

    <!-- Main Content -->
    <div class="bg-white rounded-lg shadow-lg p-8">
      <div class="text-center py-12">
        <svg class="mx-auto h-16 w-16 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
        <h3 class="mt-4 text-xl font-semibold text-gray-900">Welcome to Time Management</h3>
        <p class="mt-2 text-gray-600">Your time tracking dashboard will appear here</p>
      </div>
    </div>
  </div>
</template>