/** Mirrors backend ProblemTypes.cs verbatim. Never rename a value; Story 05 maps them to en/ar messages. */
export const ProblemTypes = {
  Validation: 'urn:crm:problem:validation',
  BadRequest: 'urn:crm:problem:bad-request',
  Unauthorized: 'urn:crm:problem:unauthorized',
  Forbidden: 'urn:crm:problem:forbidden',
  NotFound: 'urn:crm:problem:not-found',
  MethodNotAllowed: 'urn:crm:problem:method-not-allowed',
  Conflict: 'urn:crm:problem:conflict',
  UnsupportedMediaType: 'urn:crm:problem:unsupported-media-type',
  DomainRule: 'urn:crm:problem:domain-rule',
  RateLimited: 'urn:crm:problem:rate-limited',
  Internal: 'urn:crm:problem:internal',
  DependencyUnavailable: 'urn:crm:problem:dependency-unavailable',
  /** Client-side only: the request never got an HTTP response (offline, DNS, aborted). */
  Network: 'urn:crm:problem:network',
} as const
export type ProblemType = (typeof ProblemTypes)[keyof typeof ProblemTypes]

export interface Problem {
  type: string // a ProblemType, or an unknown URN from a newer backend; never narrowed by assertion
  title: string
  status: number // 0 for Network
  detail: string | null
  code: string | null
  correlationId: string | null
  traceId: string | null
  /** Field errors keyed by the request's camelCase property path ("name", "lines[0]", "address.city"). */
  fieldErrors: Record<string, string[]>
  retryAfterSeconds: number | null
}

// Mirrors ProblemTypes.ForStatus (backend) so a body with no `type` (a non-JSON error from a proxy) still
// gets a sensible classification.
function typeForStatus(status: number): string {
  switch (status) {
    case 400:
      return ProblemTypes.BadRequest
    case 401:
      return ProblemTypes.Unauthorized
    case 403:
      return ProblemTypes.Forbidden
    case 404:
      return ProblemTypes.NotFound
    case 405:
      return ProblemTypes.MethodNotAllowed
    case 409:
      return ProblemTypes.Conflict
    case 415:
      return ProblemTypes.UnsupportedMediaType
    case 422:
      return ProblemTypes.DomainRule
    case 429:
      return ProblemTypes.RateLimited
    case 503:
      return ProblemTypes.DependencyUnavailable
    default:
      return status >= 500 ? ProblemTypes.Internal : ProblemTypes.BadRequest
  }
}

// Mirrors ProblemTypes.TitleFor (backend), used only when the body carries no title of its own.
function titleForType(type: string): string {
  switch (type) {
    case ProblemTypes.Validation:
      return 'Validation failed'
    case ProblemTypes.BadRequest:
      return 'Bad request'
    case ProblemTypes.Unauthorized:
      return 'Authentication required'
    case ProblemTypes.Forbidden:
      return 'Forbidden'
    case ProblemTypes.NotFound:
      return 'Not found'
    case ProblemTypes.MethodNotAllowed:
      return 'Method not allowed'
    case ProblemTypes.Conflict:
      return 'Conflict'
    case ProblemTypes.UnsupportedMediaType:
      return 'Unsupported media type'
    case ProblemTypes.DomainRule:
      return 'Business rule violated'
    case ProblemTypes.RateLimited:
      return 'Too many requests'
    case ProblemTypes.DependencyUnavailable:
      return 'Dependency unavailable'
    case ProblemTypes.Network:
      return 'Network error'
    default:
      return 'Internal error'
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null
}

function readString(obj: Record<string, unknown>, key: string): string | null {
  const value = obj[key]
  return typeof value === 'string' ? value : null
}

// PascalCase ("Name", "Lines[0]", "Address.City") -> camelCase ("name", "lines[0]", "address.city").
// Every dot/bracket segment is lower-cased on its first character. An empty key ("" — a body-level
// binding error) becomes "_form" (Probe 12).
function toFieldKey(key: string): string {
  if (key === '') return '_form'
  return key
    .split('.')
    .map((segment) => segment.replace(/^[A-Za-z]/, (char) => char.toLowerCase()))
    .join('.')
}

function readFieldErrors(obj: Record<string, unknown>): Record<string, string[]> {
  const errors = obj['errors']
  if (!isRecord(errors)) return {}
  const result: Record<string, string[]> = {}
  for (const [key, value] of Object.entries(errors)) {
    if (!Array.isArray(value)) continue
    const messages = value.filter((item): item is string => typeof item === 'string')
    if (messages.length === 0) continue
    result[toFieldKey(key)] = messages
  }
  return result
}

function readRetryAfterSeconds(headers: Headers): number | null {
  const value = headers.get('Retry-After')
  if (value === null) return null
  const parsed = Number.parseInt(value, 10)
  return Number.isFinite(parsed) ? parsed : null
}

export function toProblem(status: number, body: unknown, headers: Headers): Problem {
  const record = isRecord(body) ? body : {}
  const type = readString(record, 'type') ?? typeForStatus(status)
  const title = readString(record, 'title') ?? titleForType(type)
  const correlationId = readString(record, 'correlationId') ?? headers.get('X-Correlation-Id')

  return {
    type,
    title,
    status,
    detail: readString(record, 'detail'),
    code: readString(record, 'code'),
    correlationId,
    traceId: readString(record, 'traceId'),
    fieldErrors: readFieldErrors(record),
    retryAfterSeconds: readRetryAfterSeconds(headers),
  }
}

/** The request never got an HTTP response at all: offline, DNS failure, or an aborted fetch. */
export function networkProblem(error: unknown): Problem {
  const detail = error instanceof Error ? error.message : String(error)
  return {
    type: ProblemTypes.Network,
    title: titleForType(ProblemTypes.Network),
    status: 0,
    detail,
    code: null,
    correlationId: null,
    traceId: null,
    fieldErrors: {},
    retryAfterSeconds: null,
  }
}

export function isProblem(value: unknown): value is Problem {
  if (!isRecord(value)) return false
  return (
    typeof value['type'] === 'string' &&
    typeof value['title'] === 'string' &&
    typeof value['status'] === 'number' &&
    isRecord(value['fieldErrors'])
  )
}
