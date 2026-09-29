# Story 05 — Bilingual Arabic and English support with RTL layout

## Prerequisites

- **Stories 01–04 are implemented and committed** (latest `985ec8b`). Read [`00-overview.md`](00-overview.md),
  "Story 03 — decisions carried forward" (`ProblemTypes` URNs are never renamed; Story 05 maps them to `en`/`ar`)
  and "Story 04 — decisions carried forward" (pins, pnpm 12 settings in `pnpm-workspace.yaml`, the lint rules
  Story 05 **must not relax**, `shellCopy` hand-off).
- **Module:** no domain module. Backend work is confined to **BuildingBlocks** (`Crm.BuildingBlocks.Application`,
  `Crm.BuildingBlocks.Presentation`) and the host's middleware order in `Crm.Api/Program.cs`. **No module project,
  no schema, no migration changes.** The Sample module is only *called* (its validator produces the Arabic message).
- **MVP scope (2026-09-29 re-scope).** This story also owns **web and mobile friendly** for the shell: agent
  layout, portal layout, and the UI kit are walked at 375 / 768 / 1280 in `en` and `ar`.
- **Verified on this machine during planning (2026-09-29).** Re-check before starting:
  - Node 24.15.0, pnpm **12.6.0** (shim already in `%APPDATA%\npm`; set `COREPACK_ENABLE_DOWNLOAD_PROMPT=0`),
    .NET SDK **10.0.401**, Edge at `C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe` (no Chrome).
  - New frontend pins (npm registry mirror, 2026-09-29), **exact**: `@nuxtjs/i18n` **10.6.0** (`latest`; brings
    `vue-i18n` ^11.4.8), `@fontsource/ibm-plex-sans-arabic` **5.3.0** (OFL-1.1). No new backend package: request
    localisation ships in `Microsoft.AspNetCore.App`, resource lookup is `System.Resources.ResourceManager`, and
    FluentValidation **12.1.1** already carries an Arabic translation.
- **Probes run during planning** (throwaway copies in the session scratchpad, **not in the repo**). Results that
  shaped the design are marked **Probe N:** inline:
  1. **Middleware order decides whether problem titles are localised.** A net10 web app with
     `UseRequestLocalization` registered **after** `UseExceptionHandler`: the handler saw culture `en-US` and wrote
     the English title for an `Accept-Language: ar` request, because `CultureInfo.CurrentUICulture` set inside a
     downstream middleware does not flow back out to the caller. Registered **before** `UseExceptionHandler`: the
     handler saw `ar` and wrote `خطأ داخلي`. The same holds for `UseStatusCodePages` (bare 401/404).
  2. **FluentValidation 12.1.1 localises its built-in messages from `CurrentUICulture` with no configuration.**
     `NotEmpty()` under `ar` → `'Name' لا يجب أن يكون فارغاً.`; under `en` → `'Name' must not be empty.`. The
     property name stays `'Name'` (Latin) — accepted for the MVP (Edge Cases).
  3. **Culture negotiation** with supported cultures `en`, `ar` and only the `Accept-Language` provider:
     `ar` → `ar`; `ar-SA,en;q=0.5` → `ar` (parent-culture fallback); `fr` → `en`; no header → `en`.
  4. **`.resx` resources need no package and no `.csproj` entry.** `Resources/Msg.resx` + `Resources/Msg.ar.resx`
     in an SDK project with `TreatWarningsAsErrors` + `AnalysisLevel latest-recommended` built clean, produced
     `ar/<Assembly>.resources.dll`, and `new ResourceManager("<RootNamespace>.Resources.Msg", assembly)
     .GetString(key, CultureInfo.CurrentUICulture)` returned the Arabic value under `ar`.
  5. **`@nuxtjs/i18n` 10.6.0 installs cleanly** on the Story 04 pins with pnpm 12.6.0 (27 s warm). It pulls a
     second esbuild (0.25.12), already covered by `allowBuilds: esbuild: true`. **No new `allowBuilds` entry.** The
     only peer warning is the known `cac@7.0.0` one from Story 04. `pnpm typecheck`, `eslint .`, `pnpm build` pass.
  6. **Detection order is cookie → `Accept-Language` → `en`**, with `strategy: 'no_prefix'` and
     `detectBrowserLanguage: { useCookie: true, cookieKey: 'crm-locale', redirectOn: 'no prefix' }` (SSR, curl):
     `Accept-Language: ar-SA` → `<html lang="ar" dir="rtl">` + `Set-Cookie: crm-locale=ar`; `fr` or none → `en`;
     cookie `crm-locale=ar` with `Accept-Language: en` → `ar`; a tampered cookie `crm-locale=zz` falls through to
     `Accept-Language`.
  7. **`lang`/`dir` belong in a plugin, not `app.vue`.** A plugin calling `useHead({ htmlAttrs: { lang, dir } })`
     from `nuxtApp.$i18n.localeProperties` gives `lang="ar" dir="rtl"` on SSR pages **and on the 404 page**
     (`error.vue` renders outside `app.vue`, so a `useHead` in `app.vue` never runs there). Client-side
     `setLocale('ar')` in headless Edge flipped `<html>` to `lang="ar" dir="rtl"` with no navigation and wrote the
     cookie. **The `<html>` attributes update a tick after `setLocale` resolves** (read immediately after
     `await setLocale(); await nextTick()` they were still `en/ltr`). Code must never read `document.dir` to decide
     behaviour right after a switch (Tabs reads computed direction at keypress time instead, Task F10).
  8. **Several JSON files per locale merge.** `locales[].files: ['en/errors.json', 'en/shell.json']` built from
     `readdirSync('i18n/locales/en')` inside `nuxt.config.ts` loads both; `t('errors.problem.internal')` and
     `t('shell.appName')` resolve in both locales.
  9. **Key-parity script** (`node scripts/check-locales.ts`, Node 24 strips the types) exits 0 on matching sets and
     1 with `ar/shell.json: missing key shell.switchTo` when a key is removed.
  10. **`vue/no-bare-strings-in-template`** (eslint-plugin-vue 10.11.1, already installed through `@nuxt/eslint`)
      flags exactly three literals in the current app outside `/_dev`: the `▾` glyph in `ui/Select.vue`, the
      `title="Nothing here yet"` in `pages/agent/index.vue`, and the tagline in `pages/index.vue`.
  11. **Fonts:** importing `@fontsource/ibm-plex-sans-arabic/{400,600}.css` through `nuxt.config.ts` `css:` builds,
      emits the woff2 files under `.output/public/_nuxt/`, and each `@font-face` carries a `unicode-range`, so an
      English-only page downloads only the Latin subset.
  12. **`Intl` output** (Node 24 ICU): `ar-SA-u-ca-gregory` → digits `٢٩‏/٠٩‏/٢٠٢٦، ٥:٠٥ م`,
      `١٬٢٣٤٬٥٦٧٫٥`, `‏١٬٢٣٤٫٥٠ ر.س.‏`, `قبل ٣ ساعات`; plain `ar` → **Latin** digits in Node 24 but Arabic-Indic in
      other ICU builds. **Pin the full tag** (Task F8) so SSR (Node) and the browser agree. `en-GB` →
      `29 Sept 2026, 17:05`, `SAR 1,234.50`.
- **Coordination:**
  - **Story 06** adds tenant/department/branch scope. The cache-key shape stays as Story 03 fixed it: **this story
    does not add the culture to cache keys** (Edge Cases, "Cached queries").
  - **Story 07** wires the user-profile language preference once `GET /me` exists: it calls `setLocale()` after
    sign-in. Locale persistence stays the `crm-locale` cookie.
  - **Story 58** (branding) may override `--crm-font-sans`; the Arabic face stays first in the stack.
  - **Every later frontend story** adds its own `i18n/locales/{en,ar}/<domain>.json` pair and never edits
    `shell.json` for feature copy.

---

## Story Goal

Arabic and English become equal, first-class locales across the shell, the UI kit, and the API's error contract,
before any feature screen exists.

When this story is done:

