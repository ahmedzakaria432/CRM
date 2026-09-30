<template>
  <div class="mx-auto w-full max-w-3xl">
    <NuxtLink to="/portal" class="text-sm text-primary-600 underline focus-visible:focus-ring">
      {{ t('portal.detail.backToList') }}
    </NuxtLink>

    <UiEmptyState v-if="!valid || notFound" class="mt-4" :title="t('portal.detail.notFound')">
      <template #actions>
        <UiButton variant="secondary" @click="backToList()">{{ t('portal.detail.backToList') }}</UiButton>
      </template>
    </UiEmptyState>
    <UiSkeleton v-else-if="loading" class="mt-4" :lines="6" />
    <UiErrorState
      v-else-if="error && !ticket"
      class="mt-4"
      :title="t('portal.detail.loadError')"
      :retry-label="t('portal.detail.retry')"
      @retry="refresh()"
    />
    <template v-else-if="ticket">
      <header class="mt-4">
        <div class="flex flex-wrap items-center justify-between gap-2">
          <span class="text-sm text-fg-muted">
            {{ t('portal.tickets.number') }} <bdi dir="ltr" class="font-mono">{{ ticket.number }}</bdi>
          </span>
          <UiBadge :tone="portalStatusTone(ticket.status)">{{ statusLabel(ticket.status) }}</UiBadge>
        </div>
        <h1 dir="auto" class="mt-2 break-words text-2xl font-bold text-fg">{{ ticket.subject }}</h1>
        <p class="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-fg-muted">
          <span>{{ categoryName }}</span>
          <span v-if="ticket.createdAtUtc">{{ t('portal.detail.created', { when: format.dateTime(ticket.createdAtUtc) }) }}</span>
          <span v-if="ticket.updatedAtUtc">{{ t('portal.detail.updated', { when: format.dateTime(ticket.updatedAtUtc) }) }}</span>
        </p>
      </header>

      <section class="mt-6 rounded-md border border-border bg-surface p-4" :aria-labelledby="descriptionHeadingId">
        <h2 :id="descriptionHeadingId" class="text-base font-semibold text-fg">{{ t('portal.detail.description') }}</h2>
        <p dir="auto" class="mt-2 whitespace-pre-wrap break-words text-sm text-fg">{{ ticket.description }}</p>
      </section>

      <section class="mt-6 min-w-0 rounded-md border border-border bg-surface p-4" :aria-labelledby="threadHeadingId">
        <h2 :id="threadHeadingId" class="text-base font-semibold text-fg">{{ t('portal.detail.thread') }}</h2>
        <div class="mt-3">
          <UiSkeleton v-if="threadLoading" :lines="3" />
          <UiErrorState
            v-else-if="threadError && !threadData"
            :title="t('portal.detail.threadError')"
            :retry-label="t('portal.detail.retry')"
            @retry="refreshThread()"
          />
          <UiEmptyState
            v-else-if="messages.length === 0"
            :heading-level="3"
            :title="t('portal.detail.threadEmpty')"
          />
          <template v-else>
            <ol class="flex flex-col gap-3">
              <li v-for="message in messages" :key="message.sequence" class="flex">
                <div
                  class="flex min-w-0 max-w-[90%] flex-col rounded-md border p-3"
                  :class="
                    message.sender === 'Customer' ? 'ms-auto border-info-500 bg-info-50' : 'me-auto border-border bg-surface'
                  "
                >
                  <span class="text-xs font-medium text-fg-muted">{{ senderOf(message) }}</span>
                  <p dir="auto" class="mt-1 whitespace-pre-wrap break-words text-sm text-fg">{{ message.body }}</p>
                  <span v-if="message.occurredAtUtc" class="mt-2 text-xs text-fg-muted">
                    {{ format.dateTime(message.occurredAtUtc) }}
                  </span>
                </div>
              </li>
            </ol>
            <div v-if="nextCursor !== null" class="mt-3 flex justify-center">
              <UiButton variant="secondary" :loading="loadingMore" @click="loadMore()">{{ t('portal.detail.loadMore') }}</UiButton>
            </div>
            <p v-if="moreFailed" role="alert" class="mt-2 text-center text-sm text-danger-600">
              {{ t('portal.detail.threadError') }}
            </p>
          </template>
        </div>
      </section>

      <section class="mt-6" aria-live="polite">
        <p v-if="actionError" role="alert" class="mb-3 text-sm text-danger-600">
          {{ actionError }}
          <UiButton v-if="conflicted" variant="secondary" size="sm" class="ms-2" @click="reloadAll()">
            {{ t('portal.detail.reload') }}
          </UiButton>
        </p>

        <form v-if="canReply" novalidate @submit.prevent="sendReply()">
          <UiTextarea
            v-model="replyText"
            :rows="4"
            maxlength="10000"
            :label="t('portal.detail.replyLabel')"
            :error="formErrors.errorFor('body')"
            required
          />
          <p v-if="formErrors.formError.value" role="alert" class="mt-2 text-sm text-danger-600">
            {{ formErrors.formError.value }}
          </p>
          <div class="mt-3 flex flex-wrap gap-2">
            <UiButton type="submit" :loading="sending">{{ t('portal.detail.send') }}</UiButton>
            <UiButton v-if="canRequestClosure" variant="secondary" :disabled="sending" @click="confirmOpen = true">
              {{ t('portal.detail.requestClosure') }}
            </UiButton>
            <UiButton v-if="canReopen" variant="secondary" :loading="acting" @click="reopen()">
              {{ t('portal.detail.reopen') }}
            </UiButton>
          </div>
        </form>
        <div v-else-if="canReopen" class="flex flex-wrap gap-2">
          <UiButton variant="secondary" :loading="acting" @click="reopen()">{{ t('portal.detail.reopen') }}</UiButton>
        </div>
        <div v-else class="rounded-md border border-border bg-surface-sunken p-4">
          <p class="text-sm text-fg">{{ t('portal.detail.closedNote') }}</p>
          <div class="mt-3">
            <UiButton @click="newRequest()">{{ t('portal.detail.newRequest') }}</UiButton>
          </div>
        </div>
      </section>

      <UiModal v-model:open="confirmOpen" size="sm" :title="t('portal.detail.requestClosureTitle')" :close-label="t('portal.detail.cancel')">
        <p class="text-sm text-fg">{{ t('portal.detail.requestClosureBody') }}</p>
        <template #footer>
          <UiButton variant="secondary" @click="confirmOpen = false">{{ t('portal.detail.cancel') }}</UiButton>
          <UiButton :loading="acting" @click="requestClosure()">{{ t('portal.detail.requestClosureConfirm') }}</UiButton>
        </template>
      </UiModal>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ProblemTypes, problemOf, type Problem } from '~/api/problem'
