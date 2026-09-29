import createClient from 'openapi-fetch'
import type { Client } from 'openapi-fetch'
import type { paths as AgentPaths } from './generated/agent'
import type { paths as PortalPaths } from './generated/portal'
import type { paths as AdminPaths } from './generated/admin'
import type { paths as PublicPaths } from './generated/public'

export interface CrmClients {
  agent: Client<AgentPaths>
  portal: Client<PortalPaths>
  admin: Client<AdminPaths>
  public: Client<PublicPaths>
}

/**
 * One `openapi-fetch` client per Area, all sharing the same wrapped `fetch` (correlation id, bearer token,
 * 401 refresh/retry — see `createCrmFetch`).
 *
 * `baseUrl` is `''` in the browser: requests go to same-origin `/api`, proxied to the API in `pnpm dev`
 * (Probe 9) and by a reverse proxy in production. On the server, a relative URL has no origin to resolve
 * against, so SSR passes `useRuntimeConfig().apiBase` instead.
 */
export function createCrmClients(
  baseUrl: string,
  fetch: (input: Request) => Promise<Response>,
): CrmClients {
  return {
    agent: createClient<AgentPaths>({ baseUrl, fetch }),
    portal: createClient<PortalPaths>({ baseUrl, fetch }),
    admin: createClient<AdminPaths>({ baseUrl, fetch }),
    public: createClient<PublicPaths>({ baseUrl, fetch }),
  }
}