1. `@nuxtjs/i18n` runs with `en` (LTR) and `ar` (RTL). Locale files live in
   `frontend/i18n/locales/{en,ar}/<domain>.json`. Every Story 04 shell string is a key; `app/copy/en.ts` is gone.
2. `<html lang dir>` follows the active locale on the server and in the browser, including the error page.
   Switching flips `dir` without a reload.
3. The locale persists in the `crm-locale` cookie (cookie first, `Accept-Language` last, `en` default).
4. A language switcher sits in the agent topbar and the portal header.
5. `useFormat()` formats date, time, date-time, relative time, number, SAR currency, and file size over `Intl`, in
   each locale's conventions, with no date library and no Hijri calendar.
6. Arabic renders in **IBM Plex Sans Arabic** (self-hosted) with a system fallback stack; Latin identifiers inside
   Arabic text are bidi-isolated.
7. The API honours `Accept-Language: ar|en`: FluentValidation messages, problem `title`s, and the fixed problem
   `detail`s come back in that language, through `.resx` resources behind `ILocalizationService` in
   `Crm.BuildingBlocks.Application`. The browser sends the UI locale as `Accept-Language` on every API call.
8. `pnpm lint` fails on a physical-direction utility, on a bare string in an app template, and on diverging
   `en`/`ar` key sets.
9. Responsive shell: the agent sidebar becomes a drawer below 768px, the portal header stacks, tables scroll inside
   their container, and no page scrolls horizontally at 375px, in either direction.
10. `/_dev/ui` has a locale switcher, a side-by-side `en`/`ar` formatting table, and a bidi demo.

**Not in scope:** Hijri calendar, localised email/SMS templates, feature copy that does not exist yet, the user
profile language preference (Story 07), branding (Story 58), localising MVC model-binding messages and domain
`Error.Description` text on the backend (the frontend localises by `code`, Task F7), and **any test suite**.

---

## Context — Read These Files First

1. `CLAUDE.md`: "Frontend — Nuxt + Vue 3 + TypeScript + Tailwind" (lines ~192–209: RTL, i18n, `useApi`, `/_dev/ui`
   rules) and "Application, API, and persistence" (lines ~141–190: `Results`, pipeline, `Caching`). Task X1 adds
   bullets to both.
2. [`04-story-frontend-scaffold-with-nuxt-typescript-and-tailwind.md`](04-story-frontend-scaffold-with-nuxt-typescript-and-tailwind.md):
   Task 3 (lint), Task 7 (`shellCopy` hand-off), Task 9 (kit rules: **no copy inside `app/components/ui/`**, every
   label is a prop), Edge Cases on the physical-direction regex.
3. `frontend/eslint.config.mjs` line 6: the `physical` regex **already bans** `left-`, `right-`, `border-l`,
   `border-r`, `rounded-l`, `rounded-r` (and `rounded-tl|tr|bl|br`, `scroll-ml|mr`, `float-*`, `clear-*`), in
   templates (line 20) and script strings (lines 21–25). The intake's "extend the ban" is therefore already met;
   Task F12 keeps it byte-for-byte and adds rules next to it.
4. `frontend/nuxt.config.ts` lines 3–22: `modules`, `css`, `routeRules`, and line 21 `app.head.htmlAttrs.lang: 'en'`
   (removed by Task F2).
5. `frontend/app/copy/en.ts` lines 4–36: every `shellCopy` key. Task F3 turns this object into `shell.json` with
   the **same key paths**. Consumers: `app/app.vue` line 6, `app/layouts/agent.vue` lines 7, 11, 30, 35, 38, 47, 49,
   60–61, 64, 86, `app/layouts/portal.vue` lines 7, 11–12, 20–22, 29, `app/error.vue` lines 3–13, `app/pages/login.vue`
   lines 3–4, `app/pages/index.vue` lines 3–5, `app/pages/agent/index.vue` lines 3–4.
6. `frontend/app/components/ui/Tabs.vue` lines 79–106 (`handleKeydown`, ArrowLeft/ArrowRight hard-wired) and the
   line 49 comment that hands the RTL swap to this story.
7. `frontend/app/components/ui/Drawer.vue` lines 2–10 (the `side` class binding, line 6) and
   `frontend/app/components/ui/ToastRegion.vue` line 2 (`fixed bottom-4 end-4 … w-full max-w-sm`).
8. `frontend/app/layouts/agent.vue` line 10 (`hidden … lg:block` sidebar), line 27 (`lg:hidden` menu button),
   line 35 (app name); `frontend/app/layouts/portal.vue` line 10 (header row).
9. `frontend/app/composables/useApi.ts` lines 24–35 (`maybeToast` uses `problem.title`) and
   `frontend/app/plugins/api.ts` lines 68–74 (`baseFetch`). `frontend/app/api/problem.ts` lines 62–88
   (`titleForType`, English fallback for bodies without a title — stays).
10. `frontend/app/assets/css/tokens.css` lines 76–78 (`--crm-font-sans`, `--crm-font-mono`).
11. `frontend/app/pages/_dev/ui.vue` lines 1–32 (header: index nav, width switcher, theme toggle), lines 455–483
    (`widths`, `sections`), and `frontend/app/components/dev/Section.vue` (light/dark columns).
12. `backend/src/Crm.Api/Program.cs` lines 83–91: the **ORDER MATTERS** block (`CorrelationIdMiddleware`,
    `UseCrmRequestLogging`, `UseExceptionHandler`, `UseStatusCodePages`). Task B2 inserts one line (Probe 1).
13. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Presentation/ErrorHandling/ProblemTypes.cs` lines 57–71
    (`TitleFor`, English literals) and its callers:
    `ErrorHandling/ErrorHandlingServiceCollectionExtensions.cs` line 27,
    `ErrorHandling/GlobalExceptionHandler.cs` lines 18–20 (primary constructor), 67–68, 78, 85, 94, 101, 104–111
    (`Create`), `ApiControllerBase.cs` lines 55 and 62, `RateLimiting/RateLimitingServiceCollectionExtensions.cs`
    lines 61–62.
14. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Application/DependencyInjection.cs` lines 17–50
    (`AddBuildingBlocksApplication`; FluentValidation registered at line 33).
15. `backend/src/Modules/Sample/Crm.Modules.Sample.Application/Commands/CreateSample/CreateSampleCommandValidator.cs`:
    `NotEmpty()` on `Name` — the source of the acceptance-criterion message. **Not edited.**
16. `backend/requests/diagnostics.http`: request style and `@host = http://localhost:5187`. Task B4 mirrors it.
17. `backend/README.md` lines 228–233 ("Error responses": "a fixed English `title`" — becomes wrong, Task X1).

---

## Backend Tasks

All paths below are under `backend/src/BuildingBlocks/` unless they start with `backend/`. Use the **Write tool**
for new files (Arabic text and apostrophes break Bash heredocs on this machine).

### B1 — `ILocalizationService` and resource files (`Crm.BuildingBlocks.Application`)

**Create file: `Crm.BuildingBlocks.Application/Localization/CrmCultures.cs`**

```csharp
namespace Crm.BuildingBlocks.Application.Localization;

/// <summary>The two supported cultures. Neutral on purpose: "ar-SA" would default .NET to the Um Al-Qura
/// (Hijri) calendar, which is out of scope. A request for ar-SA falls back to "ar" (Probe 3).</summary>
public static class CrmCultures
{
    public const string English = "en";
    public const string Arabic = "ar";
    public const string Default = English;
    public static readonly IReadOnlyList<string> All = [English, Arabic];
}
```

**Create file: `Crm.BuildingBlocks.Application/Localization/ILocalizationService.cs`**

```csharp
namespace Crm.BuildingBlocks.Application.Localization;

/// <summary>Server-side text in the request's language (Accept-Language, set by UseCrmRequestLocalization).
/// Reads CultureInfo.CurrentUICulture at call time, so it is a singleton.</summary>
public interface ILocalizationService
{
    /// <summary>"en" or "ar".</summary>
    string CurrentLanguage { get; }

    /// <summary>The resource value for <paramref name="key"/> in the current UI culture, falling back to the
    /// neutral (English) resource, then to the key itself so a missing entry is visible, never an exception.</summary>
    string Get(string key);
}
```

