<template>
  <UiButton
    variant="ghost"
    size="sm"
    :aria-pressed="theme === 'dark'"
    :aria-label="theme === 'dark' ? lightLabel : darkLabel"
    @click="toggleTheme"
  >
    <span aria-hidden="true">{{ theme === 'dark' ? '🌙' : '☀️' }}</span>
  </UiButton>
</template>

<script setup lang="ts">
interface Props {
  /** Announced when the current theme is dark (clicking would switch to light). */
  lightLabel: string
  /** Announced when the current theme is light (clicking would switch to dark). */
  darkLabel: string
}
defineProps<Props>()

const { theme, toggleTheme } = useTheme()
</script>
