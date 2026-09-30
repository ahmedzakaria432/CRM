<template>
  <section :aria-labelledby="headingId" class="min-w-0 rounded-md border border-border bg-surface p-4">
    <h2 :id="headingId" class="text-base font-semibold text-fg">{{ t('tickets.thread.title') }}</h2>

    <div class="mt-3">
      <UiSkeleton v-if="status === 'pending' && !data" :lines="4" />
      <UiErrorState
        v-else-if="forbidden"
        :title="t('errors.problem.forbidden')"
        :description="t('errors.code.Organisation.OutOfScope')"
      />
      <UiErrorState
        v-else-if="error && !data"
        :title="t('tickets.thread.loadError')"
        :retry-label="t('tickets.thread.retry')"
        @retry="refresh()"
      />
      <UiEmptyState
        v-else-if="data && entries.length === 0"
        :heading-level="3"
        :title="t('tickets.thread.empty')"
        :description="t('tickets.thread.emptyDescription')"
      />
      <template v-else>
        <ol class="flex flex-col gap-3">
          <li v-for="entry in entries" :key="entry.sequence" class="flex">
            <div
              v-if="entry.type === 'Message'"
              class="flex min-w-0 max-w-[85%] flex-col rounded-md border p-3"
              :class="entry.direction === 'Inbound' ? 'me-auto border-border bg-surface' : 'ms-auto border-info-500 bg-info-50'"
            >
              <span class="text-xs font-medium text-fg-muted">{{ senderOf(entry) }}</span>
              <p dir="auto" class="mt-1 whitespace-pre-wrap break-words text-sm text-fg">{{ entry.body }}</p>
              <div class="mt-2 flex flex-wrap items-center gap-2 text-xs text-fg-muted">
                <span>{{ when(entry) }}</span>
                <span v-if="entry.direction === 'Inbound'">
                  {{
                    entry.recordedById !== undefined && entry.recordedById === entry.authorId
                      ? t('tickets.thread.viaPortal')
                      : t('tickets.thread.recordedBy', { name: recorderOf(entry) })
                  }}
                </span>
              </div>
            </div>
            <div
              v-else-if="entry.type === 'Note'"
              class="min-w-0 flex-1 rounded-md border border-warning-500 bg-warning-50 p-3"
            >
              <div class="flex flex-wrap items-center gap-2 text-xs text-fg-muted">
                <UiBadge tone="warning" size="sm">{{ t('tickets.thread.internalNote') }}</UiBadge>
                <span>{{ authorOf(entry) }}</span>
                <span>{{ when(entry) }}</span>
              </div>
              <p dir="auto" class="mt-2 whitespace-pre-wrap break-words text-sm text-fg">{{ entry.body }}</p>
            </div>
            <p v-else class="w-full text-center text-xs text-fg-muted">
              {{ activitySentence(entry) }} <span class="whitespace-nowrap">· {{ when(entry) }}</span>
            </p>
          </li>
        </ol>
        <div v-if="nextCursor !== null" class="mt-3 flex justify-center">
          <UiButton variant="secondary" :loading="loadingMore" @click="loadMore()">{{ t('tickets.thread.loadMore') }}</UiButton>
        </div>
        <p v-if="moreFailed" role="alert" class="mt-2 text-center text-sm text-danger-600">
          {{ t('tickets.thread.loadError') }}
        </p>
      </template>
    </div>
  </section>
</template>

<script setup lang="ts">
import { useTicketThread, type TicketThreadEntryDto, type TicketThreadPageDto } from '~/composables/tickets/useTicketThread'
import { ProblemTypes, problemOf } from '~/api/problem'
import { activityText, mergeLookups, personName } from '~/utils/ticketThread'

interface Props {
  ticketId: string
  version?: number
  customerLabel?: string
  /** Story 47: the ticket contact; a portal customer acts as this contact, so activities name them. */
  contactId?: string
}
const props = withDefaults(defineProps<Props>(), { version: 0, customerLabel: undefined, contactId: undefined })

const { t, te, locale } = useI18n()
const format = useFormat()
const auth = useAuthStore()
const thread = useTicketThread(props.ticketId)
const { data, status, error, refresh } = thread.load()
const headingId = useId()

// Pages after the first ("Show more"); reset whenever the first page is reloaded.
const morePages = ref<TicketThreadPageDto[]>([])
const loadingMore = ref(false)
const moreFailed = ref(false)

watch(data, () => {
  morePages.value = []
})

const pages = computed<TicketThreadPageDto[]>(() => (data.value ? [data.value, ...morePages.value] : []))
const lookups = computed(() => {
  const merged = mergeLookups(pages.value)
  if (props.contactId !== undefined && !merged.people.has(props.contactId)) {
    const people = new Map(merged.people)
    people.set(props.contactId, props.customerLabel ?? t('tickets.thread.customer'))
    return { ...merged, people }
  }
  return merged
})
const entries = computed<TicketThreadEntryDto[]>(() => {
  const bySequence = new Map<number, TicketThreadEntryDto>()
  for (const page of pages.value) {
    for (const entry of page.items ?? []) {
      if (entry.sequence !== undefined) bySequence.set(entry.sequence, entry)
    }
  }
  return [...bySequence.values()].sort((a, b) => (a.sequence ?? 0) - (b.sequence ?? 0))
})
const nextCursor = computed<number | null>(() => pages.value[pages.value.length - 1]?.nextCursor ?? null)
const forbidden = computed(() => problemOf(error.value)?.type === ProblemTypes.Forbidden)

const translate = (key: string): string => t(key)
const exists = (key: string): boolean => te(key)

async function loadMore(): Promise<void> {
  const after = nextCursor.value
  if (after === null || loadingMore.value) return
  loadingMore.value = true
  moreFailed.value = false
  const result = await thread.page(after)
  loadingMore.value = false
  if (!result.ok) {
    moreFailed.value = true
    return
  }
  morePages.value = [...morePages.value, result.data]
}

const lastSequence = computed(() => entries.value[entries.value.length - 1]?.sequence ?? 0)

/** Appends everything after the last loaded entry (R11) - up to 5 pages; beyond that "Show more" stays. */
async function loadNewer(): Promise<void> {
  if (!data.value) {
    await refresh()
    return
  }
  let after = lastSequence.value
  for (let i = 0; i < 5; i += 1) {
    const result = await thread.page(after)
    if (!result.ok) {
      moreFailed.value = true
      return
    }
    morePages.value = [...morePages.value, result.data]
    const next = result.data.nextCursor
    if (next === null || next === undefined) return
    after = next
  }
}

watch(
  () => props.version,
  () => void loadNewer(),
)

function when(entry: TicketThreadEntryDto): string {
  return entry.occurredAtUtc ? format.dateTime(entry.occurredAtUtc) : ''
}

function authorOf(entry: TicketThreadEntryDto): string {
  return entry.authorId !== undefined && entry.authorId === auth.me?.id
    ? t('tickets.thread.you')
    : personName(entry.authorId, lookups.value, translate)
}

function senderOf(entry: TicketThreadEntryDto): string {
  return entry.direction === 'Inbound' ? (props.customerLabel ?? t('tickets.thread.customer')) : authorOf(entry)
}

function recorderOf(entry: TicketThreadEntryDto): string {
  return entry.recordedById !== undefined && entry.recordedById === auth.me?.id
    ? t('tickets.thread.you')
    : personName(entry.recordedById, lookups.value, translate)
}

function activitySentence(entry: TicketThreadEntryDto): string {
  const text = activityText(entry, lookups.value, auth.me?.id, locale.value, translate, exists)
  return t(text.key, text.params)
}
</script>