**Create file: `Crm.BuildingBlocks.Application/Localization/LocalizationResource.cs`**:
`public sealed record LocalizationResource(ResourceManager Manager);`, plus a static
`LocalizationServiceCollectionExtensions.AddLocalizationResource(this IServiceCollection services, ResourceManager
manager)` that does `services.AddSingleton(new LocalizationResource(manager))`. Lookup order is registration
order; the building blocks register first. **A module that needs its own messages** (from Story 11 on) adds a
`.resx` pair to its own Application project and calls `AddLocalizationResource` from its `IModule.AddServices`. It
never adds keys to the building-block resource.

**Create file: `Crm.BuildingBlocks.Application/Localization/ResourceLocalizationService.cs`**

```csharp
using System.Globalization;

namespace Crm.BuildingBlocks.Application.Localization;

public sealed class ResourceLocalizationService(IEnumerable<LocalizationResource> resources) : ILocalizationService
{
    private readonly LocalizationResource[] _resources = [.. resources];

    public string CurrentLanguage =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == CrmCultures.Arabic ? CrmCultures.Arabic : CrmCultures.English;

    public string Get(string key)
    {
        foreach (LocalizationResource resource in _resources)
        {
            string? value = resource.Manager.GetString(key, CultureInfo.CurrentUICulture);
            if (value is not null)
            {
                return value;
            }
        }

        return key;
    }
}
```

**Create files: `Crm.BuildingBlocks.Application/Localization/Resources/CrmMessages.resx`** (English, neutral) and
**`CrmMessages.ar.resx`** (Arabic). No `.csproj` change and no `Designer.cs` (Probe 4). Header (both files):

```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <data name="Problem.Title.validation" xml:space="preserve"><value>Validation failed</value></data>
  <!-- … one <data> per row below … -->
</root>
```

Entries. The title key suffix is the URN suffix of the `ProblemTypes` constant. **The English values are exactly
the current `TitleFor` literals** (`ProblemTypes.cs` lines 59–70) and the current detail literals, so `en`
responses do not change byte-for-byte:

| Key | English (`CrmMessages.resx`) | Arabic (`CrmMessages.ar.resx`) |
|---|---|---|
| `Problem.Title.validation` | Validation failed | فشل التحقق من صحة البيانات |
| `Problem.Title.bad-request` | Bad request | طلب غير صالح |
| `Problem.Title.unauthorized` | Authentication required | يلزم تسجيل الدخول |
| `Problem.Title.forbidden` | Forbidden | ليست لديك صلاحية |
| `Problem.Title.not-found` | Not found | غير موجود |
| `Problem.Title.method-not-allowed` | Method not allowed | الطريقة غير مسموح بها |
| `Problem.Title.conflict` | Conflict | تعارض في البيانات |
| `Problem.Title.unsupported-media-type` | Unsupported media type | نوع المحتوى غير مدعوم |
| `Problem.Title.domain-rule` | Business rule violated | مخالفة لقاعدة عمل |
| `Problem.Title.rate-limited` | Too many requests | طلبات كثيرة جداً |
| `Problem.Title.internal` | Internal error | خطأ داخلي |
| `Problem.Title.dependency-unavailable` | Dependency unavailable | خدمة مرتبطة غير متاحة |
| `Problem.Detail.Validation` | One or more validation errors occurred. | حدث خطأ واحد أو أكثر في التحقق من صحة البيانات. |
| `Problem.Detail.Concurrency` | The record was modified by someone else. Reload and try again. | عدّل مستخدم آخر هذا السجل. أعد التحميل وحاول مرة أخرى. |
| `Problem.Detail.Unreadable` | The request could not be read. | تعذّرت قراءة الطلب. |
| `Problem.Detail.DependencyUnavailable` | A downstream service is unavailable. Try again shortly. | إحدى الخدمات المرتبطة غير متاحة. حاول مرة أخرى بعد قليل. |
| `Problem.Detail.Unexpected` | An unexpected error occurred. | حدث خطأ غير متوقع. |
| `Problem.Detail.RateLimited` | Too many requests. Retry after the time in the Retry-After header. | طلبات كثيرة جداً. أعد المحاولة بعد المدة المحددة في الترويسة Retry-After. |

**File: `Crm.BuildingBlocks.Application/DependencyInjection.cs`**, after line 33 (`AddValidatorsFromAssemblies`):

```csharp
        // Story 05: server-side text follows Accept-Language. FluentValidation's built-in messages localise
        // themselves from CurrentUICulture (Probe 2); these resources cover problem titles and fixed details.
        services.AddLocalizationResource(new ResourceManager(
            "Crm.BuildingBlocks.Application.Localization.Resources.CrmMessages", typeof(DependencyInjection).Assembly));
        services.AddSingleton<ILocalizationService, ResourceLocalizationService>();
```

Add `using System.Resources;` and `using Crm.BuildingBlocks.Application.Localization;` (the same two, plus
`Microsoft.Extensions.DependencyInjection`, in `LocalizationResource.cs`).

**Do not** set `ValidatorOptions.Global.LanguageManager.Culture`: leaving it `null` is what makes FluentValidation
follow the request culture. **Do not** add `Microsoft.Extensions.Localization`; `ResourceManager` is enough.

### B2 — Request culture from `Accept-Language` (`Crm.BuildingBlocks.Presentation` + host)

**Create file: `Crm.BuildingBlocks.Presentation/Localization/RequestLocalizationExtensions.cs`**

```csharp
using Crm.BuildingBlocks.Application.Localization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;

namespace Crm.BuildingBlocks.Presentation.Localization;

public static class RequestLocalizationExtensions
{
    /// <summary>Accept-Language (en, ar) sets CurrentCulture and CurrentUICulture for the request. The only
    /// provider: no query string, no cookie - the frontend sends its UI locale as Accept-Language on every call.
    /// Must run BEFORE UseExceptionHandler and UseStatusCodePages, or their problem bodies stay English (Probe 1).</summary>
    public static IApplicationBuilder UseCrmRequestLocalization(this IApplicationBuilder app) =>
        app.UseRequestLocalization(options =>
        {
            string[] cultures = [.. CrmCultures.All];
            options.SetDefaultCulture(CrmCultures.Default)
                .AddSupportedCultures(cultures)
                .AddSupportedUICultures(cultures);
            options.RequestCultureProviders = [new AcceptLanguageHeaderRequestCultureProvider()];
            options.ApplyCurrentCultureToResponseHeaders = true; // Content-Language on every response
        });
}
```

`FallBackToParentCultures`/`FallBackToParentUICultures` default to `true`; that is what maps `ar-SA` → `ar`
(Probe 3). Keep the defaults.

**File: `backend/src/Crm.Api/Program.cs`**, lines 83–91. Insert the call right after line 88
(`app.UseMiddleware<CorrelationIdMiddleware>();`) and renumber the comment:

```csharp
    // ORDER MATTERS:
    // 1. correlation first - every later middleware, log line, and problem body can read it;
    // 2. request culture next - the exception handler and status-code pages below write problem bodies, and a
    //    culture set further down does not flow back up to them (Story 05, Probe 1);
    // 3. request logging outside the exception handler - it logs the final (handled) status once;
    // 4. exception handler, then status-code pages - bare 401/404/405 become problems too;
    // 5. rate limiter after routing (implicit), so the endpoint's [Area] is known.
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseCrmRequestLocalization();
    app.UseCrmRequestLogging();
    app.UseExceptionHandler();
    app.UseStatusCodePages();
```

Add `using Crm.BuildingBlocks.Presentation.Localization;`.

### B3 — Localised problem titles and fixed details

**File: `Crm.BuildingBlocks.Presentation/ErrorHandling/ProblemTypes.cs`**, replace `TitleFor` (lines 57–71):

