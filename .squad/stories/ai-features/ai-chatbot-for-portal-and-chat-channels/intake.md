# Story 46 — AI chatbot for portal and chat channels

> **MVP re-scope (2026-09-29): deferred post-MVP.** Not part of the 2-day MVP (customers, tickets, portal, bilingual, responsive, multi-department/branch, branding). Keep for later; do not plan it now. See `.squad/BACKLOG.md`, "MVP re-scope".

Story intake for the **AI Features** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/ai-features/ai-chatbot-for-portal-and-chat-channels/intake.md`.

---

## Feature

- **Feature name (display):** AI Features
- **Feature slug (folder under `plans/`):** `ai-features`
- **Module (backend):** `Ai`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-46`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `ai-features`

---

## Title

```
AI chatbot for portal and chat channels
```

---

## Description

A customer-facing assistant that answers from the knowledge base and hands over to a human the moment it should.
It talks to real customers unsupervised, so its restraint matters more than its fluency.

- A conversational agent over story 45's retrieval, available on the help centre from story 40, the portal from
  story 48, the live chat widget from story 26, and optionally WhatsApp through story 24.
- Multi-turn conversation with bounded memory, able to ask one clarifying question before answering, and always
  answering in the customer's language including a mid-conversation language switch.
- Strictly grounded answers with citations. When retrieval is insufficient the bot says it does not know and
  offers a human, rather than producing a confident guess.
- Handover rules, explicit and generous: the customer asks for a human, sentiment turns negative, the same question
  repeats twice, a sensitive topic is raised (billing dispute, complaint, legal, data deletion, security), or
  confidence is low. Handover carries the full transcript into a ticket with an AI-generated summary.
- Guardrails: never state a price, policy, commitment, or date absent from the source material; never process a
  transaction; never reveal internal content, another customer's data, or system details; refuse and hand over on
  any attempt to extract them.
- Prompt-injection resistance, since every input is untrusted customer text. Instructions embedded in customer
  messages must not alter behaviour.
- Authenticated mode: for a signed-in portal customer, the bot may read that customer's own tickets to answer
  status questions, scoped strictly to them, and nothing else.
- Availability and framing: identify as an automated assistant on first contact, offer a human at every turn,
  respect business hours for the handover path, and explain what happens when no agent is available.
- Analytics: containment rate, handover rate and reason, satisfaction after a bot conversation, and unanswered
  questions feeding story 38's content-gap report.

## Acceptance criteria

- [ ] The bot identifies itself as automated on first contact and offers a human path at every turn.
- [ ] An answerable question is answered with citations resolving to published public content.
- [ ] An unanswerable question produces an explicit do-not-know plus a handover offer, never a guess.
- [ ] Asking for a human hands over immediately, with the transcript and a summary landing on a ticket.
- [ ] Negative sentiment, a repeated question, or a sensitive topic triggers handover without the customer asking.
- [ ] The bot answers in the customer's language and follows a mid-conversation language switch.
- [ ] A prompt-injection attempt in a customer message does not change the system instruction or reveal it,
      verified by hand against a recorded set of attempts.
- [ ] The bot never returns internal-only content, another customer's data, or system details, verified by
      attempting each directly.
- [ ] An authenticated customer can ask about their own ticket status and receives only their own data.
- [ ] Outside business hours, the handover path explains what will happen and creates a ticket.
- [ ] Containment rate, handover reasons, and post-conversation satisfaction are all recorded and reportable.
- [ ] With AI unavailable, the entry point degrades to knowledge-base search plus ticket creation, with no error
      shown to the customer.
- [ ] An adversarial pass covering pricing, policy, commitments, competitor questions, abuse, and off-topic
      requests is walked by hand and recorded before release.

## Dependencies

- **Blocked by / related ids:** CRM-41, CRM-45, CRM-40, CRM-26, CRM-48.
- **Depends on code areas or other stories:** the AI seam from story 41, retrieval from story 45, the help centre
  from story 40, live chat from story 26, and the portal from story 48.

## Technical hints

- This is the only AI surface talking to customers unsupervised and it has no test suite behind it. Bias every
  ambiguous case toward handover; a needless handover costs an agent two minutes, a wrong confident answer costs
  a customer relationship.
- Keep the authenticated data path narrow and explicit: this customer's own tickets, nothing else, enforced by the
  same scope filter as every other query.
- Record every conversation with its retrieval context. Without it, a complaint about what the bot said is
  unanswerable.

## Out of scope

- Transactional actions such as processing refunds or changing orders.
- Voice.
- Any test project.

---

## Attachments

None.

---

## Stack and conventions (applies to every story)

- **Backend:** .NET 10 modular monolith. One module per bounded context under `backend/src/Modules/<Module>/` (`Domain` <- `Application` <- `Infrastructure` / `Presentation`, plus a public `Contracts` project), Domain-Driven Design with Clean Architecture inside each module, one host (`Crm.Api`). CQRS with MediatR, FluentValidation, EF Core 10 on SQL Server with one DbContext and schema per module, MVC controllers in per-persona Areas, `Result<T>` returns, RFC 9457 problem details. The clock is read only through `IDateTime`; `System.DateTime` is banned.
- **Module boundaries:** a module reaches another only through that module's `Contracts` - a synchronous API interface for a read, an integration event (via the outbox) for "this happened". No cross-module project references, foreign keys, joins, or shared entities; one command, one module, one transaction. The build fails with `CRM0001` on a forbidden reference. The module catalogue in `CLAUDE.md` names the module each story belongs to.
- **DDD rules:** aggregate roots guard their own invariants and are always valid; no public setters, state changes go through intention-revealing methods; children are reachable only through their root; other aggregates are referenced by strongly-typed id, never by navigation property; one transaction touches one aggregate and cross-aggregate effects travel as domain events inside the module, and as integration events across modules; anything without identity is an immutable value object; type and method names use this story description as the ubiquitous language.
- **Frontend:** Nuxt 4 with Vue 3 script-setup, TypeScript strict, Tailwind utilities only, Pinia for cross-route state, `@nuxtjs/i18n` for `en` and `ar`.
- **Bilingual and RTL:** every user-visible string is an i18n key in both `en` and `ar`; layouts use logical Tailwind utilities (`ms-`, `me-`, `ps-`, `pe-`, `text-start`), never `ml-`, `mr-`, or `text-left`.
- **Scoping:** every read and write is scoped by tenant, department, and branch through `ICurrentUserService`. Admin-only endpoints are the sole exception and must be explicitly authorised.
- **Auditing:** entities derive from `AuditableEntity`; deletes are soft deletes behind a global query filter; permission-relevant changes write an audit-log entry.
- **No automated tests.** This project deliberately ships without unit, integration, or component test suites. Do not create `tests/` folders, `*.Tests.csproj`, `*.spec.ts`, or `*.test.ts` files, and do not add a test framework to any project file. In their place: keep `TreatWarningsAsErrors`, nullable, and TypeScript `strict` on and never silence them; enforce invariants in the aggregate, which is where a unit test assertion would have gone; and commit a worked request per endpoint to `backend/requests/<feature>.http`.
- **Definition of done:** `dotnet build backend/Crm.sln` clean with zero warnings, an EF migration included whenever the schema moves, `pnpm typecheck` / `pnpm lint` / `pnpm build` clean, every new endpoint exercised from its `.http` file against a running API, every new screen walked in the browser in both `en` (LTR) and `ar` (RTL), and loading, empty, and error states present for every data-backed view.
- Full conventions, including the DDD and testing policy in detail, live in `CLAUDE.md` at the repository root.
