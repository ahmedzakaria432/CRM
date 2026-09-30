import type { InjectionKey } from 'vue'
import type { ApiResult } from '~/composables/useApi'
import { problemMessage } from '~/api/problem-message'
import { ProblemTypes } from '~/api/problem'
import { offeredActions } from '~/utils/tickets'
import { useTicket } from './useTicket'
import type { TicketDto } from './types'

/** The ticket page's shared state (D6): the ticket, the action in flight, the inline refusal, the thread version. */
export function useTicketWorkspace(id: string, valid: boolean) {
  const { $i18n } = useNuxtApp()
  const translate = { t: (key: string) => $i18n.t(key), te: (key: string) => $i18n.te(key) }
  const api = useApi()
  const toasts = useToasts()
  const { can } = useCan()
  const { data: ticket, status, error, refresh } = useTicket(id).load(valid)

  const version = ref(0) // TicketThread loads newer entries when it changes (D15)
  const busy = ref<string | null>(null) // the action in flight; every action button is disabled meanwhile
  const actionError = ref<string | null>(null)
  const customerLabel = ref<string | null>(null)
  const offered = computed(() => offeredActions(ticket.value?.allowedActions, can))

  function publish(dto: TicketDto): void {
    ticket.value = dto // R7: the next command sends dto.rowVersion
    version.value += 1
  }

  async function run(
    action: string,
    request: () => Promise<ApiResult<TicketDto>>,
    successKey = 'tickets.actions.done',
  ): Promise<TicketDto | null> {
    if (busy.value !== null) return null
    busy.value = action
    actionError.value = null
    const result = await request()
    busy.value = null
    if (result.ok) {
      publish(result.data)
      toasts.push({ tone: 'success', title: $i18n.t(successKey) })
      return result.data
    }
    const problem = result.problem
    if (problem.code === 'Persistence.ConcurrencyConflict') {
      api.toast(problem)
    } else if (
      [ProblemTypes.Conflict, ProblemTypes.DomainRule, ProblemTypes.Validation, ProblemTypes.NotFound].some(
        (type) => type === problem.type,
      )
    ) {
      actionError.value = problemMessage(problem, translate).description ?? problem.title
    } else {
      api.toast(problem)
      return null
    }
    await refresh()
    version.value += 1
    return null
  }

  return { id, ticket, status, error, refresh, version, busy, actionError, customerLabel, offered, publish, run }
}

export type TicketWorkspace = ReturnType<typeof useTicketWorkspace>
export const ticketWorkspaceKey: InjectionKey<TicketWorkspace> = Symbol('ticket-workspace')

/** For the workspace's child components. The page provides it; a missing provider is a programming error. */
export function useTicketWorkspaceContext(): TicketWorkspace {
  const workspace = inject(ticketWorkspaceKey, null)
  if (workspace === null) throw new Error('useTicketWorkspaceContext() outside the ticket page')
  return workspace
}