```csharp
    /// <summary>The problem title in the request's language (Accept-Language). Resource key
    /// "Problem.Title.&lt;urn suffix&gt;"; an unknown type gets the internal-error title.</summary>
    public static string TitleFor(string type, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(localization);
        string known = type switch
        {
            Validation or BadRequest or Unauthorized or Forbidden or NotFound or MethodNotAllowed or Conflict
                or UnsupportedMediaType or DomainRule or RateLimited or DependencyUnavailable => type,
            _ => Internal,
        };
        return localization.Get("Problem.Title." + known["urn:crm:problem:".Length..]);
    }
```

Add `using Crm.BuildingBlocks.Application.Localization;`. **The constants do not change.** Update the class summary
line 4 ("The frontend maps these to localised messages (Story 05)") to "Titles are localised server-side from
Accept-Language; the frontend also maps these URNs to its own messages."

**Update every caller** (each fails to compile until updated, which is how you find them):

| File | Line(s) | Change |
|---|---|---|
| `ErrorHandling/ErrorHandlingServiceCollectionExtensions.cs` | 27 | `ProblemTypes.TitleFor(problem.Type, context.HttpContext.RequestServices.GetRequiredService<ILocalizationService>())` |
| `ErrorHandling/GlobalExceptionHandler.cs` | 18–20 | add primary-constructor parameter `ILocalizationService localization` |
| same | 67–68 | `Title = ProblemTypes.TitleFor(ProblemTypes.Validation, localization)`, `Detail = localization.Get("Problem.Detail.Validation")` |
| same | 78, 85, 94, 101 | detail literal → `localization.Get("Problem.Detail.Concurrency" / "…Unreadable" / "…DependencyUnavailable" / "…Unexpected")` |
| same | 104–111 | `Create` becomes an instance method (drop `static`), `Title = ProblemTypes.TitleFor(type, localization)` |
| `ApiControllerBase.cs` | 55, 62 | resolve once at the top of `Problem(Error)`: `ILocalizationService localization = HttpContext.RequestServices.GetRequiredService<ILocalizationService>();` then `ProblemTypes.TitleFor(type, localization)` |
| `RateLimiting/RateLimitingServiceCollectionExtensions.cs` | 61–62 | resolve `ILocalizationService` from `context.HttpContext.RequestServices`; `Title = ProblemTypes.TitleFor(ProblemTypes.RateLimited, localization)`, `Detail = localization.Get("Problem.Detail.RateLimited")` |

**What stays English on purpose** (Edge Cases): `DomainException`/`Error.Description` text (e.g. the concurrency
`Error.Conflict` description in `TransactionBehavior.cs` line 122), and MVC model-binding messages
("The Lines field is required."). Their `type` and `code` are stable; the frontend shows its own text by `code`
(Task F7).

### B4 — Request samples

**Create file: `backend/requests/localization.http`** (`@host = http://localhost:5187`, same comment style as
`diagnostics.http`). One request per row, each with a `# expect:` comment line:

1. `POST {{host}}/api/Admin/Samples` + `Accept-Language: ar` + `{ "name": "", "lines": [] }` → 400,
   `title` "فشل التحقق من صحة البيانات", `errors.Name[0]` "'Name' لا يجب أن يكون فارغاً.",
   `Content-Language: ar`. **This is the acceptance-criterion request.**
2. Same body with `Accept-Language: en` → "Validation failed", "'Name' must not be empty.", `Content-Language: en`.
3. Same body with `Accept-Language: ar-SA,en;q=0.5` → Arabic (parent-culture fallback).
4. Same body with `Accept-Language: fr` → English (unsupported → default).
5. Same body with no `Accept-Language` → English.
6. `GET {{host}}/api/Admin/Nope` + `Accept-Language: ar` → 404 `title` "غير موجود" (status-code pages path).
7. `GET {{host}}/api/Admin/Diagnostics/throw` + `Accept-Language: ar` → 500 `title` "خطأ داخلي", `detail`
   "حدث خطأ غير متوقع." (exception-handler path — fails if B2's order is wrong).
8. `POST {{host}}/api/Admin/Samples` + `Accept-Language: ar` + `{ "name": "Dup", "lines": ["a", "A"] }` → 422
   `title` "مخالفة لقاعدة عمل", `detail` stays the English domain message, `code` `Sample.DuplicateLine`.

---

## Frontend Tasks

All paths below are under `frontend/` unless they start with `backend/` or `.squad/`. Use the **Write tool** for
files. Arabic strings are UTF-8; do not paste them through a Bash heredoc.

### F1 — Dependencies

```bash
export COREPACK_ENABLE_DOWNLOAD_PROMPT=0
cd frontend && pnpm add -E @nuxtjs/i18n@10.6.0 @fontsource/ibm-plex-sans-arabic@5.3.0
```

Both land in `dependencies`, exact versions. Commit `package.json` and `pnpm-lock.yaml`. **No change to
`pnpm-workspace.yaml`** (Probe 5). If the install reports `ERR_PNPM_IGNORED_BUILDS` for a package not seen in the
probe, add it to `allowBuilds` with a one-line reason; never `--ignore-scripts`.

### F2 — `nuxt.config.ts`

```ts
import { readdirSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import tailwindcss from '@tailwindcss/vite'

// Every i18n/locales/en/<domain>.json is loaded for both locales (Probe 8), so a feature story adds a file pair
// and never edits this list. scripts/check-locales.ts guarantees the ar/ twin exists with the same keys.
const domains = readdirSync(fileURLToPath(new URL('./i18n/locales/en', import.meta.url)))
  .filter((file) => file.endsWith('.json'))
  .sort()

export default defineNuxtConfig({
  // … keep compatibilityDate, devtools, vite, typescript, runtimeConfig, routeRules, nitro unchanged …
  modules: ['@nuxt/eslint', '@pinia/nuxt', '@nuxtjs/i18n'],
  css: [
    '@fontsource/ibm-plex-sans-arabic/400.css',
    '@fontsource/ibm-plex-sans-arabic/500.css',
    '@fontsource/ibm-plex-sans-arabic/600.css',
    '@fontsource/ibm-plex-sans-arabic/700.css',
    '~/assets/css/main.css',
  ],
  i18n: {
    strategy: 'no_prefix', // one URL per page; the locale lives in the crm-locale cookie
    defaultLocale: 'en',
    locales: [
      { code: 'en', language: 'en', dir: 'ltr', name: 'English', files: domains.map((d) => `en/${d}`) },
      { code: 'ar', language: 'ar', dir: 'rtl', name: 'العربية', files: domains.map((d) => `ar/${d}`) },
    ],
    // Probe 6: cookie first, Accept-Language next, then 'en'. Story 07 adds the profile preference via setLocale.
    detectBrowserLanguage: {
      useCookie: true,
      cookieKey: 'crm-locale',
      redirectOn: 'no prefix',
      alwaysRedirect: false,
      fallbackLocale: 'en',
    },
  },
})
```

**Delete** the `app: { head: { htmlAttrs: { lang: 'en' } } }` line (line 21); Task F4 owns `lang`/`dir`. Weights
400/500/600/700 cover `font-normal|medium|semibold|bold`, the four the kit uses. `name` values are endonyms and are
**not** translated; the switcher shows the *target* language in its own script.

### F3 — Locale files

Create `i18n/locales/en/` and `i18n/locales/ar/` (project root, **not** under `app/`; `@nuxtjs/i18n` 10 resolves
`langDir` from `<rootDir>/i18n`). **Rule, enforced by Task F12:** a file's single top-level key equals its file name
(`shell.json` → `{ "shell": { … } }`), so `t('shell.x')` always lives in `shell.json`.

**Create file: `i18n/locales/en/shell.json`**: the exact key paths of `app/copy/en.ts` lines 4–36, plus four new
keys:

```json
{
  "shell": {
    "appName": "Customer Support CRM",
    "skipToContent": "Skip to main content",
    "nav": { "home": "Home", "main": "Main navigation", "openMenu": "Open navigation menu", "closeMenu": "Close navigation menu" },
    "theme": { "toLight": "Switch to light theme", "toDark": "Switch to dark theme" },
    "locale": { "switchTo": "Switch language to {language}" },
    "error": { "title": "Something went wrong", "retry": "Try again", "home": "Go home", "reference": "Reference" },
    "toast": { "dismiss": "Dismiss" },
    "pagination": { "label": "Pagination", "previous": "Previous", "next": "Next", "page": "Page" },
    "login": { "title": "Sign in", "pending": "Sign-in arrives with Story 07." },
    "home": { "tagline": "A multi-department, multi-branch, bilingual customer support CRM." },
    "agentHome": { "emptyTitle": "Nothing here yet", "emptyDescription": "Ticket, customer, and queue screens arrive in later stories." }
  }
}
```

**Create file: `i18n/locales/ar/shell.json`**, same keys:

| Key | Arabic |
|---|---|
| `appName` | نظام دعم العملاء |
| `skipToContent` | انتقل إلى المحتوى الرئيسي |
| `nav.home` / `nav.main` / `nav.openMenu` / `nav.closeMenu` | الرئيسية / التنقل الرئيسي / فتح قائمة التنقل / إغلاق قائمة التنقل |
| `theme.toLight` / `theme.toDark` | التبديل إلى المظهر الفاتح / التبديل إلى المظهر الداكن |
| `locale.switchTo` | تغيير اللغة إلى {language} |
| `error.title` / `retry` / `home` / `reference` | حدث خطأ ما / حاول مرة أخرى / العودة إلى الرئيسية / المرجع |
| `toast.dismiss` | إغلاق |
| `pagination.label` / `previous` / `next` / `page` | ترقيم الصفحات / السابق / التالي / صفحة |
| `login.title` / `login.pending` | تسجيل الدخول / يتوفر تسجيل الدخول مع القصة 07. |
| `home.tagline` | نظام لدعم العملاء متعدد الأقسام والفروع وثنائي اللغة. |
| `agentHome.emptyTitle` / `emptyDescription` | لا يوجد شيء هنا بعد / تصل شاشات التذاكر والعملاء وقوائم الانتظار في قصص لاحقة. |

**Create files: `i18n/locales/{en,ar}/errors.json`**: `errors.problem.<slug>` for the 12 backend URN suffixes
(same English/Arabic titles as the B1 table) plus `network` ("Network error" / "تعذّر الاتصال بالخادم"), and an
empty object `errors.code` (`{}`) that feature stories fill with `Error.Code` → message entries (Task F7).
The parity script treats `{}` on both sides as matching.

### F4 — `lang`/`dir` on `<html>`

**Create file: `app/plugins/locale-head.ts`** (Probe 7)

```ts
// <html lang dir> follow the active locale, on the server and after a client-side switch. A plugin rather than
// app.vue, because error.vue renders outside app.vue and must be RTL too.
export default defineNuxtPlugin((nuxtApp) => {
  const i18n = nuxtApp.$i18n
  useHead({
    htmlAttrs: {
      lang: () => i18n.localeProperties.value.language ?? i18n.locale.value,
      dir: () => i18n.localeProperties.value.dir ?? 'ltr',
    },
  })
})
```

This exact file typechecked in the probe. Logical utilities (`ms-`, `start-`, `border-s`, `text-start`) resolve
from `dir`, so no component reads the locale to lay itself out.

### F5 — Move `shellCopy` to keys

Mechanical: `shellCopy.a.b` → `t('shell.a.b')`, with `const { t } = useI18n()` in each `<script setup>` and the
`import { shellCopy } from '~/copy/en'` line removed. Files: `app/app.vue`, `app/layouts/agent.vue`,
`app/layouts/portal.vue`, `app/error.vue`, `app/pages/login.vue`, `app/pages/index.vue` (tagline →
`t('shell.home.tagline')`), `app/pages/agent/index.vue` (`title`/`description` → `t('shell.agentHome.*')`).

`agent.vue` line 86 builds `agentNav` once with `shellCopy.nav.home`; make it a `computed` so the label follows a
locale switch: `const agentNav = computed<ReadonlyArray<AgentNavItem>>(() => [{ to: '/agent', label:
t('shell.nav.home') }])`.

**Delete file: `app/copy/en.ts`** (and the empty `app/copy/` folder). `grep -rn "shellCopy\|copy/en" app` must be
empty afterwards. `/_dev/**` pages keep literal English (dev-only, never localised).

### F6 — Language switcher

**Create file: `app/components/shell/LocaleSwitcher.vue`** (a shell component: it may use `useI18n`; the
`app/components/ui/` kit still contains no copy and no i18n).

- Two locales, so it is **one toggle button** that switches to the other locale: `UiButton variant="ghost"
  size="sm"`, label = the target locale's `name` inside `<span :lang="target.code">` (so screen readers pronounce
  "العربية" as Arabic), `:aria-label="t('shell.locale.switchTo', { language: target.name })"`.