import type { PortalThreadMessageDto, PortalThreadPageDto } from '~/composables/portal/types'
import { usePortalTicket } from '~/composables/portal/usePortalTicket'
import { usePortalTicketCommands } from '~/composables/portal/usePortalTicketCommands'
import { usePortalTicketThread } from '~/composables/portal/usePortalTicketThread'
import { isGuid } from '~/utils/customers'
import { portalStatusKey, portalStatusTone } from '~/utils/portal'
import { localizedName } from '~/utils/scope'
import { bodyFieldForCode } from '~/utils/tickets'

definePageMeta({ layout: 'portal', key: (route) => route.path })

const { t, locale } = useI18n()
const route = useRoute()
const format = useFormat()
const formErrors = useFormErrors()
const id = String(route.params.id)
// An invalid id never reaches the API: the not-found state renders instead.
const valid = isGuid(id)

const { data: ticket, status, error, refresh } = usePortalTicket(id)
const commands = usePortalTicketCommands()
const thread = usePortalTicketThread(id)
const { data: threadData, status: threadStatus, error: threadError, refresh: refreshThread } = thread.load()

const descriptionHeadingId = useId()
const threadHeadingId = useId()

// Data loads in the browser only, so the server render (and the first client render) is the skeleton: idle counts as pending.
const loading = computed(() => (status.value === 'idle' || status.value === 'pending') && !ticket.value)
const threadLoading = computed(() => (threadStatus.value === 'idle' || threadStatus.value === 'pending') && !threadData.value)
const notFound = computed(() => problemOf(error.value)?.type === ProblemTypes.NotFound)

