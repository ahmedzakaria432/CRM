# backend-unit-tests — plan overview

Entry point for the **backend-unit-tests** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 60 | [60-story-unit-tests-foundation-and-building-blocks.md](60-story-unit-tests-foundation-and-building-blocks.md) | Unit tests — foundation and building blocks | UT-01 | — |
| 61 | [61-story-unit-tests-for-organisation-module.md](61-story-unit-tests-for-organisation-module.md) | Unit tests — Organisation module | UT-02 | 60 |
| 62 | [62-story-unit-tests-for-identity-module.md](62-story-unit-tests-for-identity-module.md) | Unit tests — Identity module | UT-03 | 60 |
| 63 | [63-story-unit-tests-for-customers-module.md](63-story-unit-tests-for-customers-module.md) | Unit tests — Customers module | UT-04 | 60 |
| 64 | [64-story-unit-tests-for-tickets-module.md](64-story-unit-tests-for-tickets-module.md) | Unit tests — Tickets module | UT-05 | 60 |

## Dependency notes

UT-02..UT-05 (plans 61–64: Organisation, Identity, Customers, Tickets) depend on 60 and run one at a time; they share the solution build and the same `dotnet test` run.