- `const { locale, locales, setLocale } = useI18n()`; `target` = the entry of `locales.value` whose `code !==
  locale.value`. Narrow with `typeof entry === 'object'` (the array type allows strings); **no `as` casts**.
- `@click="setLocale(target.code)"`. No navigation, no reload; the cookie is written by the module.

Mount it (auto-imported as `ShellLocaleSwitcher`) in `app/layouts/agent.vue`'s topbar, before `UiThemeToggle`
(line 38), and in `app/layouts/portal.vue`'s header, before `UiThemeToggle` (line 12).

### F7 — API calls carry the UI locale; problems shown in the UI locale

**File: `app/plugins/api.ts`**, lines 65–74. Change `defineNuxtPlugin(() => {` to `defineNuxtPlugin((nuxtApp) => {`
and wrap `baseFetch` (used by both the first send and the retry, so the 401 contract is untouched):

```ts
    baseFetch: (request) => {
      // The browser's own Accept-Language is the OS language, not the language the user picked (Task F2).
      const headers = new Headers(request.headers)
      headers.set('Accept-Language', nuxtApp.$i18n.locale.value)
      return globalThis.fetch(new Request(request, { headers }))
    },
```

**File: `app/composables/useApi.ts`**, `maybeToast` (lines 24–35). Take `$i18n` from `useNuxtApp()` next to
`$api` (line 21) and add a pure helper **`app/api/problem-message.ts`**:

```ts
import type { Problem } from './problem'

type Translate = { t: (key: string) => string; te: (key: string) => boolean }

/** Toast/alert text in the UI locale: a known code wins, then the URN's title, then the body's own title. */
export function problemMessage(problem: Problem, i18n: Translate): { title: string; description: string | null } {
  const slug = problem.type.startsWith('urn:crm:problem:') ? problem.type.slice('urn:crm:problem:'.length) : ''
  const titleKey = `errors.problem.${slug}`
  const codeKey = problem.code === null ? '' : `errors.code.${problem.code}`
  return {
    title: slug !== '' && i18n.te(titleKey) ? i18n.t(titleKey) : problem.title,
    description: codeKey !== '' && i18n.te(codeKey) ? i18n.t(codeKey) : problem.detail,
  }
}
```

`maybeToast` pushes `title`/`description` from
`problemMessage(problem, { t: (key) => $i18n.t(key), te: (key) => $i18n.te(key) })` (lambdas, because `$i18n.t` is
overloaded and does not assign to the narrow `Translate` type). Toast policy is unchanged
(validation never toasted, 401 never toasted). `app/api/problem.ts` is **not** changed: `titleForType` stays the
English fallback for bodies with no title.

### F8 — Formatting helpers over `Intl`

**Create file: `app/utils/format.ts`** (pure functions; explicit locale argument so `/_dev/ui` can render both
locales side by side):

