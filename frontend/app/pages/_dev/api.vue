<template>
  <div class="mx-auto max-w-3xl p-4">
    <h1 class="text-2xl font-bold text-fg">/_dev/api</h1>
    <p class="mt-2 max-w-prose text-sm text-fg-muted">
      Exercises the typed API client against the live API and a scripted fake transport. Dev-only —
      excluded from production builds (<code class="font-mono">modules/dev-only-pages.ts</code>).
    </p>

    <!-- Panel 1: live API -->
    <section class="mt-8 border-t border-border pt-6">
      <h2 class="text-lg font-semibold text-fg">1. Live API</h2>
      <p class="mt-1 text-sm text-fg-muted">Requires the backend running in Development on :5187.</p>

      <div class="mt-4 flex flex-wrap gap-2">
        <UiButton size="sm" variant="secondary" @click="callThrow">GET Diagnostics/throw (500)</UiButton>
        <UiButton size="sm" variant="secondary" @click="callForbidden">GET Diagnostics/forbidden (403)</UiButton>
        <UiButton size="sm" variant="secondary" @click="callUnauthorized">
          GET Diagnostics/unauthorized (401)
        </UiButton>
      </div>

      <form class="mt-6 flex max-w-md flex-col gap-3" @submit.prevent="submitCustomer">
        <UiInput v-model="customerName" label="Customer name (English)" :error="formErrors.errorFor('displayNameEn')" />
        <p v-if="formErrors.formError.value" role="alert" class="text-xs text-danger-600">
          {{ formErrors.formError.value }}
        </p>
        <UiButton type="submit" :loading="submitting">Create customer</UiButton>
      </form>
    </section>

    <!-- Panel 2: fake transport, 401 flow without a backend -->
    <section class="mt-8 border-t border-border pt-6">
      <h2 class="text-lg font-semibold text-fg">2. Fake transport — 401 refresh/retry contract</h2>
      <p class="mt-1 max-w-prose text-sm text-fg-muted">
        A private <code class="font-mono">createCrmFetch</code> with a scripted queue of responses and a fake
        <code class="font-mono">AuthSession</code>. This is the manual check for the 401 acceptance criterion
        until Story 07 exists.
      </p>
      <div class="mt-4 flex flex-wrap gap-2">
        <UiButton size="sm" @click="runAllScenarios">Run all scenarios (A–E)</UiButton>
      </div>
      <div class="mt-4 overflow-x-auto">
        <table class="w-full border-collapse text-start text-sm">
          <thead>
            <tr class="border-b border-border">
              <th scope="col" class="p-2 text-start font-medium">Scenario</th>
              <th scope="col" class="p-2 text-start font-medium">Requests</th>
              <th scope="col" class="p-2 text-start font-medium">Refreshes</th>
              <th scope="col" class="p-2 text-start font-medium">Redirects</th>
              <th scope="col" class="p-2 text-start font-medium">Last Authorization</th>
              <th scope="col" class="p-2 text-start font-medium">Result</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="scenario in scenarioOrder" :key="scenario" class="border-b border-border last:border-0">
              <td class="p-2 align-top">
                <p class="font-medium">{{ scenario }}</p>
                <p class="text-xs text-fg-muted">{{ scenarioDescriptions[scenario] }}</p>
              </td>
              <td class="p-2 align-top">{{ scenarioResults[scenario]?.requests ?? '—' }}</td>
              <td class="p-2 align-top">{{ scenarioResults[scenario]?.refreshes ?? '—' }}</td>
              <td class="p-2 align-top">{{ scenarioResults[scenario]?.redirects ?? '—' }}</td>
              <td class="p-2 align-top font-mono text-xs">
                {{ scenarioResults[scenario]?.lastAuthorization ?? '—' }}
              </td>
              <td class="p-2 align-top">
                <UiBadge v-if="scenarioResults[scenario]" :tone="scenarioResults[scenario]?.pass ? 'success' : 'danger'">
                  {{ scenarioResults[scenario]?.pass ? 'PASS' : 'FAIL' }}
                </UiBadge>
                <span v-else class="text-xs text-fg-muted">not run</span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <!-- Panel 3: data states -->
    <section class="mt-8 border-t border-border pt-6">
      <h2 class="text-lg font-semibold text-fg">3. Data states</h2>
      <div class="mt-4 flex flex-wrap gap-2">
        <UiButton size="sm" variant="secondary" @click="loadCustomers">Load customers (skeleton → data)</UiButton>
        <UiButton size="sm" variant="secondary" @click="showEmpty">Simulate empty</UiButton>
        <UiButton size="sm" variant="secondary" @click="loadMissingRoute">Load missing route (404)</UiButton>
      </div>
      <div class="mt-4 max-w-md">
        <UiSkeleton v-if="dataState === 'loading'" variant="text" :lines="2" />
        <UiEmptyState v-else-if="dataState === 'empty'" title="No customers" description="Nothing to show yet." />
        <UiErrorState
          v-else-if="dataState === 'error'"
          title="Could not load"
          :description="dataError?.detail ?? dataError?.title"
          :correlation-id="dataError?.correlationId ?? undefined"
          reference-label="Reference"
          retry-label="Try again"
          @retry="loadMissingRoute"
        />
        <div v-else-if="dataState === 'data' && customerData" class="rounded-md border border-border p-3 text-sm">
          <p><span class="font-medium">number:</span> {{ customerData.number }}</p>
          <p><span class="font-medium">name (en):</span> {{ customerData.displayName?.en }}</p>
        </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { createCrmFetch } from '~/api/crm-fetch'
