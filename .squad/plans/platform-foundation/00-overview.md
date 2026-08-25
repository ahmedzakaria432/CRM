# platform-foundation — plan overview

Entry point for the **Platform Foundation** feature. This is **feature 1 of 12** in the project planning order
(see [`../00-index.md`](../00-index.md)). Stories execute in `NN` order.

## Stories

| NN | Intake | Title | Plan file | Depends on |
|----|--------|-------|-----------|------------|
| 01 | [`backend-solution-scaffold-with-ddd-and-clean-architecture`](../../stories/platform-foundation/backend-solution-scaffold-with-ddd-and-clean-architecture/intake.md) | Backend solution scaffold with DDD and Clean Architecture | [`01-story-backend-solution-scaffold-with-ddd-and-clean-architecture.md`](01-story-backend-solution-scaffold-with-ddd-and-clean-architecture.md) | none. This is the first story |
| 02 | [`persistence-baseline-with-auditing-and-soft-delete`](../../stories/platform-foundation/persistence-baseline-with-auditing-and-soft-delete/intake.md) | Persistence baseline with auditing and soft delete | _not planned yet_ | CRM-01 |
| 03 | [`cross-cutting-concerns-error-handling-logging-and-caching`](../../stories/platform-foundation/cross-cutting-concerns-error-handling-logging-and-caching/intake.md) | Cross cutting concerns error handling logging and caching | _not planned yet_ | CRM-01, CRM-02 |
| 04 | [`frontend-scaffold-with-nuxt-typescript-and-tailwind`](../../stories/platform-foundation/frontend-scaffold-with-nuxt-typescript-and-tailwind/intake.md) | Frontend scaffold with Nuxt TypeScript and Tailwind | _not planned yet_ | CRM-01, for the OpenAPI document |
| 05 | [`bilingual-arabic-and-english-support-with-rtl-layout`](../../stories/platform-foundation/bilingual-arabic-and-english-support-with-rtl-layout/intake.md) | Bilingual Arabic and English support with RTL layout | _not planned yet_ | CRM-03, CRM-04 |
| 06 | [`multi-department-and-multi-branch-organisation-scoping`](../../stories/platform-foundation/multi-department-and-multi-branch-organisation-scoping/intake.md) | Multi department and multi branch organisation scoping | _not planned yet_ | CRM-02, CRM-04 |

## Dependency notes

_Plan the stories above in `NN` order. Replace "not planned yet" with the generated plan path as
each `/squad-plan` run completes._

### Story 01 — decisions carried forward

- **MediatR pinned to 12.4.1 (Apache-2.0).** 13.x and later are RPL-1.5 or a paid Lucky Penny Software licence;
  RPL-1.5 would require releasing this CRM's source. Do not bump without a licence decision.
- **`dotnet ef` pinned to 10.0.11 via a local tool manifest.** The globally installed 10.0.3 is older than the
  EF Core packages story 02 needs, and EF errors on that skew rather than warning.
- **`pnpm` is not installed on the dev machine.** It blocks Story 04, not this story.