```ts
export type CrmLocale = 'en' | 'ar'

/** Pinned BCP 47 tags (Probe 12). Gregorian only (Hijri is out of scope); Arabic-Indic digits, the ar-SA CLDR
 * default. Switching Arabic to Latin digits is a one-line change here: 'ar-SA-u-ca-gregory-nu-latn'. */
export const intlLocale: Readonly<Record<CrmLocale, string>> = { en: 'en-GB', ar: 'ar-SA-u-ca-gregory-nu-arab' }

/** One zone for SSR and browser, so server and client render the same text. Tenant/branch time zones: Story 06+. */
export const crmTimeZone = 'Asia/Riyadh'

export function toCrmLocale(value: string): CrmLocale // 'ar' when value === 'ar', else 'en'. No casts.
export function formatDate(value: Date | string, locale: CrmLocale, style?: 'short' | 'medium' | 'long'): string
export function formatTime(value: Date | string, locale: CrmLocale): string
export function formatDateTime(value: Date | string, locale: CrmLocale): string
/** `now` is a parameter: a relative time must be computed on the client, never during SSR. */
export function formatRelativeTime(value: Date | string, locale: CrmLocale, now: Date): string
export function formatNumber(value: number, locale: CrmLocale, options?: Intl.NumberFormatOptions): string
export function formatCurrency(amount: number, locale: CrmLocale, currency?: string): string // default 'SAR'
export function formatFileSize(bytes: number, locale: CrmLocale): string
/** Wraps text in U+2066 LRI … U+2069 PDI, for identifiers placed in a plain string (title attributes, toasts). */
export function isolateLtr(text: string): string
```

Rules:
- Dates use `timeZone: crmTimeZone`. Strings from the API are ISO `DateTimeOffset`; parse with `new Date(value)`.
  An invalid date (`Number.isNaN(date.getTime())`) returns `''`, never "Invalid Date".
- `formatRelativeTime` picks the largest unit (`year`, `month`, `week`, `day`, `hour`, `minute`, `second`) and uses
  `Intl.RelativeTimeFormat(…, { numeric: 'auto' })` ("yesterday" / "أمس").
- `formatFileSize` is decimal (1000) with `style: 'unit'`, units `byte`, `kilobyte`, `megabyte`, `gigabyte`,
  `unitDisplay: 'short'`, `maximumFractionDigits: 1`.
- Cache `Intl.*` instances in a module-level `Map<string, …>` keyed by locale + JSON of the options.

**Create file: `app/composables/useFormat.ts`**: calls `useI18n()` once (so call `useFormat()` in `<script setup>`)
and returns the same functions bound to the current locale, reading `toCrmLocale(locale.value)` on **each** call so
a switch re-renders correctly: `date`, `time`, `dateTime`, `relativeTime`, `number`, `currency`, `fileSize`.

### F9 — Arabic typography

**File: `app/assets/css/tokens.css`**, line 76–77: prepend the Arabic face.

```css
  --crm-font-sans:
    'IBM Plex Sans Arabic', ui-sans-serif, system-ui, -apple-system, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
```

IBM Plex Sans Arabic carries Latin glyphs too, so both locales share one typeface; each `@font-face` has a
`unicode-range` (Probe 11), so the Arabic file downloads only when Arabic text is on the page. `--crm-font-mono`
unchanged.

### F10 — RTL fixes in the kit

**File: `app/components/ui/Tabs.vue`**, `handleKeydown` (lines 79–106). Read the direction from the element at
keypress time (works under `<html dir>` and under a `dir="rtl"` wrapper on `/_dev/ui`; Probe 7 is why it is not read
from the locale right after a switch):

```ts
  const rtl = event.currentTarget instanceof HTMLElement && getComputedStyle(event.currentTarget).direction === 'rtl'
  const forward = (index + 1) % enabled.length
  const backward = (index - 1 + enabled.length) % enabled.length
  // …
    case 'ArrowRight':
      nextIndex = rtl ? backward : forward
      break
    case 'ArrowLeft':
      nextIndex = rtl ? forward : backward
      break
```

Replace the line 49 comment with "ArrowLeft/ArrowRight follow the visual order: swapped under dir=rtl."
`'ArrowLeft'`/`'ArrowRight'` string literals do not match the physical-direction regex (it needs `left`/`right`
at a class boundary, lower case), so no lint change is needed.

**File: `app/components/ui/Drawer.vue`**, line 6. The UA stylesheet gives `<dialog>` both inline insets `0`; with
only `start-0` set, the box is over-constrained and an `end` drawer can pin to the start edge. Make the free edge
explicit: `side === 'start' ? 'start-0 end-auto border-e border-border' : 'end-0 start-auto border-s border-border'`.

**File: `app/components/ui/ToastRegion.vue`**, line 2: `w-full` → `w-[calc(100%-2rem)]` (with `end-4`, a full-width
region at 375px sticks 16px past the start edge in either direction; `max-w-sm` stays).

**`app/components/ui/Pagination.vue`: no change.** `‹`/`›` (U+2039/U+203A) are Unicode bidi-mirrored glyphs and flip
by themselves under `dir="rtl"`; verify it on `/_dev/ui` (Test Plan row 14).

**`app/components/ui/Select.vue`: no change.** Its decorative `▾` (line 31) is in Task F12's bare-string allowlist.

### F11 — Responsive shell

**File: `app/layouts/agent.vue`**: the sidebar/drawer switch moves from `lg` (1024) to `md` (768), per the intake.
Line 10 `lg:block` → `md:block`; line 27 `lg:hidden` → `md:hidden`. Line 35: the app name gets `truncate`, and its
wrapper (line 24) `min-w-0`, so the long Arabic name never pushes the topbar wider than 375px.

**File: `app/layouts/portal.vue`**, line 10: the header row stacks on phones:
`mx-auto flex max-w-5xl flex-col items-start gap-2 px-4 py-3 sm:flex-row sm:items-center sm:justify-between`, and
the switcher + theme toggle sit in `<div class="flex items-center gap-2">`.

Tables already scroll inside their own `overflow-x-auto` wrapper (`ui/Table.vue` line 2). No page-level
`overflow-x-hidden` anywhere: it would hide a real overflow instead of fixing it.

### F12 — Lint: locale parity and bare strings

**Create file: `scripts/check-locales.ts`** (Probe 9; erasable TypeScript only, Node 24 runs it directly):

- Reads `i18n/locales/en/*.json` and `i18n/locales/ar/*.json`.
- Fails (exit 1, one `check-locales: …` line per problem on stderr) when: a file exists in one locale only; a file is
  not valid JSON; the file does not have exactly one top-level key equal to its base name; a value is neither a
  string nor an object (arrays, numbers, `null`); a string value is empty; a flattened key (`shell.nav.home`)
  exists in one locale only.
- Prints `check-locales: N domain files, en/ar key sets match` and exits 0 otherwise.
- Use `process.stdout.write`/`process.stderr.write`, not `console` (keeps the script lint-clean).

**File: `package.json`**, line 13: `"lint": "eslint . && node scripts/check-locales.ts"`. `lint:fix` stays
`eslint . --fix`.

**File: `eslint.config.mjs`**: keep lines 1–35 as they are (**the `physical` regex and both direction rules are not
touched**). Add one config object before `.append(prettier)`:

```js
  {
    // Every user-visible string is a translation key (CLAUDE.md). /_dev/** is dev-only English and is exempt.
    files: ['app/**/*.vue'],
    ignores: ['app/pages/_dev/**', 'app/components/dev/**'],
    rules: {
      'vue/no-bare-strings-in-template': [
        'error',
        {
          allowlist: ['(', ')', ',', '.', '&', '+', '-', '=', '*', '/', '#', '%', '!', '?', ':', '[', ']', '{', '}',
            '<', '>', '|', '·', '•', '–', '—', '…', '☰', '✕', '‹', '›', '▾', '🌙', '☀️'],
        },
      ],
    },
  },
```

After Task F5, `pnpm lint` reports **zero** bare strings (Probe 10 found exactly the three that F5/F10 remove or
allow).

### F13 — `/_dev/ui`: locale review, formatting table, bidi demo

**File: `app/pages/_dev/ui.vue`**:

1. **Locale switcher** in the header (lines 14–27 group): `<ShellLocaleSwitcher />`. Switching flips the whole page
   (html `dir`), so every kit section is reviewed in RTL in one pass; the light/dark columns and the width switcher
   keep working.