import type { components } from '~/api/generated/agent'
import type { AuthSession } from '~/api/auth-session'
import { toProblem, networkProblem, type Problem } from '~/api/problem'
import { newCorrelationId, CorrelationHeader } from '~/api/correlation'

definePageMeta({ layout: false })

const api = useApi()
const toasts = useToasts()
const formErrors = useFormErrors()

// --- Panel 1: live API ---------------------------------------------------

async function callThrow(): Promise<void> {
  await api.call(api.clients.admin.GET('/api/Admin/Diagnostics/throw'))
}
async function callForbidden(): Promise<void> {
  await api.call(api.clients.admin.GET('/api/Admin/Diagnostics/forbidden'))
}
async function callUnauthorized(): Promise<void> {
  // anonymousSession.refresh() resolves false: exactly one request, then redirect to /login (Task 6d).
  await api.call(api.clients.admin.GET('/api/Admin/Diagnostics/unauthorized'))
}

const customerName = ref('')
const submitting = ref(false)

async function submitCustomer(): Promise<void> {
  formErrors.clear()
  submitting.value = true

  const result = await api.call(
    api.clients.agent.POST('/api/Agent/Customers', {
      body: { displayNameEn: customerName.value, type: 'Individual', preferredLanguage: 'en' },
    }),
  )
  submitting.value = false

  if (result.ok) {
    if (result.data.created) {
      toasts.push({ tone: 'success', title: 'Customer created', description: result.data.customer?.number ?? null })
    } else {
      toasts.push({ tone: 'info', title: 'Possible duplicate', description: null })
    }
    customerName.value = ''
    return
  }
  // A validation (400) problem lands as field errors here, never a toast (useApi already skipped it).
  formErrors.apply(result.problem)
}

// --- Panel 2: fake transport ----------------------------------------------

interface FakeLog {
  authorization: string | null
  correlationId: string | null
}

interface ScenarioOutcome {
  requests: number
  refreshes: number
  redirects: number
  lastAuthorization: string | null
  pass: boolean
}

type ScenarioId = 'A' | 'B' | 'C' | 'D' | 'E'
const scenarioOrder: ReadonlyArray<ScenarioId> = ['A', 'B', 'C', 'D', 'E']
const scenarioDescriptions: Record<ScenarioId, string> = {
  A: '[401, 200], refresh -> true',
  B: '[401, 401], refresh -> true',
  C: '[401], refresh -> false',
  D: '3 parallel calls, each [401, 200]',
  E: 'isRefreshRequest true, [401]',
}
const scenarioResults = reactive<Record<ScenarioId, ScenarioOutcome | null>>({
  A: null,
  B: null,
  C: null,
  D: null,
  E: null,
})

function makeFakeSession(refreshSucceeds: boolean, isRefreshRequestFlag: boolean, counter: { count: number }): AuthSession {
  let tokenVersion = 0
  return {
    accessToken: () => (tokenVersion === 0 ? null : `token-${tokenVersion}`),
    refresh: async () => {
      counter.count++
      if (!refreshSucceeds) return false
      tokenVersion++
      return true
    },
    clear: () => {},
    isRefreshRequest: () => isRefreshRequestFlag,
  }
}

