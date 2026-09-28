# Story 12 — Customer notes and attachments

Story intake for the **Customer Management** feature of the Customer Support CRM.
Plan it with `/squad-plan .squad/stories/customer-management/customer-notes-and-attachments/intake.md`.

---

## Feature

- **Feature name (display):** Customer Management
- **Feature slug (folder under `plans/`):** `customer-management`
- **Module (backend):** `Customers`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-12`
- **Work item type:** `User Story`
- **Status:** `New`
- **Assignee:** ``
- **Labels:** `crm`, `customer-management`

---

## Title

```
Customer notes and attachments
```

---

## Description

Free-form notes and file attachments against a customer, and the file storage abstraction that ticket messages,
knowledge-base articles, and portal uploads all reuse later.

- `CustomerNote` as a child entity inside the customer aggregate: body, visibility (internal or shared with the
  customer), pinned flag, author, and timestamps. Editing is restricted to the author within a configurable
  window, after which notes are append-only.
- An `IFileStorage` abstraction with a local-disk implementation for development and an S3-compatible or Azure
  Blob implementation for deployment, selected by configuration. Files are stored under a content-addressed key,
  never under the original filename.
- `Attachment` as a value-object-backed entity: original filename, content type, byte size, checksum, storage
  key, uploaded-by, and scan status. It is referenced by a customer, a note, and later by a ticket message.
- Upload constraints: an allow-list of content types verified by sniffing the file's magic bytes rather than
  trusting the declared type or extension, a maximum size per file and per request, and a filename sanitiser.
- Download through a short-lived signed URL or a streaming endpoint that re-checks the caller's scope and
  permission on every request. A storage key alone must never grant access.
- Optional antivirus hook: files land in a quarantine state and become downloadable only once scanning passes,
  with the scanner pluggable and a no-op implementation for development.
- Frontend: a notes panel with rich-text-lite editing and pinning, and a drag-and-drop uploader with progress,
  retry, thumbnail previews for images, and inline PDF preview.

## Acceptance criteria

- [ ] A note can be created, edited by its author within the configured window, and is append-only afterwards.
- [ ] An internal note is never returned by any portal-facing endpoint, verified by requesting it with a portal
      token.
- [ ] Pinned notes sort above unpinned ones regardless of date.
- [ ] Uploading a file whose magic bytes do not match its declared content type is rejected, even when the
      extension looks correct.
- [ ] Uploading a file over the configured size limit is rejected before the whole body is buffered in memory.
- [ ] A file uploaded twice is stored once by checksum and referenced twice.
- [ ] Requesting a download with a valid storage key but without the required permission or scope returns 403.
- [ ] A signed download URL expires after the configured window and is rejected afterwards.
- [ ] With the antivirus hook enabled, a quarantined file cannot be downloaded until scanning passes.
- [ ] The uploader shows progress, survives a failed chunk with a retry, and reports a rejected file with a
      localised reason in both `en` and `ar`.
- [ ] Deleting a note or attachment is a soft delete and remains visible in the audit log.

## Dependencies

- **Blocked by / related ids:** CRM-11.
- **Depends on code areas or other stories:** the customer aggregate from story 11. Stories 18, 37, and 48 reuse
  the `IFileStorage` abstraction and attachment model delivered here.

## Technical hints

- Content-address the storage key by checksum plus a tenant prefix. It deduplicates uploads and makes the key
  useless as a capability on its own.
- Stream uploads straight to storage; do not buffer the whole file in memory or the size limit is theatre.
- File upload is the most common source of remote code execution in a CRM and there is no test suite here. Verify
  the magic-byte check by hand with a renamed executable before calling this done.

## Out of scope

- Ticket message attachments as a feature surface. Story 18 consumes this abstraction.
- Image processing beyond thumbnail generation.
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
