// Matches CorrelationIdMiddleware.HeaderName and IsAcceptable (backend): <= 64 chars of [A-Za-z0-9._:-].
export const CorrelationHeader = 'X-Correlation-Id'

/** A fresh UUID (36 chars of [0-9a-f-]) — always accepted by CorrelationIdMiddleware.IsAcceptable. */
export function newCorrelationId(): string {
  return crypto.randomUUID()
}