const categoryName = computed(() =>
  ticket.value?.category != null
    ? localizedName({ ar: ticket.value.category.nameAr, en: ticket.value.category.nameEn }, locale.value)
    : t('portal.tickets.noCategory'),
)

function statusLabel(value: string | null | undefined): string {
  const key = portalStatusKey(value)
  return key === null ? (value ?? '') : t(key)
}

// What the customer may do now comes from the API (Ticket.CustomerActionsFor) - never guessed from the status.
const actions = computed(() => new Set(ticket.value?.actions ?? []))
const canReply = computed(() => actions.value.has('Reply'))
const canRequestClosure = computed(() => actions.value.has('RequestClosure'))
const canReopen = computed(() => actions.value.has('Reopen'))

// --- thread: pages after the first ("Show more"); reset whenever the first page is reloaded ---
const morePages = ref<PortalThreadPageDto[]>([])
const loadingMore = ref(false)
const moreFailed = ref(false)

watch(threadData, () => {
  morePages.value = []
})

const pages = computed<PortalThreadPageDto[]>(() => (threadData.value ? [threadData.value, ...morePages.value] : []))
const messages = computed<PortalThreadMessageDto[]>(() => {
  const bySequence = new Map<number, PortalThreadMessageDto>()
  for (const page of pages.value) {
    for (const message of page.items ?? []) {
      if (message.sequence !== undefined) bySequence.set(message.sequence, message)
    }
  }
  return [...bySequence.values()].sort((a, b) => (a.sequence ?? 0) - (b.sequence ?? 0))
})
const nextCursor = computed<number | null>(() => pages.value[pages.value.length - 1]?.nextCursor ?? null)

function senderOf(message: PortalThreadMessageDto): string {
  if (message.sender === 'Customer') return message.isYou ? t('portal.detail.you') : t('portal.detail.colleague')
  return t('portal.detail.support')
}

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

const lastSequence = computed(() => messages.value[messages.value.length - 1]?.sequence ?? 0)

/** Appends everything after the last loaded message - up to 5 pages; beyond that "Show more" stays. */
async function loadNewer(): Promise<void> {
  if (!threadData.value) {
    await refreshThread()
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

// --- actions ---
const replyText = ref('')
const sending = ref(false)
const acting = ref(false)
const confirmOpen = ref(false)
const actionError = ref<string | null>(null)
const conflicted = ref(false)

function showActionProblem(problem: Problem): void {
  conflicted.value = problem.type === ProblemTypes.Conflict
  if (conflicted.value) {
    actionError.value = t('portal.detail.conflict')
  } else if (formErrors.applyCode(problem, {})) {
    actionError.value = formErrors.formError.value
    formErrors.clear()
  } else {
    actionError.value = t('portal.detail.loadError')
  }
}

function resetActionState(): void {
  formErrors.clear()
  actionError.value = null
  conflicted.value = false
}

async function reloadAll(): Promise<void> {
  resetActionState()
  await Promise.all([refresh(), refreshThread()])
}

async function sendReply(): Promise<void> {
  if (sending.value) return
  sending.value = true
  resetActionState()
  const result = await thread.reply(replyText.value)
  sending.value = false

  if (result.ok) {
    ticket.value = result.data
    replyText.value = ''
    await loadNewer()
    return
  }

  if (formErrors.apply(result.problem)) return
  if (formErrors.applyCode(result.problem, bodyFieldForCode)) return
  showActionProblem(result.problem)
}

async function requestClosure(): Promise<void> {
  if (acting.value) return
  acting.value = true
  resetActionState()
  const result = await commands.requestClosure(id)
  acting.value = false
  confirmOpen.value = false

  if (result.ok) {
    ticket.value = result.data
    return
  }
  showActionProblem(result.problem)
}

async function reopen(): Promise<void> {
  if (acting.value) return
  acting.value = true
  resetActionState()
  const result = await commands.reopen(id)
  acting.value = false

  if (result.ok) {
    ticket.value = result.data
    return
  }
  showActionProblem(result.problem)
}

async function backToList(): Promise<void> {
  await navigateTo('/portal')
}

async function newRequest(): Promise<void> {
  await navigateTo('/portal/tickets/new')
}
</script>
