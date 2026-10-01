<template>
  <section :aria-labelledby="titleId" class="flex flex-col gap-2">
    <h3 :id="titleId" class="text-sm font-semibold text-fg">{{ t('branding.contrast.title') }}</h3>
    <ul class="flex flex-col gap-2">
      <li
        v-for="check in checks"
        :key="check.name ?? ''"
        class="flex flex-wrap items-center justify-between gap-2 rounded-md border border-border px-3 py-2"
      >
        <div class="min-w-0">
          <p class="text-sm text-fg">{{ label(check.name) }}</p>
          <p class="text-xs text-fg-muted">
            {{ t('branding.contrast.ratio', { ratio: numberText(check.ratio), required: numberText(check.required, 1) }) }}
          </p>
        </div>
        <UiBadge :tone="check.passes ? 'success' : 'danger'">
          {{ check.passes ? t('branding.contrast.pass') : t('branding.contrast.fail') }}
        </UiBadge>
      </li>
    </ul>
  </section>
</template>

<script setup lang="ts">
import type { ContrastCheckDto } from '~/composables/branding/types'
import { isolateLtr } from '~/utils/format'

interface Props {
  checks: ReadonlyArray<ContrastCheckDto>
}
defineProps<Props>()

const { t } = useI18n()
const format = useFormat()
const titleId = useId()

// Fixed map: the label key is never built from the response.
const labelKeys: Readonly<Record<string, string>> = {
  PrimaryText: 'branding.contrast.primaryText',
  AccentOnSurface: 'branding.contrast.accentOnSurface',
}

function label(name: string | null | undefined): string {
  const key = name ? (labelKeys[name] ?? undefined) : undefined
  return key ? t(key) : (name ?? '')
}

// The numbers go inside the translated sentence as a left-to-right isolate, so 4.48:1 never reorders in Arabic.
function numberText(value: number | undefined, digits = 2): string {
  const text = format.number(value ?? 0, { minimumFractionDigits: digits, maximumFractionDigits: digits })
  return isolateLtr(text)
}
</script>
