# backend-unit-tests — plan overview

Entry point for the **backend-unit-tests** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 60 | [60-story-unit-tests-foundation-and-building-blocks.md](60-story-unit-tests-foundation-and-building-blocks.md) | Unit tests — foundation and building blocks | UT-01 | — |

## Dependency notes

UT-02..UT-05 (plans 61–64: Organisation, Identity, Customers, Tickets) depend on 60 and run one at a time; they share the solution build and the same `dotnet test` run.