2. **New section "Formatting"** (`id="formatting"`, add to `sections`, lines 465–483): a `UiTable` with columns
   *Helper*, *en*, *ar* and one row each for `formatDate` (short/medium/long), `formatTime`, `formatDateTime`,
   `formatRelativeTime` (−90 s, −3 h, −1 day, +2 days), `formatNumber(1234567.891)`, `formatCurrency(1234.5)`,
   `formatCurrency(-0.5)`, `formatFileSize` (0, 999, 1536, 5 242 880, 3 221 225 472). The *en* cell calls the helper
   with `'en'`, the *ar* cell with `'ar'` and carries `dir="rtl" lang="ar"`, so both render side by side regardless
   of the page locale. The fixed input instant is `2026-09-29T14:05:00Z`; `now` for relative times is taken in
   `onMounted` (the page is `ssr: false`, but keep the habit).
3. **New section "Bidi"** (`id="bidi"`): three rows inside a `dir="rtl" lang="ar"` block, each shown **raw** and
   **isolated** (`<bdi dir="ltr">`): an Arabic sentence containing the ticket reference `TCK-2026-000123`, the e-mail
   `ahmed@example.com`, and the phone number `+966 55 123 4567` (the raw phone reorders its digit groups in RTL; the
   isolated one does not). Plus one `isolateLtr()` example in a `title` attribute. This is the manual check for the
   "mixed-direction content" criterion and the pattern feature stories copy.

Keep the existing `arabicString` long-content rows. Add the two sections to `sections` so they appear in the index.

### X1 — Documentation