function makeBaseFetch(queue: number[], logs: FakeLog[]): (request: Request) => Promise<Response> {
  return async (request: Request): Promise<Response> => {
    logs.push({
      authorization: request.headers.get('Authorization'),
      correlationId: request.headers.get(CorrelationHeader),
    })
    const status = queue.shift() ?? 500
    return new Response(null, { status })
  }
}

async function runSingleScenario(
  id: ScenarioId,
  queue: number[],
  refreshSucceeds: boolean,
  isRefreshRequestFlag: boolean,
  expected: { requests: number; refreshes: number; redirects: number },
): Promise<void> {
  const counter = { count: 0 }
  let redirects = 0
  const logs: FakeLog[] = []
  const baseFetch = makeBaseFetch([...queue], logs)
  const session = makeFakeSession(refreshSucceeds, isRefreshRequestFlag, counter)
  const crmFetch = createCrmFetch({
    baseFetch,
    session: () => session,
    onUnauthenticated: () => {
      redirects++
    },
  })
  await crmFetch(new Request('https://example.test/resource'))

  const pass =
    logs.length === expected.requests && counter.count === expected.refreshes && redirects === expected.redirects

  scenarioResults[id] = {
    requests: logs.length,
    refreshes: counter.count,
    redirects,
    lastAuthorization: logs.at(-1)?.authorization ?? null,
    pass,
  }
}

async function runScenarioD(): Promise<void> {
  const counter = { count: 0 }
  let redirects = 0
  const logs: FakeLog[] = []
  const baseFetch = makeBaseFetch([401, 401, 401, 200, 200, 200], logs)
  const session = makeFakeSession(true, false, counter)
  const crmFetch = createCrmFetch({
    baseFetch,
    session: () => session,
    onUnauthenticated: () => {
      redirects++
    },
  })
  await Promise.all([
    crmFetch(new Request('https://example.test/a')),
    crmFetch(new Request('https://example.test/b')),
    crmFetch(new Request('https://example.test/c')),
  ])

  const pass = logs.length === 6 && counter.count === 1 && redirects === 0
  scenarioResults.D = {
    requests: logs.length,
    refreshes: counter.count,
    redirects,
    lastAuthorization: logs.at(-1)?.authorization ?? null,
    pass,
  }
}

async function runAllScenarios(): Promise<void> {
  await runSingleScenario('A', [401, 200], true, false, { requests: 2, refreshes: 1, redirects: 0 })
  await runSingleScenario('B', [401, 401], true, false, { requests: 2, refreshes: 1, redirects: 1 })
  await runSingleScenario('C', [401], false, false, { requests: 1, refreshes: 1, redirects: 1 })
  await runScenarioD()
  await runSingleScenario('E', [401], true, true, { requests: 1, refreshes: 0, redirects: 0 })
}

// --- Panel 3: data states ---------------------------------------------------

type DataState = 'idle' | 'loading' | 'data' | 'empty' | 'error'
const dataState = ref<DataState>('idle')
const customerData = ref<components['schemas']['CustomerLookupDto'] | null>(null)
const dataError = ref<Problem | null>(null)

async function loadCustomers(): Promise<void> {
  dataState.value = 'loading'
  const result = await api.call(
    api.clients.agent.GET('/api/Agent/Customers/lookup', { params: { query: { search: 'a', take: 5 } } }),
    { toastOnError: false },
  )
  if (result.ok) {
    const first = result.data[0]
    if (first === undefined) {
      dataState.value = 'empty'
      return
    }
    customerData.value = first
    dataState.value = 'data'
  } else {
    dataError.value = result.problem
    dataState.value = 'error'
  }
}

function showEmpty(): void {
  dataState.value = 'empty'
}

async function loadMissingRoute(): Promise<void> {
  dataState.value = 'loading'
  try {
    const response = await fetch('/api/Agent/DoesNotExist', {
      headers: { [CorrelationHeader]: newCorrelationId() },
    })
    const body: unknown = await response.json().catch(() => undefined)
    if (!response.ok) {
      dataError.value = toProblem(response.status, body, response.headers)
      dataState.value = 'error'
      return
    }
    dataState.value = 'data'
  } catch (error) {
    dataError.value = networkProblem(error)
    dataState.value = 'error'
  }
}
</script>