- **File: `frontend/README.md`**: new section "i18n and RTL": locale files per domain and the top-level-key rule,
  the `crm-locale` cookie and detection order, `useFormat()`/`intlLocale`/`crmTimeZone`, `<bdi dir="ltr">` /
  `isolateLtr` for identifiers, `dir="ltr"` on e-mail/phone/URL inputs (passed through `UiInput`'s `$attrs`),
  `Accept-Language` sent on every API call, the `pnpm lint` parity check. In "Lint rules and why", add the
  bare-string rule.
- **File: `CLAUDE.md`**, "Frontend" (after line ~209), append:
  - Locale files are `frontend/i18n/locales/{en,ar}/<domain>.json`, one top-level key per file equal to the file
    name; `pnpm lint` fails on diverging key sets and on bare template strings outside `/_dev`.
  - Format dates, numbers, currency, and sizes only through `useFormat()` (`Intl`, Gregorian, `Asia/Riyadh`).
    Identifiers inside Arabic text go in `<bdi dir="ltr">`.
- **File: `CLAUDE.md`**, "Application, API, and persistence" (after the `Results` bullet, line ~155–160), append:
  - **Localisation:** `Accept-Language` (`en`, `ar`) sets the request culture (`UseCrmRequestLocalization`, before
    the exception handler). FluentValidation built-in messages and problem titles follow it. Custom server text
    goes through `ILocalizationService` and a `.resx` pair in the owning module's Application project, registered
    with `AddLocalizationResource`. Cached query results are culture-neutral: return both languages
    (`LocalizedText`) rather than caching a translated string.
- **File: `backend/README.md`**, "Error responses" (lines 228–233): "a fixed English `title`" → "a `title` in the
  request's language (`Accept-Language: en|ar`, default `en`)"; mention `Content-Language` and
  `backend/requests/localization.http`.
- **File: `.claude/commands/frontend-slice.md`**, line 15: `i18n/locales/{en,ar}.json` →
  `i18n/locales/{en,ar}/<feature>.json` (one top-level key `<feature>`); "no hardcoded copy" stays.

---

## Edge Cases & Failure Modes

- **`UseCrmRequestLocalization` placed after `UseExceptionHandler`** → 500/404/401 bodies stay English under
  `Accept-Language: ar` while 400s from controllers look right (Probe 1). Enforced by the Program.cs order (B2);
  caught by `.http` requests 6 and 7.
- **`Accept-Language: ar-SA` / `ar-EG`** → `ar` via parent fallback (Probe 3). **Never add `ar-SA` as a supported
  culture**: its .NET default calendar is Um Al-Qura, and any server-side date formatting would turn Hijri.
- **Unsupported or malformed `Accept-Language`** (`fr`, `*`, `xx;q=abc`) → default `en`, never an error.
- **Missing resource key** → `ILocalizationService.Get` returns the key itself (`Problem.Detail.X`): visible in
  review, never an exception. A key missing only in `CrmMessages.ar.resx` falls back to English.
- **FluentValidation property names stay Latin** in Arabic messages (`'Name' لا يجب أن يكون فارغاً.`, Probe 2). The
  form shows the message under its labelled field, so this is accepted for the MVP. A localised display-name
  resolver is a later story's call, not this one.
- **MVC model-binding messages** ("The Lines field is required.", unreadable JSON) stay English; their `title`
  is localised through `CustomizeProblemDetails` (B3 row 1). Domain `Error.Description` stays English; the frontend
  shows `errors.code.<Code>` when a feature story defines it (`problemMessage`, F7).
- **Validation message containing `:` or ` | `** → Story 03's `ValidationErrorParser` quirk. Arabic messages put
  the property name first, so the first `:` after the field prefix is still the separator. Unchanged here.
- **Serilog rendered messages under `ar`**: log lines emitted inside an Arabic request render numbers in the `ar`
  culture in `@m` (e.g. `12٫5`). Structured properties are unaffected. Do **not** move localisation inside the
  exception handler to "fix" it.
- **Cached queries.** `CachingBehavior` keys by scope, not culture (Story 03/06 contract). Failures are never
  cached, so localised validation text never enters the cache. A query must not return a translated string; return
  both languages. Enforced by the CLAUDE.md bullet (X1).
- **Browser sends the OS language, not the UI language** → without F7's wrapper, an Arabic UI on an English
  Windows gets English validation messages. The wrapper also covers the 401 retry because it sits in `baseFetch`.
  SSR calls go through the same plugin, and `$i18n.locale` is already resolved from the cookie on the server.
- **`<html dir>` lags `setLocale` by a tick** (Probe 7) → never branch on `document.dir` right after a switch.
  Tabs reads the computed direction at keypress time.
- **Tampered `crm-locale` cookie** (`zz`) → ignored; `Accept-Language`, then `en` (Probe 6).
- **Agent SPA** (`/agent/**`, `ssr: false`) → locale detected on the client from the cookie, then
  `navigator.language`. First paint in an SPA is client-rendered, so there is no LTR flash.
- **Hydration mismatch on SSR pages** (portal): server (Node ICU) and browser ICU can differ in punctuation or
  invisible marks for the same `Intl` call. Pinned tags and `crmTimeZone` remove the big differences; a relative
  time must be computed on the client (`now` parameter). If a date still mismatches, wrap that element in
  `<ClientOnly>`. No portal page shows dates in this story.
- **vue-i18n message syntax**: `{`, `}`, `@`, `$`, `|` are special in messages (`|` is pluralisation, `@` a linked
  message). A literal e-mail or pipe in a translation must be written as `{'@'}` / `{'|'}`. The build fails with a
  message-compile error otherwise.
- **Physical-direction regex false positives**: a script literal `'left'`/`'right'` still fails (Story 04 rule, kept).
  `translate-x-*` is not banned (centering is direction-neutral); never use it for a directional offset.
- **Inputs for Latin data in RTL**: e-mail, phone, and URL inputs get `dir="ltr"` from the feature form
  (`UiInput` forwards `$attrs`); otherwise the caret and punctuation jump. Documented in X1, no kit change.
- **Arabic-Indic digits**: displayed numbers use `٠–٩` in `ar`; users may type Latin digits in inputs. Parsing input
  is a feature-story concern; `intlLocale.ar` is the single switch if the owner prefers Latin digits.
- **Font loading**: `font-display: swap` shows the fallback first. With no network access to `/_nuxt/*.woff2` the
  system Arabic face (Segoe UI on Windows) is used; text stays readable.
- **`pnpm lint` on a fresh clone** needs `pnpm install` first (Story 04 edge case, unchanged); `check-locales.ts`
  runs only if ESLint passes (`&&`), so fix lint errors first.
- **Adding a locale file to `en/` only** → `nuxt dev` loads it for `ar` too and fails to find the file; `pnpm lint`
  names the missing twin first. Always add the pair.

---

## Test Plan

**This project ships without automated tests** (`CLAUDE.md`, "Testing policy"). **Do not add Vitest, Playwright,
`@nuxt/test-utils`, an i18n test harness, or any `*.spec.ts` / `*.test.ts` / `*.Tests.csproj`.** The compiler, the
linter, the parity script, `/_dev/ui`, and `backend/requests/localization.http` replace the suite. Run every row and
record the result in the story report.

| # | Check | How | Expected |
|---|---|---|---|
| 1 | Backend build | `dotnet build backend/Crm.sln` | 0 warnings, 0 errors, no `CRM0001`; `backend/src/Crm.Api/bin/Debug/net10.0/ar/Crm.BuildingBlocks.Application.resources.dll` exists |
| 2 | Arabic validation (AC) | API running; `localization.http` request 1 | 400, `title` "فشل التحقق من صحة البيانات", `errors.Name[0]` "'Name' لا يجب أن يكون فارغاً.", header `Content-Language: ar` |
| 3 | Negotiation | requests 2–5 | en / ar / en / en as listed in B4 |
| 4 | Exception + status pages | requests 6–8 | Arabic `title` on 404, 500 (and 500 `detail` Arabic, no exception text), 422 |
| 5 | English unchanged | `diagnostics.http` requests 4, 6, 7 without `Accept-Language` | Byte-identical titles/details to before this story |
| 6 | Frontend gates | `pnpm --dir frontend typecheck`, `lint`, `build` | all exit 0; lint prints `check-locales: 2 domain files, en/ar key sets match` |
| 7 | Lint bites: direction | temporarily add `class="left-0 border-l rounded-r-md ml-2"` to a page and `'pr-4'` in a script | each is an error; revert |
| 8 | Lint bites: parity | temporarily delete `shell.nav.home` from `ar/shell.json`; then add a key only to `ar/errors.json` | `pnpm lint` exits 1 naming each key; revert |
| 9 | Lint bites: bare string | temporarily add `<p>Hello</p>` to `app/pages/index.vue` | `vue/no-bare-strings-in-template` error; the same line in `app/pages/_dev/ui.vue` passes |
| 10 | Grep guard | `grep -rn "shellCopy\|copy/en" frontend/app` and the Story 04 `any`/`ts-ignore` grep | no matches |
| 11 | Switch without reload | `pnpm --dir frontend dev`; `/` → click the switcher | `<html lang="ar" dir="rtl">`, text Arabic, layout mirrored, DevTools Network shows **no document request** |
| 12 | Persistence (AC) | after row 11, reload; view-source | server HTML already `lang="ar" dir="rtl"`; cookie `crm-locale=ar`; same on `/agent` after reload |
| 13 | First visit | clear cookies; Edge language set to Arabic first, reload `/` | Arabic; then English-first → English |
| 14 | Kit in RTL | `/_dev/ui`, switch to Arabic, walk every section in light and dark | nothing clipped or overlapping; Drawer `start` opens from the right, `end` from the left; Select chevron on the left; Pagination arrows point outward correctly; Toasts bottom-left |
| 15 | Tabs keyboard | `/_dev/ui` Tabs, keyboard only, in `en` then `ar` | ArrowRight moves to the visually right tab in both directions; Home/End unchanged |
| 16 | Formatting (AC) | `/_dev/ui` Formatting section | en and ar columns side by side; ar shows Arabic-Indic digits, Gregorian dates, `ر.س.` currency, Arabic relative times; en shows `SAR 1,234.50`, `29 Sept 2026` |
| 17 | Bidi (AC) | `/_dev/ui` Bidi section | isolated rows keep `+966 55 123 4567`, `TCK-2026-000123`, the e-mail intact; the raw phone row visibly reorders |
| 18 | Font (AC) | DevTools → Elements → an Arabic heading → Computed → Rendered fonts | "IBM Plex Sans Arabic"; Network shows only the Latin woff2 on an English page with no Arabic text |
| 19 | API locale | `/_dev/api` form with empty Name, UI in Arabic | request header `Accept-Language: ar`; the Arabic message appears under Name; no toast. A 500 ("throw") toasts the Arabic title from `errors.problem.internal` |
| 20 | Responsive (AC) | DevTools device toolbar at 375, 768, 1280 on `/`, `/agent`, `/login`, `/does-not-exist`, `/_dev/ui`, in `en` **and** `ar` | no horizontal page scroll (`document.documentElement.scrollWidth === innerWidth` in the console); agent sidebar is a drawer at 375 and visible at 768; portal header stacks at 375 |
| 21 | Error page RTL | `/does-not-exist` with `crm-locale=ar` | styled 404, `<html dir="rtl">`, Arabic copy |
| 22 | Production strip | `pnpm --dir frontend build`; `grep -rl "Fake transport\|static preview" frontend/.output` | no matches (Story 04 row 8 still holds) |

---

## Verification Steps

1. **Backend builds:** `dotnet build backend/Crm.sln` → row 1.
2. **Backend runs:** `docker compose -f backend/docker-compose.yml up -d`, then
   `dotnet run --project backend/src/Crm.Api --launch-profile http` → `Now listening on: http://localhost:5187`.
   Run `backend/requests/localization.http` top to bottom → rows 2–5.
3. **Frontend installs:** `cd frontend && pnpm install` (after F1) → no `ERR_PNPM_*`.
4. **Frontend gates:** from `D:\CRM`: `pnpm --dir frontend typecheck`, `pnpm --dir frontend lint`,
   `pnpm --dir frontend build` → row 6; then rows 7–10 and 22.
5. **Frontend runs:** `pnpm --dir frontend dev` with the API up → rows 11–21, in Edge.
6. **Regression:** `pnpm --dir frontend gen:api` with the API up → `git diff --stat frontend/openapi` empty (no
   contract change in this story). `/_dev/api` Fake transport scenarios A–E still PASS (the `baseFetch` wrapper must
   not change the 401 contract).

---

## Done Criteria

- [ ] Switching locale flips `dir` between `ltr` and `rtl` without a full reload and without layout breakage
      (rows 11, 14).
- [ ] `pnpm lint` fails on a physical-direction utility (existing Story 04 rules, unchanged) and on diverging
      `en`/`ar` key sets (rows 7, 8); it also fails on a bare template string outside `/_dev` (row 9).
- [ ] Dates, numbers, SAR currency (and time, relative time, file size) render in each locale's conventions, side by
      side on `/_dev/ui` (row 16), through `app/utils/format.ts` over `Intl` only.
- [ ] `POST /api/Admin/Samples` with an empty name and `Accept-Language: ar` returns the validation message in Arabic,
      recorded as request 1 of `backend/requests/localization.http` (row 2). Problem titles and fixed details follow
      `Accept-Language` through `ILocalizationService` + `.resx` in `Crm.BuildingBlocks.Application` (row 4).
- [ ] The chosen locale survives a browser refresh (`crm-locale` cookie; server HTML already RTL) (row 12).
- [ ] Arabic renders in self-hosted IBM Plex Sans Arabic with a system fallback stack (row 18).
- [ ] Mixed-direction content renders without bidi artifacts using `<bdi dir="ltr">` / `isolateLtr` (row 17).
- [ ] Agent layout, portal layout, and the UI kit are walked in `ar` and `en` at 375, 768, 1280 with no horizontal
      page scroll; the agent sidebar is a drawer below 768px; the portal header stacks (row 20).
- [ ] A language switcher is in the agent topbar and the portal header; `/_dev/ui` has one too.
- [ ] `app/copy/en.ts` is deleted; every shell string is a key in `i18n/locales/{en,ar}/shell.json`.
- [ ] API calls send `Accept-Language` = the UI locale; toasts use `errors.problem.*` in the UI locale (row 19).
- [ ] `dotnet build backend/Crm.sln` has 0 warnings and no `CRM0001`; no module project, schema, or migration changed.
- [ ] `frontend/README.md`, `backend/README.md`, `CLAUDE.md`, and `.claude/commands/frontend-slice.md` updated (X1).
- [ ] **No test framework, test file, or `tests/` folder exists** anywhere.
- [ ] `00-overview.md` lists this plan.

---

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 06.**
