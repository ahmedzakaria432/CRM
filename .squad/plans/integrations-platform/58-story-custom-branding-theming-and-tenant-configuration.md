# Story 58 — Custom branding theming and tenant configuration

## Prerequisites

- **Stories 01–07, 11, 14, 15, 18, 20 and 47 are implemented and committed** (latest `caa782c` "Implement Story 47").
  This is the **last MVP story** (`.squad/BACKLOG.md` line 107: `… → 47 → 58`). It builds on:
  - Story 04 — [`../platform-foundation/04-story-frontend-scaffold-with-nuxt-typescript-and-tailwind.md`](../platform-foundation/04-story-frontend-scaffold-with-nuxt-typescript-and-tailwind.md)
    line 93: "Story 58 (branding) rebrands by overriding the `--crm-*` CSS variables only. No component names a raw
    colour"; line 444: the theme stays `light`/`dark` (no `system` option — this story keeps that).
  - Story 05 — [`../platform-foundation/05-story-bilingual-arabic-and-english-support-with-rtl-layout.md`](../platform-foundation/05-story-bilingual-arabic-and-english-support-with-rtl-layout.md)
    line 72: "Story 58 may override `--crm-font-sans`; the Arabic face stays first in the stack."
  - Story 06 — [`../platform-foundation/06-story-multi-department-and-multi-branch-organisation-scoping.md`](../platform-foundation/06-story-multi-department-and-multi-branch-organisation-scoping.md)
    line 63: "Story 58 adds tenant configuration/branding to the `Organisation` aggregate created here."
  - Story 07 — [`../identity-access/07-story-authentication-users-roles-and-permissions.md`](../identity-access/07-story-authentication-users-roles-and-permissions.md)
    line 111: "Story 58 declares `CrmPermissions.AdminBranding` on its requests" (the permission already exists and
    Administrator already holds it — `CrmPermissions.cs` lines 17, 22–23, 36).
  - Story 20 — [`../ticket-management/20-story-ticket-inbox-and-ticket-detail-workspace-ui.md`](../ticket-management/20-story-ticket-inbox-and-ticket-detail-workspace-ui.md)
    line 97: the workspace screens are themed through `--crm-*` only. The agent layout `<main>` is `overflow-x-clip`
    (`frontend/app/layouts/agent.vue` line 47) — keep it.
  - Story 47 — [`../customer-portal/47-story-customer-portal-accounts-ticket-submission-tracking-and-history.md`](../customer-portal/47-story-customer-portal-accounts-ticket-submission-tracking-and-history.md)
    D17 ("`/portal/**` pages are server-rendered with the portal layout; the session and every data call run in the
    browser … The SSR server never holds a session"). This story adds **one anonymous server-side call** (public
    branding) to SSR and nothing else; D17 stays true for every user-owned call.
- **Module:** `Organisation` only (catalogue in `CLAUDE.md`: "tenant configuration and branding", stories 06, 58). No
  other module, no Contracts change, no integration event, no cross-module reference. Building blocks: two doc-comment
  edits only (B10). One EF migration in the `organisation` schema.
- **Checked during planning (read from lockfiles / installed packages, not run):** `nuxt` 4.5.2, `@nuxtjs/i18n` 10.6.0,
  `@pinia/nuxt` 1.0.2, `tailwindcss` 4.3.3, `typescript` 5.9.3, `openapi-fetch` 0.17.0 (`dist/index.mjs` lines 50–63: a
  `FormData` body gets no `Content-Type: application/json`; lines 476–479: `defaultBodySerializer` passes `FormData`
  through), `openapi-typescript` 7.13.0 (`dist/index.d.ts` line 323: the `transform(schemaObject, options)` option),
  `@fontsource/ibm-plex-sans-arabic` 5.3.0, backend `Swashbuckle.AspNetCore` 10.2.3 (`backend/Directory.Packages.props`
  line 20). There is **no `IFormFile` endpoint yet** in the backend (grep: none) — this story adds the first (R3).

---

## Story Goal

An administrator makes the product look like their organisation — no deployment, no component change:

1. **Admin → Branding** (`/admin/branding`, `admin.branding`): display name in Arabic and English, a light and a dark
   logo (PNG/SVG, ≤ 512 KB, sniffed by content, SVG sanitised), primary and accent colours, the Arabic font from a fixed
   list, a **live preview** of the agent shell and a portal page in light and dark, a live WCAG AA contrast list, save,
   and **revert to the previous version**.
2. **Save rejects a palette that fails WCAG AA** (text on the primary colour, the accent against the page) with a
   localised 422 naming the check, the ratio and the requirement.
3. **The organisation's tokens reach every page through `--crm-*` only**: the server derives an 11-step primary and
   accent scale for light and dark; the frontend writes them into one `<style id="crm-brand">` in the head —
   server-rendered on the portal pages and `/login`, on shell load for the agent/admin SPA.
4. **An anonymous endpoint** returns the public branding (name, logo ids, tokens, font) for the portal sign-in pages;
   logos are served anonymously by id with safe headers. The public response is cached (`ICacheableQuery`) and
   invalidated on save and revert.
5. **No literal colour in `app/components/` or `app/layouts/`** — already true today (verified by grep during
   planning); this story adds an ESLint guard so it stays true.

**Not in scope** (intake, "Out of scope"): per-branch branding, custom domains/certificates, email/SMS/PDF template
branding, a system configuration screen, feature flags, configuration export/import, a `system` theme option, audit
log entries (the Audit module, story 10, is deferred — no integration event is published), deleting orphaned logos,
and any test project.

---

## Context — Read These Files First

1. `CLAUDE.md` — "Modular monolith", "Domain-driven design", "Application, API, and persistence" (Results, Localisation,
   Caching, Localization keys, Persistence: "Value objects map as owned types or with a `ValueConverter`"), "Scoping"
   (Organisation is unscoped; the two permitted scope lifts), "Frontend" (tokens only, `useApi()`, `useFormErrors()`,
   `/_dev/ui` for new kit components, locale files), "Testing policy".
2. `backend/src/Modules/Organisation/Crm.Modules.Organisation.Domain/Organisation.cs` lines 1–32 — the aggregate you
   extend: `AuditableEntity<TenantId>`, `IHasRowVersion`, **not** `ITenantScoped` (its id *is* the tenant), private
   parameterless ctor for EF (lines 18–22), static `Create` (30–31). `LocalizedText.cs` lines 19–38 (reuse it for the
   display name; its error codes are `Organisation.NameRequired`/`NameTooLong`). `OrganisationCode.cs` lines 15–35
   (`Create` upper-cases; pattern `^[A-Z0-9-]{2,20}$`). `MembershipId.cs` lines 5–8 (strongly-typed id shape).
   `Department.cs` lines 49–59 (intention-revealing method + `DomainException` shape).
3. `backend/src/Modules/Organisation/Crm.Modules.Organisation.Infrastructure/Persistence/Configurations/OrganisationConfiguration.cs`
   lines 9–31 — where the new columns are mapped. `MembershipConfiguration.cs` lines 14–23 (an aggregate's own id needs
   its own conversion; `TenantId` is converted by the `ModuleDbContext` convention). `OrganisationDbContext.cs` lines
   15–21 (add the `DbSet`). `OrganisationModule.cs` lines 19–33 (repositories, seeder, module API registration).
   `OrganisationSeeder.cs` lines 15–26 (the DEMO organisation, idempotent by well-known id).
4. `backend/src/Modules/Organisation/Crm.Modules.Organisation.Application/Commands/RenameDepartment/*.cs` — the command
   + handler + validator triplet to copy (`SetOriginalRowVersion` at handler line 22). `Dtos.cs` lines 5–9
   (`LocalizedTextDto(Ar, En)` — reuse). `Specifications/BranchByCodeSpecification.cs` lines 8–14 (`.Equals`, not `==`,
   on a value object; `AsReadOnly()`). `Presentation/Controllers/Admin/DepartmentsController.cs` lines 12–51 (controller
   shape, `ProducesResponseType` set). `Presentation/Crm.Modules.Organisation.Presentation.csproj` lines 3–15
   (`DisableTransitiveProjectReferences`: controllers see only DTOs).
5. `backend/src/Modules/Tickets/Crm.Modules.Tickets.Application/TicketRefusals.cs` lines 9–45 — the **pre-check +
   domain backstop** pattern: ask the domain first, return `Error` whose description is localised server text
   (`string.Format(CultureInfo.CurrentCulture, localization.Get(key), …)`); the aggregate throws the same code in
   English if a handler forgets. `Localization/TicketsMessageKeys.cs` lines 3–30 (keys class). `Localization/Resources/TicketsMessages.resx`
   (resx shape). `Crm.Modules.Tickets.Infrastructure/TicketsModule.cs` lines 38–40 (`AddLocalizationResource`).
   `Crm.Modules.Customers.Application/Validation/CustomersRules.cs` lines 11–19 (a localised validator rule).
6. `backend/src/Modules/Customers/Crm.Modules.Customers.Application/Queries/GetCustomerById/GetCustomerByIdQuery.cs`
   lines 8–15 and `CustomersCacheKeys.cs` lines 3–7 — `ICacheableQuery` + keys class. `ICacheInvalidatingCommand`: e.g.
   `Commands/AddNote/AddNoteCommand.cs` line 9.
7. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Application/Behaviors/CachingBehavior.cs` lines 83–95 (key prefix
   `t=…|d=…|b=…`; anonymous callers get `t=-|d=-|b=-`; invalidation is by tag, scope-free) and `TransactionBehavior.cs`
   lines 78–83 (tags invalidated after commit). `Caching/ICacheableQuery.cs` lines 8–28.
8. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Application/Authorization/RequestAuthorizationAttributes.cs` lines
   14–17 (`[AllowAnonymousRequest]` — doc comment to extend) and `CrmPermissions.cs` lines 10–45.
9. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Presentation/AREAS.md` (Public Area: anonymous, IP rate-limited by the
   global limiter, no attribute needed) and `RateLimiting/RateLimitingServiceCollectionExtensions.cs` lines 27–66
   (`RateLimiting:Public`, 60 per minute per IP in `appsettings.json` lines 20–24). `Authorization/StaffAreaAuthorizationConvention.cs`
   lines 11–29 (Agent/Admin get the Staff policy automatically; Public is untouched — **do not** add `[AllowAnonymous]`).
10. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Presentation/ApiControllerBase.cs` lines 18–68 and
    `ErrorHandling/ErrorTypeMapping.cs` (Domain → 422 `urn:crm:problem:domain-rule`; the `code` extension).
    `SharedKernel/DomainError.cs`, `DomainException.cs`, `ValueObject.cs`, `StronglyTypedId.cs`.
11. `backend/src/BuildingBlocks/Crm.BuildingBlocks.Application/Behaviors/LoggingBehavior.cs` lines 33–37 and 81–82 —
    the request is destructured **only at Debug**; the upload command carries the file bytes (see Edge Cases).
12. `backend/requests/organisation.http` lines 1–40 (header, dev-seed ids, login requests) and
    `backend/requests/ticket-thread.http` line 425 (`< ./data/…` file inclusion).
13. `frontend/app/assets/css/tokens.css` lines 1–104 (the defaults; header comment lines 1–6 says "Story 58 overrides
    this file only") and `frontend/app/assets/css/main.css` lines 20–86 (`@theme inline` maps `--color-*` to `--crm-*`,
    so a nested `data-theme` or an inline `style` re-resolves utilities — "Probe 5").
14. `frontend/app/layouts/agent.vue` lines 10–21 (desktop nav), 35 (app name), 69–78 (drawer nav), 87–113 (script);
    `frontend/app/layouts/portal.vue` lines 9–16 (header + app name), 70–72 (footer); `frontend/app/app.vue` line 2.
15. `frontend/app/plugins/api.ts` lines 6–41 (SSR uses `runtimeConfig.apiBase`, browser same-origin `/api`),
    `plugins/theme.ts`, `plugins/locale-head.ts` (`<html lang dir>` on the server), `composables/useTheme.ts` lines 3–18,
    `composables/useApi.ts` lines 18–66, `api/problem-message.ts` line 9 (`serverWordedCodes`), `api/clients.ts` lines
    23–33 (the `public` client already exists), `stores/auth.ts` lines 7–47 (store shape with `nuxtApp.runWithContext`),
    `composables/useBranches.ts` lines 5–30 (admin composable shape, `useAsyncData` + thrown problem).
16. `frontend/nuxt.config.ts` lines 15–27 (`css`, `runtimeConfig`) and 28–32 (`/admin/**` is `ssr: false`; `/login`,
    `/` and `/portal/**` are SSR). `frontend/eslint.config.mjs` lines 7–35 (the class-restriction rule and its ui-kit
    override — a later block **replaces** the rule options, it does not merge them). `frontend/scripts/gen-api.ts`
    lines 70–78 (`openapiTS(jsonPath)`).
17. `frontend/app/pages/admin/branches.vue` lines 1–80 and 119 (admin page states: forbidden / error / table,
    `definePageMeta({ layout: 'agent' })`), `components/ui/Input.vue` lines 1–49 (kit field shape: label, hint, error,
    `useId`), `components/ui/Select.vue`, `utils/debounce.ts`, `utils/scope.ts` line 14 (`localizedName`),
    `pages/_dev/ui.vue` lines 569–590 (`sections` list).
18. `frontend/i18n/locales/en/errors.json` lines 19–25 (`errors.code.Organisation.*`) and `shell.json`.

---

## Product rules (decisions this story makes — later stories must not silently reverse them)

- **D1 — Branding is a value object on `Organisation`, stored as JSON through a `ValueConverter`.** `Organisation`
  gains `Branding? Branding`, `Branding? PreviousBranding`, `bool CanRevertBranding`. `null` Branding = the product
  defaults (tokens.css). Two nullable `nvarchar(4000)` columns (`Branding`, `PreviousBranding`) + one `bit`. Chosen over
  nested optional owned types (EF cannot tell an absent optional owned type with nested owned members from an all-null
  one) and over a history table (MVP needs one step of history).
- **D2 — Revert is a swap.** `ChangeBranding(b)` moves the current branding to `PreviousBranding` (unless `b` equals the
  current one — saving the same thing twice keeps the real previous version) and sets `CanRevertBranding = true`.
  `RevertBranding()` swaps current and previous; reverting twice re-applies. The first save's previous version is the
  product default (`null`), so revert after the first save returns to the defaults. `CanRevertBranding == false` →
  422 `Organisation.NoPreviousBranding`.
- **D3 — Logos are their own aggregate, `BrandLogo`, in `organisation.BrandLogos` (`varbinary(max)`, in the database).**
  Upload creates a `BrandLogo` and returns its id; the branding references logo ids. Logos are immutable, so revert
  and the pre-save preview are free. Orphaned logos (uploaded, never saved) are kept (MVP; noted).
- **D4 — `BrandLogo` is not `ITenantScoped`** (like `Organisation`): a logo is public content served anonymously by id to
  the portal sign-in page before any scope exists. It carries `TenantId`; the change command refuses a logo id whose
  `TenantId` is not the caller's tenant (404 `Organisation.BrandLogoNotFound`). **No new `IgnoreOrganisationScope()`
  caller, no filter change** (S1).
- **D5 — Which organisation a page shows.** Pre-login surfaces (`/`, `/login`, `/portal/**`) show the organisation
  named by `runtimeConfig.public.organisationCode` (default `'DEMO'`, env `NUXT_PUBLIC_ORGANISATION_CODE`) through the
  anonymous endpoint — one portal host per organisation (custom domains are out of scope). The agent/admin shell
  re-themes from the **signed-in staff user's tenant** (`GET /api/Agent/Branding`). A portal customer of another tenant
  who signs in on a host configured for a different code sees the host's branding (accepted for the MVP).
- **D6 — Palette and contrast rules (the domain's, enforced on save).** Colours are `#RRGGBB`. Surface = `#FFFFFF`
  (tokens.css `--crm-surface: oklch(1 0 0)`), inverse text = `#FFFFFF` (`--crm-text-inverse`). WCAG 2.x relative
  luminance and contrast ratio. Checks:
  - `PrimaryText` — `#FFFFFF` on the primary colour ≥ **4.5:1** (buttons, pagination, skip link; the same ratio covers
    `text-primary-600` links on the white page).
  - `AccentOnSurface` — the accent against `#FFFFFF` ≥ **3.0:1** (WCAG 1.4.11 non-text: the accent is an indicator
    colour only — active nav bar, portal header strip; it never carries text).
  A failing save → 422 `Organisation.BrandingContrastTooLow`, detail localised server-side (added to `serverWordedCodes`).
- **D7 — Scales are derived by the server, in OKLCH, anchored at step 600.** Light theme: step 600 is **exactly** the
  chosen colour; the other ten steps follow tokens.css's lightness/chroma ladder (B4). Dark theme: the anchor is the
  chosen colour moved in OKLCH lightness into a luminance window — primary `[0.115, 0.180]` (white text ≥ 4.5:1 **and**
  ≥ 3:1 against the dark surface `--crm-neutral-950`), accent `≥ 0.115` — then the same ladder. Dark derivation never
  fails, so the domain validates the light palette only. **Known limit:** `text-primary-600` links on the dark surface
  get ≥ 3:1, not 4.5:1 — one token cannot give both white-on-primary 4.5:1 and primary-on-black 4.5:1; a separate
  `--crm-link` token is a follow-up (pre-existing in Story 04's defaults).
- **D8 — An accent scale joins the tokens.** `--crm-accent-{50…950}` in tokens.css (defaults derived from `#0F766E`),
  mapped to Tailwind `accent-*` colours in main.css. Used by: the agent nav's active item (`border-s-4`) and the portal
  header's top strip (`border-t-4`). Nothing else changes colour role.
- **D9 — Arabic font from a fixed list:** `IbmPlexSansArabic` (default, bundled), `NotoKufiArabic`, `Tajawal` (two new
  `@fontsource` packages, R1). It applies to `html[lang='ar']` only (the chosen face first, then the existing fallback
  stack); English keeps IBM Plex Sans Arabic's Latin glyphs.
- **D10 — Defaults.** No saved branding → public DTO `tokens: null`, font `IbmPlexSansArabic`, display name = the
  organisation's `Name`, no logos: the frontend injects nothing and tokens.css applies. The admin form starts from
  primary `#145EC1` (≈ tokens.css `--crm-primary-600` `oklch(0.5 0.17 258)`, converted by hand during planning — B4
  confirms it with the converter and corrects the constant) and accent `#0F766E`.
- **D11 — Development seed brands DEMO** (the MVP demo wants a branded portal): "Demo Support Centre" / "مركز الدعم
  التجريبي", primary `#0E6655`, accent `#B7791F`, font `Tajawal`, a light and a dark SVG logo at well-known ids —
  applied only while the DEMO organisation was never branded (`Branding == null && !CanRevertBranding`), so an admin's
  revert to defaults is never overwritten on restart.
- **D12 — Caching.** `GetPublicBrandingQuery` and `GetCurrentBrandingQuery` are `ICacheableQuery` with the one tag
  `organisation:brandings`; `ChangeBrandingCommand` and `RevertBrandingCommand` invalidate it. One tag for every tenant
  (a command cannot put the organisation code in its tag without a read) — branding saves are rare. Logo bytes are **not**
  put in Redis; the logo response carries `Cache-Control: public, max-age=31536000, immutable` (content never changes
  for an id).
- **D13 — SSR cache on the Nuxt server.** The Nuxt server keeps the public branding per organisation code for **15 s**
  in memory (public data, no user state), so SSR does not spend the Public Area's 60/min per-IP budget (R6). A saved
  change reaches the portal within 15 s of the save.
- **D14 — No integration event, no audit row.** Nothing subscribes today (Audit, story 10, is deferred). Story 10 adds
  `BrandingChanged` when it lands.
- **D15 — Upload transport:** `POST /api/Admin/Branding/logos`, `multipart/form-data`, field `file`, request size limit
  1 MiB (so a 513 KB–1 MiB file reaches the domain and gets the localised 422; bigger is a 400 from the framework).
  The domain is the authority on 512 KB and type.

## Security design (named security risks — the reviewer reads this section first)

- **S1 — Anonymous reads of `Organisation` and `BrandLogo`.** Both are unscoped entities (no scope filter to lift).
  *Narrowing:* the public query returns only `PublicBrandingDto` (display name, logo ids, font, tokens) — never the
  organisation id, code list, row version, or membership data; an unknown or inactive code is 404. Logos are fetched by
  a random GUID only; there is no list endpoint. An uploaded-but-unsaved logo is fetchable by whoever has its id (the
  uploading admin); accepted.
- **S2 — Uploaded SVG.** *Narrowing, in depth:* (1) the domain sniffs content (PNG signature or UTF-8 XML text
  containing `<svg`) and ignores the file name/content type; (2) `SvgSanitizer` parses with `DtdProcessing.Prohibit`,
  `XmlResolver = null`, requires the root `{http://www.w3.org/2000/svg}svg`, keeps an **allow-list** of SVG elements
  (no `script`, `foreignObject`, `image`, `a`, `style`, animation elements), drops every `on*` attribute, every `style`
  attribute, every `href`/`xlink:href` not starting with `#`, and any attribute value containing `javascript:`,
  `data:` or a non-fragment `url(`; a parse failure or wrong root → 422 `Organisation.LogoTypeNotAllowed`; the stored
  bytes are the sanitised output; (3) the logo response sends `X-Content-Type-Options: nosniff` and
  `Content-Security-Policy: default-src 'none'; style-src 'unsafe-inline'; sandbox`; (4) the frontend shows logos only
  through `<img src>` (scripts never run in an image context), never inline.
- **S3 — CSS injection through tokens.** Token values are generated by the server from validated `#RRGGBB` input, never
  echoed. The frontend still re-validates every entry before writing the `<style>` (key
  `^--crm-(primary|accent)-(50|[1-9]00|950)$`, value `^#[0-9a-fA-F]{6}$`; anything else is dropped) and builds the
  font rule from a fixed map, never from the response string.
- **S4 — `[AllowAnonymousRequest]` on two HTTP-exposed queries** (`GetPublicBrandingQuery`, `GetBrandLogoQuery`). The
  attribute's doc says "never on a request an HTTP endpoint exposes for data that belongs to someone"; public branding is
  published on purpose. B10 extends the doc comment to name Story 58's two public reads explicitly.
- **S5 — Rate limiting.** Both anonymous endpoints are in the Public Area → per-IP global limiter (60/min). The
  admin endpoints are staff-only and not rate limited (as every Admin endpoint today).

---

## Backend Tasks

Order: **B1 → B2 → B3 → B4 → B5 → B6 → B7 → migration (R2) → B8 → B9 → B10 → B11**.
Namespace warning: **do not create a folder or namespace segment named `Branding`** in any Organisation project — inside
`Crm.Modules.Organisation.Application.*` a namespace `…Application.Branding` would shadow the domain type `Branding`
(enclosing-namespace lookup beats `using` directives). Domain types live flat in the Domain root (like the existing
ones); Application/Infrastructure helpers go under `Theming/`.

### B1 — Domain: colours and palette

**Create file: `backend/src/Modules/Organisation/Crm.Modules.Organisation.Domain/BrandColour.cs`**

```csharp
using System.Globalization;
using System.Text.RegularExpressions;
using Crm.SharedKernel;

namespace Crm.Modules.Organisation.Domain;

/// <summary>An sRGB colour as #RRGGBB (upper-case). WCAG 2.x luminance/contrast live here because the contrast
/// rule is a domain invariant (Story 58, D6).</summary>
public sealed partial class BrandColour : ValueObject
{
    public static readonly BrandColour White = Create("#FFFFFF");

    public string Hex { get; }
    public byte R { get; }
    public byte G { get; }
    public byte B { get; }

    private BrandColour(string hex, byte r, byte g, byte b) { Hex = hex; R = r; G = g; B = b; }

    public static BrandColour Create(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        string trimmed = hex.Trim();
        if (!HexPattern().IsMatch(trimmed))
        {
            throw new DomainException(new DomainError("Organisation.BrandColourInvalid", "A colour must be a hex value like #1A2B3C."));
        }
        byte Channel(int start) => byte.Parse(trimmed.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new BrandColour(trimmed.ToUpperInvariant(), Channel(1), Channel(3), Channel(5));
    }

    /// <summary>Linear-light channels in [0, 1] clamp and round to the nearest sRGB byte.</summary>
    public static BrandColour FromLinearRgb(double r, double g, double b) { … Encode each: c <= 0.0031308 ? 12.92c : 1.055·c^(1/2.4) − 0.055; clamp [0,1]; round(·255); format "#{R:X2}{G:X2}{B:X2}" … }

    public (double R, double G, double B) ToLinearRgb() => (Linear(R), Linear(G), Linear(B));

    /// <summary>WCAG 2.x relative luminance.</summary>
    public double RelativeLuminance { get { var (r, g, b) = ToLinearRgb(); return (0.2126 * r) + (0.7152 * g) + (0.0722 * b); } }

    public double ContrastWith(BrandColour other) { ArgumentNullException.ThrowIfNull(other); double a = RelativeLuminance, b = other.RelativeLuminance; return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05); }

    private static double Linear(byte channel) { double c = channel / 255.0; return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }

    protected override IEnumerable<object?> GetEqualityComponents() { yield return Hex; }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexPattern();
}
```

(The `…` bodies are one-liners as described; write them out.)

**Create file: `…Domain/ContrastCheck.cs`**

```csharp
namespace Crm.Modules.Organisation.Domain;

/// <summary>One WCAG AA check of a palette (D6). Name is one of ContrastCheck.Names.</summary>
public sealed record ContrastCheck(string Name, double Ratio, double Required)
{
    public bool Passes => Ratio >= Required;

    public static class Names
    {
        public const string PrimaryText = "PrimaryText";
        public const string AccentOnSurface = "AccentOnSurface";
    }
}
```

**Create file: `…Domain/BrandPalette.cs`** — `sealed class BrandPalette : ValueObject` with `BrandColour Primary`,
`BrandColour Accent`; constants `public const double TextContrast = 4.5; public const double NonTextContrast = 3.0;`;

```csharp
public static IReadOnlyList<ContrastCheck> Evaluate(BrandColour primary, BrandColour accent) =>
[
    new(ContrastCheck.Names.PrimaryText, BrandColour.White.ContrastWith(primary), TextContrast),
    new(ContrastCheck.Names.AccentOnSurface, BrandColour.White.ContrastWith(accent), NonTextContrast),
];

public static BrandPalette Create(BrandColour primary, BrandColour accent)
{
    ArgumentNullException.ThrowIfNull(primary);
    ArgumentNullException.ThrowIfNull(accent);
    ContrastCheck? failing = Evaluate(primary, accent).FirstOrDefault(c => !c.Passes);
    if (failing is not null)
    {
        throw new DomainException(new DomainError("Organisation.BrandingContrastTooLow", string.Create(CultureInfo.InvariantCulture,
            $"{failing.Name} contrast is {failing.Ratio:0.00}:1; WCAG AA needs {failing.Required:0.0}:1.")));
    }
    return new BrandPalette(primary, accent);
}
```
Equality components: `Primary`, `Accent`.

**Create file: `…Domain/ArabicFont.cs`** — `public enum ArabicFont { IbmPlexSansArabic = 0, NotoKufiArabic = 1, Tajawal = 2 }`.

### B2 — Domain: `BrandLogo` aggregate

**Create file: `…Domain/BrandLogoId.cs`** — `public sealed record BrandLogoId(Guid Value) : StronglyTypedId(Value) { public static BrandLogoId New() => new(Guid.NewGuid()); }` (copy `MembershipId.cs`).

**Create file: `…Domain/LogoContentType.cs`** — `public enum LogoContentType { Png = 0, Svg = 1 }`.

**Create file: `…Domain/BrandLogo.cs`**

```csharp
/// <summary>An uploaded logo (Story 58, D3). NOT ITenantScoped on purpose (D4, like Organisation): the portal sign-in
/// page fetches it anonymously by id before any scope exists. It carries TenantId; ChangeBrandingCommandHandler
/// refuses a logo of another tenant. Immutable after Create.</summary>
public sealed class BrandLogo : AuditableEntity<BrandLogoId>, IHasRowVersion
{
    public const int MaxBytes = 512 * 1024;

    public TenantId TenantId { get; private set; } = TenantId.None;
    public LogoContentType ContentType { get; private set; }
    public byte[] Content { get; private set; } = [];
    public byte[] RowVersion { get; private set; } = [];

    private BrandLogo() { }
    private BrandLogo(BrandLogoId id, TenantId tenantId, LogoContentType contentType, byte[] content) : base(id) { … }

    /// <summary>Content sniffing (never the file name or the declared type): PNG signature 89 50 4E 47 0D 0A 1A 0A, or
    /// strict UTF-8 text (BOM allowed) whose first non-whitespace char is '<' and that contains "<svg"
    /// (OrdinalIgnoreCase). Throws Organisation.LogoEmpty / LogoTooLarge (> MaxBytes) / LogoTypeNotAllowed.</summary>
    public static LogoContentType Sniff(byte[] content) { … new UTF8Encoding(false, throwOnInvalidBytes: true); catch DecoderFallbackException → not SVG … }

    public static BrandLogo Create(TenantId tenantId, byte[] content, BrandLogoId? id = null)
    {
        if (tenantId.Equals(TenantId.None)) { throw new DomainException(new DomainError("Organisation.TenantRequired", "A logo needs a tenant.")); }
        LogoContentType type = Sniff(content);
        return new BrandLogo(id ?? BrandLogoId.New(), tenantId, type, [.. content]);
    }

    public bool BelongsTo(TenantId tenant) => TenantId.Equals(tenant);

    public string MediaType => ContentType == LogoContentType.Png ? "image/png" : "image/svg+xml";
}
```

Messages: `LogoEmpty` "The logo file is empty.", `LogoTooLarge` "The logo is larger than 512 KB.", `LogoTypeNotAllowed`
"The logo must be a PNG or SVG image."

### B3 — Domain: `Branding` value object and the aggregate methods

**Create file: `…Domain/Branding.cs`** — `sealed class Branding : ValueObject` with `LocalizedText DisplayName`,
`BrandPalette Palette`, `ArabicFont ArabicFont`, `BrandLogoId? LightLogo`, `BrandLogoId? DarkLogo`; private ctor;

```csharp
public static Branding Create(LocalizedText displayName, BrandPalette palette, ArabicFont arabicFont, BrandLogoId? lightLogo, BrandLogoId? darkLogo)
{
    ArgumentNullException.ThrowIfNull(displayName);
    ArgumentNullException.ThrowIfNull(palette);
    if (!Enum.IsDefined(arabicFont)) { throw new DomainException(new DomainError("Organisation.ArabicFontUnsupported", "The Arabic font is not one of the supported fonts.")); }
    if (darkLogo is not null && lightLogo is null) { throw new DomainException(new DomainError("Organisation.DarkLogoWithoutLight", "A dark-theme logo needs a light-theme logo.")); }
    return new Branding(displayName, palette, arabicFont, lightLogo, darkLogo);
}
```
Equality components: `DisplayName, Palette, ArabicFont, LightLogo, DarkLogo`.

**File: `…Domain/Organisation.cs`** — after line 16 add:

```csharp
/// <summary>null = the product defaults (tokens.css). Story 58, D1.</summary>
public Branding? Branding { get; private set; }

/// <summary>The version Revert restores; null is the product defaults. Meaningful only when CanRevertBranding.</summary>
public Branding? PreviousBranding { get; private set; }

public bool CanRevertBranding { get; private set; }
```
and after `Create` (line 31):

```csharp
/// <summary>Saving the branding already in effect changes nothing - the real previous version survives (D2).</summary>
public void ChangeBranding(Branding branding)
{
    ArgumentNullException.ThrowIfNull(branding);
    if (branding.Equals(Branding)) { return; }
    PreviousBranding = Branding;
    Branding = branding;
    CanRevertBranding = true;
}

/// <summary>Swaps current and previous (D2): reverting twice re-applies.</summary>
public void RevertBranding()
{
    if (!CanRevertBranding) { throw new DomainException(new DomainError("Organisation.NoPreviousBranding", "There is no previous branding to revert to.")); }
    (Branding, PreviousBranding) = (PreviousBranding, Branding);
}
```

### B4 — Application: tokens, localisation, errors, cache keys

**Create file: `…Application/Theming/Oklch.cs`** — `internal readonly record struct Oklch(double L, double C, double H)`
(H in radians) with Björn Ottosson's OKLab matrices:

```csharp
public static Oklch FromColour(BrandColour colour)
{
    (double r, double g, double b) = colour.ToLinearRgb();
    double l = Math.Cbrt((0.4122214708 * r) + (0.5363325363 * g) + (0.0514459929 * b));
    double m = Math.Cbrt((0.2119034982 * r) + (0.6806995451 * g) + (0.1073969566 * b));
    double s = Math.Cbrt((0.0883024619 * r) + (0.2817188376 * g) + (0.6299787005 * b));
    double okL = (0.2104542553 * l) + (0.7936177850 * m) - (0.0040720468 * s);
    double a = (1.9779984951 * l) - (2.4285922050 * m) + (0.4505937099 * s);
    double bb = (0.0259040371 * l) + (0.7827717662 * m) - (0.8086757660 * s);
    return new Oklch(okL, Math.Sqrt((a * a) + (bb * bb)), Math.Atan2(bb, a));
}

public (double R, double G, double B) ToLinearRgb()
{
    double a = C * Math.Cos(H), b = C * Math.Sin(H);
    double l = Math.Pow(L + (0.3963377774 * a) + (0.2158037573 * b), 3);
    double m = Math.Pow(L - (0.1055613458 * a) - (0.0638541728 * b), 3);
    double s = Math.Pow(L - (0.0894841775 * a) - (1.2914855480 * b), 3);
    return ((4.0767416621 * l) - (3.3077115913 * m) + (0.2309699292 * s),
            (-1.2684380046 * l) + (2.6097574011 * m) - (0.3413193965 * s),
            (-0.0041960863 * l) - (0.7034186147 * m) + (1.7076147010 * s));
}

public bool InGamut() { var (r, g, b) = ToLinearRgb(); const double e = 1e-6; return r >= -e && r <= 1 + e && g >= -e && g <= 1 + e && b >= -e && b <= 1 + e; }

/// <summary>Keeps L and H; bisects chroma (24 steps) down to the largest in-gamut value.</summary>
public Oklch ClampChroma() { if (InGamut()) return this; double lo = 0, hi = C; for (int i = 0; i < 24; i++) { double mid = (lo + hi) / 2; if ((this with { C = mid }).InGamut()) lo = mid; else hi = mid; } return this with { C = lo }; }

public BrandColour ToColour() { var (r, g, b) = ClampChroma().ToLinearRgb(); return BrandColour.FromLinearRgb(r, g, b); }
```

**Create file: `…Application/Theming/BrandTokenGenerator.cs`** — `public static class BrandTokenGenerator`:

```csharp
// tokens.css primary ladder (lines 11-21): lightness and chroma / 0.17 per step.
private static readonly (string Step, double L, double Chroma)[] Ladder =
[
    ("50", 0.97, 0.12), ("100", 0.93, 0.26), ("200", 0.87, 0.47), ("300", 0.79, 0.65), ("400", 0.70, 0.82),
    ("500", 0.60, 1.0), ("600", 0.50, 1.0), ("700", 0.42, 0.88), ("800", 0.35, 0.71), ("900", 0.28, 0.53), ("950", 0.19, 0.35),
];

public const double DarkMinLuminance = 0.115;   // >= 3:1 against --crm-neutral-950 (D7)
public const double DarkPrimaryMaxLuminance = 0.180; // white text >= 4.5:1 (D7)

public static BrandTokensDto Generate(BrandColour primary, BrandColour accent)
{
    var light = new Dictionary<string, string>(StringComparer.Ordinal);
    var dark = new Dictionary<string, string>(StringComparer.Ordinal);
    AddScale(light, "primary", primary);
    AddScale(light, "accent", accent);
    AddScale(dark, "primary", ForDark(primary, DarkMinLuminance, DarkPrimaryMaxLuminance));
    AddScale(dark, "accent", ForDark(accent, DarkMinLuminance, 1.0));
    return new BrandTokensDto(light, dark);
}
```
- `AddScale(map, name, anchor)`: `Oklch a = Oklch.FromColour(anchor)`; for each ladder step: `"600"` → `anchor.Hex`
  exactly; lighter steps (`L > 0.50`): `L = a.L >= 0.97 ? a.L : a.L + ((0.97 - a.L) * (step.L - 0.50) / 0.47)`; darker
  steps: `L = a.L * step.L / 0.50`; `C = a.C * step.Chroma`; hue `a.H`; `map[$"--crm-{name}-{step}"] = new Oklch(L, C, a.H).ToColour().Hex`.
- `ForDark(colour, min, max)`: `lum = colour.RelativeLuminance`; inside `[min, max]` → `colour`. Below `min`: bisect L in
  `[a.L, 1]` (24 steps, colour = `new Oklch(L, a.C, a.H).ToColour()`) for the smallest L with luminance ≥ `min`. Above
  `max`: bisect L in `[0, a.L]` for the largest L with luminance ≤ `max`. Return the colour on the safe side.
- Also `public static class BrandDefaults { public const string Primary = "#145EC1"; public const string Accent = "#0F766E"; }`
  in the same folder (D10). **First run check (R8):** convert `oklch(0.5 0.17 258)` with `Oklch.ToColour()` once
  (e.g. a temporary line in the dev seeder, then delete it) and set `Primary` to that output if it differs.

**Create file: `…Application/Localization/OrganisationMessageKeys.cs`**

```csharp
namespace Crm.Modules.Organisation.Application.Localization;

/// <summary>Every resource key in Resources/OrganisationMessages*.resx (Story 58).</summary>
public static class OrganisationMessageKeys
{
    public static class Validation
    {
        public const string ColourInvalid = "Organisation.Validation.ColourInvalid";
        public const string ArabicFontUnsupported = "Organisation.Validation.ArabicFontUnsupported";
        public const string OrganisationCodeInvalid = "Organisation.Validation.OrganisationCodeInvalid";
    }

    public static class Refusal
    {
        public const string ContrastTooLow = "Organisation.Refusal.ContrastTooLow";
    }

    public static class Contrast
    {
        public const string PrimaryText = "Organisation.Contrast.PrimaryText";
        public const string AccentOnSurface = "Organisation.Contrast.AccentOnSurface";
    }
}
```

**Create files: `…Application/Localization/Resources/OrganisationMessages.resx` and `OrganisationMessages.ar.resx`**
(copy the header of `TicketsMessages.resx`; **Write tool** for the Arabic file):

| Key | en | ar |
|---|---|---|
| `Organisation.Validation.ColourInvalid` | A colour must be a hex value like #1A2B3C. | يجب أن يكون اللون قيمة سداسية عشرية مثل #1A2B3C. |
| `Organisation.Validation.ArabicFontUnsupported` | The Arabic font must be one of IbmPlexSansArabic, NotoKufiArabic, Tajawal. | يجب أن يكون الخط العربي أحد الخطوط: IbmPlexSansArabic أو NotoKufiArabic أو Tajawal. |
| `Organisation.Validation.OrganisationCodeInvalid` | The organisation code must be 2-20 characters of A-Z, 0-9, or '-'. | يجب أن يتكون رمز المنشأة من 2 إلى 20 حرفاً من A-Z أو 0-9 أو '-'. |
| `Organisation.Refusal.ContrastTooLow` | {0} has a contrast of {1}:1, but WCAG AA needs at least {2}:1. Choose a darker colour. | {0} بنسبة تباين {1}:1، بينما يتطلب معيار WCAG AA ‏{2}:1 على الأقل. اختر لوناً أغمق. |
| `Organisation.Contrast.PrimaryText` | Text on the primary colour | النص على اللون الأساسي |
| `Organisation.Contrast.AccentOnSurface` | The accent colour on the page background | لون التمييز على خلفية الصفحة |

**Create file: `…Application/OrganisationErrors.cs`** — `public static class OrganisationErrors` with
`OrganisationNotFound()` → `Error.NotFound("Organisation.OrganisationNotFound", "The organisation was not found.")`,
`BrandLogoNotFound(Guid id)` → `Error.NotFound("Organisation.BrandLogoNotFound", $"Logo '{id}' was not found.")`,
`NoPreviousBranding()` → `Error.Domain("Organisation.NoPreviousBranding", "There is no previous branding to revert to.")`,
`LogoTypeNotAllowed()` → `Error.Domain("Organisation.LogoTypeNotAllowed", "The logo must be a PNG or SVG image.")`,
`ContrastTooLow(string detail)` → `Error.Domain("Organisation.BrandingContrastTooLow", detail)`.

**Create file: `…Application/Theming/BrandingRefusals.cs`** — `internal static class BrandingRefusals` (copy
`TicketRefusals.cs` lines 9–24):

```csharp
public static Error? ContrastTooLow(IReadOnlyList<ContrastCheck> checks, ILocalizationService localization)
{
    ArgumentNullException.ThrowIfNull(checks);
    ArgumentNullException.ThrowIfNull(localization);
    ContrastCheck? failing = checks.FirstOrDefault(c => !c.Passes);
    return failing is null ? null : OrganisationErrors.ContrastTooLow(string.Format(
        CultureInfo.CurrentCulture, localization.Get(OrganisationMessageKeys.Refusal.ContrastTooLow),
        CheckName(failing.Name, localization),
        failing.Ratio.ToString("0.00", CultureInfo.InvariantCulture),
        failing.Required.ToString("0.0", CultureInfo.InvariantCulture)));
}

public static string CheckName(string name, ILocalizationService localization) => localization.Get(name switch
{
    ContrastCheck.Names.PrimaryText => OrganisationMessageKeys.Contrast.PrimaryText,
    _ => OrganisationMessageKeys.Contrast.AccentOnSurface,
});
```
Numbers use `InvariantCulture` on purpose (Latin digits and `.`; the frontend wraps them in `<bdi dir="ltr">`).

**Create file: `…Application/Validation/OrganisationRules.cs`** (copy `CustomersRules.cs` lines 8–19):
`HexColour<T>(this IRuleBuilder<T, string> rule, ILocalizationService l)` → `.Matches("^#[0-9A-Fa-f]{6}$").WithMessage(_ => l.Get(OrganisationMessageKeys.Validation.ColourInvalid))`;
`SupportedArabicFont<T>(…)` → `.Must(f => Enum.GetNames<ArabicFont>().Contains(f, StringComparer.Ordinal))` with
`ArabicFontUnsupported`; `OrganisationCode<T>(…)` → `.Matches("^[A-Za-z0-9-]{2,20}$")` with `OrganisationCodeInvalid`.

**Create file: `…Application/OrganisationCacheKeys.cs`**

```csharp
/// <summary>Cache keys and tags: {module}:{aggregate-plural}:{id} (D12).</summary>
public static class OrganisationCacheKeys
{
    /// <summary>One tag for every tenant's branding - a branding command cannot name the organisation code.</summary>
    public const string Brandings = "organisation:brandings";

    public static string PublicBranding(string organisationCode) => $"organisation:brandings:code:{organisationCode.Trim().ToUpperInvariant()}";

    /// <summary>CachingBehavior prefixes the caller's tenant, so the current tenant needs no id in the key.</summary>
    public const string CurrentBranding = "organisation:brandings:current";
}
```

**Create file: `…Application/Theming/ISvgSanitizer.cs`** — `public interface ISvgSanitizer { /// null when not a well-formed SVG document. byte[]? Sanitize(byte[] svg); }`.

**File: `…Application/Dtos.cs`** — append (primitives only):

```csharp
public sealed record BrandTokensDto(IReadOnlyDictionary<string, string> Light, IReadOnlyDictionary<string, string> Dark);

public sealed record BrandingDto(LocalizedTextDto DisplayName, string PrimaryColour, string AccentColour, string ArabicFont, Guid? LightLogoId, Guid? DarkLogoId)
{
    public static BrandingDto FromDomain(Branding branding) => new(
        LocalizedTextDto.FromDomain(branding.DisplayName), branding.Palette.Primary.Hex, branding.Palette.Accent.Hex,
        branding.ArabicFont.ToString(), branding.LightLogo?.Value, branding.DarkLogo?.Value);

    /// <summary>The product defaults, shown in the admin form when nothing is saved (D10).</summary>
    public static BrandingDto Defaults(LocalizedText organisationName) => new(
        LocalizedTextDto.FromDomain(organisationName), BrandDefaults.Primary, BrandDefaults.Accent, nameof(ArabicFont.IbmPlexSansArabic), null, null);
}

/// <summary>Admin view. Current is the effective branding (defaults when IsCustom is false); Previous null with CanRevert = the defaults.</summary>
public sealed record BrandingSettingsDto(LocalizedTextDto OrganisationName, BrandingDto Current, bool IsCustom, BrandingDto? Previous, bool CanRevert, byte[] RowVersion);

/// <summary>Anonymous view (S1). Tokens null = keep tokens.css.</summary>
public sealed record PublicBrandingDto(LocalizedTextDto DisplayName, Guid? LightLogoId, Guid? DarkLogoId, string ArabicFont, BrandTokensDto? Tokens)
{
    public static PublicBrandingDto FromDomain(Domain.Organisation organisation) => organisation.Branding is { } b
        ? new(LocalizedTextDto.FromDomain(b.DisplayName), b.LightLogo?.Value, b.DarkLogo?.Value, b.ArabicFont.ToString(),
              BrandTokenGenerator.Generate(b.Palette.Primary, b.Palette.Accent))
        : new(LocalizedTextDto.FromDomain(organisation.Name), null, null, nameof(ArabicFont.IbmPlexSansArabic), null);
}

public sealed record ContrastCheckDto(string Name, double Ratio, double Required, bool Passes);

public sealed record BrandingPreviewDto(BrandTokensDto Tokens, IReadOnlyList<ContrastCheckDto> Checks);

public sealed record BrandLogoDto(Guid Id, string ContentType, int SizeBytes);

public sealed record BrandLogoContentDto(byte[] Content, string ContentType);
```
Add a static `BrandingSettingsDto.FromDomain(Domain.Organisation o)` building `Current` = `o.Branding` or `Defaults(o.Name)`,
`IsCustom = o.Branding is not null`, `Previous = o.PreviousBranding is null ? null : BrandingDto.FromDomain(...)`,
`CanRevert = o.CanRevertBranding`, `RowVersion = o.RowVersion`.

### B5 — Application: specifications, queries, commands

**Create file: `…Application/Specifications/OrganisationByCodeSpecification.cs`** — copy `BranchByCodeSpecification.cs`
lines 6–14 for `Domain.Organisation` (`Where(o => o.Code.Equals(code)); AsReadOnly();`).

Every request below: one folder `Queries|Commands/<UseCase>/` with the record, handler, and (when it has input) validator.

1. **`Queries/GetPublicBranding/GetPublicBrandingQuery.cs`**

```csharp
/// <summary>The portal sign-in pages' branding, anonymous by design (S1, S4). Cached; ChangeBranding/RevertBranding invalidate.</summary>
[AllowAnonymousRequest]
public sealed record GetPublicBrandingQuery(string Organisation) : IRequest<Result<PublicBrandingDto>>, ICacheableQuery
{
    public string CacheKey => OrganisationCacheKeys.PublicBranding(Organisation);
    public IReadOnlyCollection<string> CacheTags => [OrganisationCacheKeys.Brandings];
}
```
Validator: `RuleFor(q => q.Organisation).NotEmpty().OrganisationCode(localization)` (constructor-inject
`ILocalizationService`, as `CreateCustomerCommandValidator` does). Handler: `repository.FirstOrDefaultAsync(new
OrganisationByCodeSpecification(OrganisationCode.Create(request.Organisation)))`; `null` or `!IsActive` →
`OrganisationErrors.OrganisationNotFound()`; else `PublicBrandingDto.FromDomain(org)`.

2. **`Queries/GetCurrentBranding/GetCurrentBrandingQuery.cs`** — `[RequireAuthenticatedUser]`, `ICacheableQuery` with
`CacheKey => OrganisationCacheKeys.CurrentBranding`, tag `Brandings`. Handler: `GetByIdAsync(currentUser.Scope.Tenant)`;
null → `OrganisationNotFound`; else `PublicBrandingDto.FromDomain`.

3. **`Queries/GetBrandLogo/GetBrandLogoQuery.cs`** — `[AllowAnonymousRequest] sealed record GetBrandLogoQuery(Guid Id) :
IRequest<Result<BrandLogoContentDto>>` (**not** cacheable, D12). Validator `NotEmpty`. Handler:
`IRepository<BrandLogo, BrandLogoId>.GetByIdAsync` → null → `BrandLogoNotFound(id)`; else `new(logo.Content, logo.MediaType)`.

4. **`Queries/GetBrandingSettings/GetBrandingSettingsQuery.cs`** — `[RequirePermission(CrmPermissions.AdminBranding)]`,
no input, not cached (the admin needs the live row version). Handler: org by `currentUser.Scope.Tenant` → `BrandingSettingsDto.FromDomain`.

5. **`Queries/PreviewBranding/PreviewBrandingQuery.cs`** — `[RequirePermission(CrmPermissions.AdminBranding)] sealed record
PreviewBrandingQuery(string PrimaryColour, string AccentColour) : IRequest<Result<BrandingPreviewDto>>`. Validator:
`HexColour` on both. Handler (no repository): parse both with `BrandColour.Create`, `Evaluate`, return
`new BrandingPreviewDto(BrandTokenGenerator.Generate(p, a), checks.Select(c => new ContrastCheckDto(c.Name, Math.Round(c.Ratio, 2), c.Required, c.Passes)).ToList())`.
A failing palette **still returns 200** with `Passes = false` — the preview must show bad colours.

6. **`Commands/UploadBrandLogo/UploadBrandLogoCommand.cs`** — `[RequirePermission(CrmPermissions.AdminBranding)] sealed
record UploadBrandLogoCommand(byte[] Content) : ICommand<BrandLogoDto>`. Validator: `RuleFor(c => c.Content).NotNull()`.
Handler (inject `IRepository<BrandLogo, BrandLogoId>`, `IOrganisationUnitOfWork`, `ICurrentUserService`, `ISvgSanitizer`):

```csharp
byte[] content = request.Content;
if (BrandLogo.Sniff(content) == LogoContentType.Svg)   // throws LogoEmpty/LogoTooLarge/LogoTypeNotAllowed -> 422
{
    byte[]? clean = sanitizer.Sanitize(content);
    if (clean is null) { return Result.Failure<BrandLogoDto>(OrganisationErrors.LogoTypeNotAllowed()); }
    content = clean;
}
BrandLogo logo = BrandLogo.Create(currentUser.Scope.Tenant, content);
repository.Add(logo);
await unitOfWork.SaveChangesAsync(cancellationToken);
return Result.Success(new BrandLogoDto(logo.Id.Value, logo.MediaType, logo.Content.Length));
```

7. **`Commands/ChangeBranding/ChangeBrandingCommand.cs`**

```csharp
[RequirePermission(CrmPermissions.AdminBranding)]
public sealed record ChangeBrandingCommand(
    string DisplayNameAr, string DisplayNameEn, string PrimaryColour, string AccentColour, string ArabicFont,
    Guid? LightLogoId, Guid? DarkLogoId, byte[] RowVersion) : ICommand<BrandingSettingsDto>, ICacheInvalidatingCommand
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [OrganisationCacheKeys.Brandings];
}
```
Validator: names `NotEmpty().MaximumLength(200)`, colours `HexColour`, font `SupportedArabicFont`, `RowVersion NotEmpty`.
Handler (inject org repository, logo repository, unit of work, `ICurrentUserService`, `ILocalizationService`):
org by `currentUser.Scope.Tenant` → null → `OrganisationNotFound`; `SetOriginalRowVersion(org, request.RowVersion)`;
`primary`/`accent` = `BrandColour.Create`; `if (BrandingRefusals.ContrastTooLow(BrandPalette.Evaluate(primary, accent), localization) is { } refusal) return Result.Failure<BrandingSettingsDto>(refusal);`;
for each non-null logo id: `GetByIdAsync(new BrandLogoId(id))` → `null` or `!logo.BelongsTo(tenant)` →
`BrandLogoNotFound(id)` (D4); `org.ChangeBranding(Branding.Create(LocalizedText.Create(ar, en), BrandPalette.Create(primary, accent), Enum.Parse<ArabicFont>(request.ArabicFont), light, dark))`;
save; `Result.Success(BrandingSettingsDto.FromDomain(org))`.

8. **`Commands/RevertBranding/RevertBrandingCommand.cs`** — `(byte[] RowVersion)`, same attribute, same
`ICacheInvalidatingCommand` tag; validator `RowVersion NotEmpty`. Handler: org; `SetOriginalRowVersion`;
`if (!org.CanRevertBranding) return Result.Failure<BrandingSettingsDto>(OrganisationErrors.NoPreviousBranding());`
(pre-check; the aggregate throws the same code as the backstop); `org.RevertBranding()`; save; settings DTO.

### B6 — Infrastructure: persistence, sanitiser, module wiring

**Create file: `…Infrastructure/Persistence/Converters/BrandingConverter.cs`**

```csharp
/// <summary>Branding <-> JSON (D1). A ValueConverter for a value object is the CLAUDE.md-sanctioned mapping. EF never
/// passes null to a converter, so null stays SQL NULL (= product defaults).</summary>
public sealed class BrandingConverter() : ValueConverter<Branding, string>(b => Serialize(b), s => Deserialize(s))
{
    private sealed record Row(string NameAr, string NameEn, string Primary, string Accent, string ArabicFont, Guid? LightLogo, Guid? DarkLogo);

    private static string Serialize(Branding b) => JsonSerializer.Serialize(new Row(b.DisplayName.Arabic, b.DisplayName.English,
        b.Palette.Primary.Hex, b.Palette.Accent.Hex, b.ArabicFont.ToString(), b.LightLogo?.Value, b.DarkLogo?.Value));

    private static Branding Deserialize(string json)
    {
        Row row = JsonSerializer.Deserialize<Row>(json) ?? throw new InvalidOperationException("Stored branding is empty.");
        return Branding.Create(LocalizedText.Create(row.NameAr, row.NameEn),
            BrandPalette.Create(BrandColour.Create(row.Primary), BrandColour.Create(row.Accent)),
            Enum.Parse<ArabicFont>(row.ArabicFont),
            row.LightLogo is { } l ? new BrandLogoId(l) : null, row.DarkLogo is { } d ? new BrandLogoId(d) : null);
    }
}
```

**File: `…Infrastructure/Persistence/Configurations/OrganisationConfiguration.cs`** — before line 28 add:

```csharp
// Story 58 (D1): the branding value objects as JSON; NULL = product defaults.
builder.Property(x => x.Branding).HasConversion(new BrandingConverter()).HasColumnName("Branding").HasMaxLength(4000);
builder.Property(x => x.PreviousBranding).HasConversion(new BrandingConverter()).HasColumnName("PreviousBranding").HasMaxLength(4000);
builder.Property(x => x.CanRevertBranding).IsRequired();
```

**Create file: `…Infrastructure/Persistence/Configurations/BrandLogoConfiguration.cs`** — `ToTable("BrandLogos")`, key,
`Id` conversion `id => id.Value, v => new BrandLogoId(v)` + `ValueGeneratedNever()` (like `MembershipConfiguration.cs`
lines 17–19); `ContentType` `.HasConversion<string>().HasMaxLength(10)`; `Content` `.IsRequired()` (varbinary(max));
`RowVersion` `.IsRowVersion()`; `builder.HasIndex(x => x.TenantId)`; ignore `MediaType` (`builder.Ignore(x => x.MediaType)` — it is a computed getter).

**File: `…Infrastructure/Persistence/OrganisationDbContext.cs`** — after line 21: `public DbSet<Domain.BrandLogo> BrandLogos => Set<Domain.BrandLogo>();`

**Create file: `…Infrastructure/Theming/SvgSanitizer.cs`** — `public sealed class SvgSanitizer : ISvgSanitizer` (S2):

```csharp
private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";
private static readonly XNamespace XLink = "http://www.w3.org/1999/xlink";
private static readonly FrozenSet<string> AllowedElements = new[]
{
    "svg", "g", "path", "rect", "circle", "ellipse", "line", "polyline", "polygon", "text", "tspan", "defs",
    "linearGradient", "radialGradient", "stop", "clipPath", "mask", "title", "desc", "use", "symbol",
}.ToFrozenSet(StringComparer.Ordinal);

public byte[]? Sanitize(byte[] svg)
{
    ArgumentNullException.ThrowIfNull(svg);
    var settings = new XmlReaderSettings
    {
        DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 2_000_000,
        IgnoreComments = true, IgnoreProcessingInstructions = true,
    };
    XDocument document;
    try
    {
        using var input = new MemoryStream(svg);
        using var reader = XmlReader.Create(input, settings);
        document = XDocument.Load(reader);
    }
    catch (XmlException) { return null; }

    if (document.Root is not { } root || root.Name != Svg + "svg") { return null; }
    Clean(root);

    using var output = new MemoryStream();
    using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false) }))
    {
        new XDocument(root).Save(writer);
    }
    return output.ToArray();
}
```
- `Clean(XElement e)`: for each child element (snapshot with `.ToList()`): namespace ≠ `Svg` or local name not in
  `AllowedElements` → `Remove()`, else recurse; then remove every attribute for which `IsAllowed` is false.
- `IsAllowed(XAttribute a)`: namespace declarations → true; local name starts with `on` (OrdinalIgnoreCase) → false;
  `style` → false; local name `href` (no namespace or `XLink`) → true only when the trimmed value starts with `#`;
  any other namespaced attribute except `XNamespace.Xml` → false; value contains `javascript:` or `data:`
  (OrdinalIgnoreCase) → false; value contains `url(` and not only `url(#…)` fragments (regex `url\(\s*[^#\s)]`) → false;
  else true.

**File: `…Infrastructure/OrganisationModule.cs`** — after line 27:

```csharp
services.AddRepository<BrandLogo, BrandLogoId, OrganisationDbContext>();
services.AddSingleton<ISvgSanitizer, SvgSanitizer>();

// Story 58: branding validation and refusal messages follow Accept-Language.
services.AddLocalizationResource(new ResourceManager(
    "Crm.Modules.Organisation.Application.Localization.Resources.OrganisationMessages", ApplicationAssembly));
```
(add `using System.Resources;`, `using Crm.BuildingBlocks.Application.Localization;`, the `Theming` namespaces).

### B7 — Development seed (D11)

**File: `backend/src/Modules/Organisation/Crm.Modules.Organisation.Contracts/DevSeed/OrganisationDevSeed.cs`** — after
line 33: `public static readonly Guid DemoLightLogoId = Guid.Parse("06000000-0000-0000-0005-000000000001");` and
`DemoDarkLogoId = …-0005-000000000002`.

**File: `…Infrastructure/Persistence/OrganisationSeeder.cs`** — at the end of `SeedAsync` (after line 82) call
`await EnsureDemoBrandingAsync(tenantId, cancellationToken);`:
- two `const string` SVGs (160×40 viewBox: a rounded square + a triangle + the text `DEMO`; light: text `#0E6655`,
  dark: text `#FFFFFF`); for each well-known logo id not present → `context.BrandLogos.Add(BrandLogo.Create(tenantId,
  Encoding.UTF8.GetBytes(svg), new BrandLogoId(id)))`; `SaveChangesAsync`.
- load the DEMO organisation **tracked** (`context.Organisations.SingleAsync(o => o.Id == tenantId)`); if
  `org.Branding is null && !org.CanRevertBranding` → `org.ChangeBranding(Branding.Create(LocalizedText.Create("مركز الدعم
  التجريبي", "Demo Support Centre"), BrandPalette.Create(BrandColour.Create("#0E6655"), BrandColour.Create("#B7791F")),
  ArabicFont.Tajawal, new BrandLogoId(OrganisationDevSeed.DemoLightLogoId), new BrandLogoId(OrganisationDevSeed.DemoDarkLogoId)))`;
  `SaveChangesAsync`. (Planning computed white-on-`#0E6655` ≈ 6.9:1 and white-vs-`#B7791F` ≈ 3.6:1 by hand — the
  preview endpoint confirms, R8.) **Write tool** for this file (Arabic literal).

### B8 — Presentation

**Create file: `…Presentation/Controllers/Public/BrandingController.cs`**

```csharp
/// <summary>Story 58: public branding for the portal sign-in pages. Public Area: anonymous and rate limited per IP by
/// the global limiter (AREAS.md) - no [AllowAnonymous] needed or allowed.</summary>
[Area(CrmAreas.Public)]
public sealed class BrandingController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PublicBrandingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicBrandingDto>> Get([FromQuery] string organisation, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetPublicBrandingQuery(organisation), cancellationToken));

    [HttpGet("logos/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Logo(Guid id, CancellationToken cancellationToken)
    {
        Result<BrandLogoContentDto> result = await Sender.Send(new GetBrandLogoQuery(id), cancellationToken);
        if (result.IsFailure) { return Problem(result.Error); }
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; sandbox";
        return File(result.Value.Content, result.Value.ContentType);
    }
}
```
(`Result` needs `using Crm.SharedKernel.Results;` — the csproj already references `Crm.SharedKernel`, line 11.)

**Create file: `…Presentation/Controllers/Agent/BrandingController.cs`** — `[Area(CrmAreas.Agent)]`, one `[HttpGet]`
→ `GetCurrentBrandingQuery`, 200 `PublicBrandingDto`, 401/403/404.

**Create file: `…Presentation/Controllers/Admin/BrandingController.cs`** — `[Area(CrmAreas.Admin)]`, class-level 401/403
like `DepartmentsController.cs` lines 12–14:
- `[HttpGet]` → `GetBrandingSettingsQuery` (200 `BrandingSettingsDto`).
- `[HttpGet("preview")]` `([FromQuery] string primaryColour, [FromQuery] string accentColour)` → `PreviewBrandingQuery` (200 `BrandingPreviewDto`, 400).
- `[HttpPost("logos")] [Consumes("multipart/form-data")] [RequestSizeLimit(1_048_576)] [RequestFormLimits(MultipartBodyLengthLimit = 1_048_576)]`
  `UploadLogo(IFormFile file, CancellationToken ct)`: copy into a `MemoryStream` (`await file.CopyToAsync(stream, ct)`),
  send `UploadBrandLogoCommand(stream.ToArray())` (200 `BrandLogoDto`, 400, 422). Never read `file.ContentType` or `FileName` (S2).
- `[HttpPut]` `([FromBody] ChangeBrandingRequest request)` → `ChangeBrandingCommand` (200, 400, 404, 409, 422).
- `[HttpPost("revert")]` `([FromBody] RevertBrandingRequest request)` → `RevertBrandingCommand` (200, 409, 422).
- Request records at the bottom of the file: `ChangeBrandingRequest(string DisplayNameAr, string DisplayNameEn, string
  PrimaryColour, string AccentColour, string ArabicFont, Guid? LightLogoId, Guid? DarkLogoId, byte[] RowVersion)` and
  `RevertBrandingRequest(byte[] RowVersion)`.

Three classes named `BrandingController` in three namespaces is fine — Tickets already has `TicketsController` in
Agent and Portal (Story 47); routes are `api/{Area}/Branding`.

### B9 — Migration (R2)

`backend/scripts/ef.ps1 -Module Organisation migrations add Branding`. Expected: `AddColumn` `Branding` and
`PreviousBranding` (`nvarchar(4000)`, nullable) and `CanRevertBranding` (`bit`, not null, `defaultValue: false`) on
`organisation.Organisations`; `CreateTable` `organisation.BrandLogos` (`Id uniqueidentifier` PK, `TenantId`,
`ContentType nvarchar(10)`, `Content varbinary(max)`, audit columns as on the other tables, `RowVersion rowversion`,
soft-delete columns only if `ModuleDbContext` adds them to every entity) and index `IX_BrandLogos_TenantId`. No other
table touched. Review the file before running the API.

### B10 — Documentation

- `backend/src/BuildingBlocks/Crm.BuildingBlocks.Application/Authorization/RequestAuthorizationAttributes.cs` line 14 —
  extend the `AllowAnonymousRequestAttribute` summary: "…and Story 58's published branding (`GetPublicBrandingQuery`,
  `GetBrandLogoQuery`), which is public by design."
- `backend/src/BuildingBlocks/Crm.BuildingBlocks.Presentation/AREAS.md` — Agent: "Story 58: Organisation's
  `BrandingController` (current tenant's branding, any signed-in staff user)."; Admin: "Story 58: Organisation's
  `BrandingController` (`admin.branding`: settings, preview, logo upload, change, revert)."; Public: "Story 58:
  Organisation's `BrandingController` — public branding by organisation code and logos by id (anonymous, per-IP limit)."
- `CLAUDE.md` "Frontend" — add one bullet: "Branding (Story 58): `tokens.css` holds the defaults; `plugins/branding.ts`
  writes the organisation's `--crm-primary-*`/`--crm-accent-*` overrides and Arabic font into `<style id="crm-brand">`
  (SSR on portal pages). No literal colour in `app/components/` or `app/layouts/` (lint)."

### B11 — `backend/requests/branding.http` and sample files

**Create data files** (Write tool for text; PowerShell for binaries):
- `backend/requests/data/branding-logo-light.svg`, `branding-logo-dark.svg` — small valid SVGs.
- `backend/requests/data/branding-logo-evil.svg` — `<svg xmlns="http://www.w3.org/2000/svg" onload="alert(1)"><script>alert(2)</script><a href="javascript:alert(3)"><rect width="10" height="10"/></a><foreignObject/><rect width="20" height="20" style="fill:red" onclick="x()"/></svg>`.
- `backend/requests/data/branding-not-an-image.png` — the text `this is not an image`.
- `backend/requests/data/branding-logo.png` — a real 1×1 PNG:
  `[IO.File]::WriteAllBytes("D:\CRM\backend\requests\data\branding-logo.png", [Convert]::FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg=="))`.
- `backend/requests/data/branding-too-large.png` — PNG signature + 600 KiB of zeros:
  `$b = New-Object byte[] (600*1024); [byte[]](0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A) | % -Begin {$i=0} { $b[$i++] = $_ }; [IO.File]::WriteAllBytes("D:\CRM\backend\requests\data\branding-too-large.png", $b)`.

**Create file: `backend/requests/branding.http`** — header as `organisation.http` lines 1–8 plus `@org = DEMO`; logins for
admin and agent A (lines 17–40 pattern). Requests, each `###` titled with the expected status:
1. `GET {{host}}/api/Public/Branding?organisation={{org}}` — 200, seeded "Demo Support Centre", `tokens.light["--crm-primary-600"] == "#0E6655"`, `tokens.dark` present.
2. same with `Accept-Language: ar` — identical body (culture-neutral).
3. `?organisation=NOPE` — 404 `Organisation.OrganisationNotFound`; `?organisation=@@` — 400 validation (localised).
4. `GET /api/Public/Branding/logos/06000000-0000-0000-0005-000000000001` — 200 `image/svg+xml`, headers `Cache-Control`, `X-Content-Type-Options`, `Content-Security-Policy`; unknown id — 404.
5. `GET /api/Agent/Branding` as agent A — 200 (same body as 1); no token — 401.
6. `GET /api/Admin/Branding` as agent A — 403 naming `admin.branding`; as admin — 200, `isCustom: true`, `canRevert: true`, `previous: null`. Capture `rowVersion`.
7. `GET /api/Admin/Branding/preview?primaryColour=%23777777&accentColour=%230F766E` — 200, `PrimaryText` ratio **4.48**, `passes: false`; `%23767676` — **4.54**, `passes: true` (R8 reference values); `primaryColour=blue` — 400.
8. Upload (multipart, boundary `CrmBoundary`, part `name="file"`, body `< ./data/…`): `branding-logo.png` — 200 `image/png`; `branding-logo-light.svg` — 200; `branding-logo-dark.svg` — 200; `branding-logo-evil.svg` — 200, then GET its logo URL: no `script`, `onload`, `onclick`, `javascript:`, `foreignObject`, `style`; `branding-not-an-image.png` — 422 `Organisation.LogoTypeNotAllowed`; `branding-too-large.png` — 422 `Organisation.LogoTooLarge`. Capture the light/dark ids.
9. `PUT /api/Admin/Branding` primary `#FFD400` — 422 `Organisation.BrandingContrastTooLow`, detail in English; again with `Accept-Language: ar` — Arabic detail.
10. `PUT` accent `#F5F5F5` — 422 (AccentOnSurface).
11. `PUT` valid (primary `#1D4ED8`, accent `#0F766E`, font `NotoKufiArabic`, the new light/dark ids) — 200; then request 1 again — `--crm-primary-600` is `#1D4ED8` (cache invalidated).
12. `PUT` with `darkLogoId` only — 422 `Organisation.DarkLogoWithoutLight`; with a random `lightLogoId` — 404 `Organisation.BrandLogoNotFound`; with font `Arial` — 400.
13. `PUT` with the stale `rowVersion` from 6 — 409 `Persistence.ConcurrencyConflict`.
14. `POST /api/Admin/Branding/revert` (fresh rowVersion) — 200, current = the seeded DEMO branding again; revert again — 200, current = `#1D4ED8`; revert with a stale rowVersion — 409.
15. Restore the demo: revert once more so the seeded branding is current (leave the database as the demo expects).

---

## Frontend Tasks

Order: **F0 → F1 → F2 → F3 → F4 → F5 → F6 → F7 → F8 → F9 → F10**. Every file with Arabic: Write tool.

### F0 — Packages, config, API types

- **R1:** `corepack enable pnpm --install-directory "$APPDATA/npm"`; `pnpm --dir frontend add @fontsource/noto-kufi-arabic @fontsource/tajawal --save-exact`
  (exact versions in `package.json`, matching the existing pinning style). Fallback if either cannot be installed: drop
  that font from `ArabicFont` (backend enum + validator message + frontend map) and record it in the plan's risk notes.
- **File: `frontend/nuxt.config.ts`** lines 15–21 — add after line 19:
  `'@fontsource/noto-kufi-arabic/400.css', '@fontsource/noto-kufi-arabic/500.css', '@fontsource/noto-kufi-arabic/600.css', '@fontsource/noto-kufi-arabic/700.css', '@fontsource/tajawal/400.css', '@fontsource/tajawal/500.css', '@fontsource/tajawal/700.css'`
  (Tajawal has no 600 — the browser uses 700). `@font-face` files download only when the family is used. Line 26:
  `public: { apiBase: '', organisationCode: 'DEMO' }, // Story 58 D5: the organisation this host brands pre-login; NUXT_PUBLIC_ORGANISATION_CODE overrides`.
- **File: `frontend/scripts/gen-api.ts`** — line 74: `openapiTS(jsonPath, { transform })` with
  `import ts from 'typescript'` and
  `const BLOB = ts.factory.createTypeReferenceNode(ts.factory.createIdentifier('Blob'))`;
  `function transform(schema: { format?: string }): ts.TypeNode | undefined { return schema.format === 'binary' ? BLOB : undefined }`
  (openapi-typescript's documented file-upload recipe; erasable syntax only, Node type stripping). Then with the API
  running: `pnpm --dir frontend gen:api`. Expect `/api/Public/Branding`, `/api/Public/Branding/logos/{id}`,
  `/api/Agent/Branding`, and five `/api/Admin/Branding*` paths; the upload body typed `{ file?: Blob }` (R3). Commit both
  `openapi/*.json` and `app/api/generated/*.ts`.

### F1 — Tokens

- **File: `frontend/app/assets/css/tokens.css`** — after line 21 add a `/* Accent (Story 58, D8) */` block
  `--crm-accent-50` … `--crm-accent-950` with the **light** values the preview endpoint returns for accent `#0F766E`
  (`branding.http` 7 with `accentColour=%230F766E`, copy `tokens.light`); inside `[data-theme='dark']` (after line 82)
  add the same eleven names with the **dark** values. Update the comment at line 82: "Primary stays put; the accent
  scale and the semantic roles move." Update lines 2–3: "Story 58 overrides the primary/accent scales and the Arabic
  font at runtime (`plugins/branding.ts`); these are the defaults."
- **File: `frontend/app/assets/css/main.css`** — after line 32 add `--color-accent-50: var(--crm-accent-50);` … `950`.

### F2 — `frontend/app/utils/branding.ts`

```ts
import type { components } from '~/api/generated/public'
import type { Theme } from '~/composables/useTheme'

export type PublicBrandingDto = components['schemas']['PublicBrandingDto']
export type BrandTokens = components['schemas']['BrandTokensDto']

/** Mirrors backend ArabicFont (D9). The CSS family is built from this map only, never from a response (S3). */
export const arabicFontFamilies = {
  IbmPlexSansArabic: "'IBM Plex Sans Arabic'",
  NotoKufiArabic: "'Noto Kufi Arabic'",
  Tajawal: "'Tajawal'",
} as const
export type ArabicFontKey = keyof typeof arabicFontFamilies
export const arabicFontKeys = Object.keys(arabicFontFamilies) as ArabicFontKey[]
const fallbackStack = "ui-sans-serif, system-ui, -apple-system, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif"

const tokenName = /^--crm-(primary|accent)-(50|[1-9]00|950)$/
const hexValue = /^#[0-9a-fA-F]{6}$/

export function brandLogoUrl(id: string | null | undefined): string | null { return id ? `/api/Public/Branding/logos/${id}` : null }

export function isArabicFontKey(value: string): value is ArabicFontKey { return value in arabicFontFamilies }

/** Only well-formed token entries survive (S3). */
export function safeTokens(map: Record<string, string> | undefined): Record<string, string> { … filter Object.entries by both regexes … }

function declarations(map: Record<string, string>): string { return Object.entries(safeTokens(map)).map(([k, v]) => `${k}:${v}`).join(';') }

/** The <style id="crm-brand"> text. Doubled selectors outrank tokens.css's :root / [data-theme] (R4). */
export function buildBrandCss(branding: PublicBrandingDto | null): string {
  if (!branding) return ''
  const parts: string[] = []
  if (branding.tokens) {
    parts.push(`:root:root,[data-theme='light'][data-theme='light']{${declarations(branding.tokens.light)}}`)
    parts.push(`[data-theme='dark'][data-theme='dark']{${declarations(branding.tokens.dark)}}`)
  }
  const font = branding.arabicFont
  if (isArabicFontKey(font) && font !== 'IbmPlexSansArabic')
    parts.push(`html[lang='ar'][lang='ar']{--crm-font-sans:${arabicFontFamilies[font]}, ${fallbackStack}}`)
  return parts.join('')
}

/** Inline style for a preview frame: token overrides (+ the Arabic font) on one subtree. */
export function previewStyle(tokens: BrandTokens | null, theme: Theme, font: string): Record<string, string> { … safeTokens(theme === 'dark' ? tokens?.dark : tokens?.light) plus '--crm-font-sans' when isArabicFontKey(font) … }
```
(Adjust the generated property names — `tokens.light`/`dark`, `arabicFont`, `lightLogoId` — to what `gen:api` emits.)

### F3 — Store, server cache, plugin

- **Create file: `frontend/app/utils/brandingServerCache.ts`** — module-level `Map<string, { expires: number; value:
  PublicBrandingDto | null }>`, TTL `15_000` ms (D13); `export async function cachedServerBranding(code: string, load:
  () => Promise<PublicBrandingDto | null>): Promise<PublicBrandingDto | null>`. Comment: "Server only. Public data, no
  user state — safe to share across requests; without it every SSR render spends the Public Area's per-IP budget (R6)."
- **Create file: `frontend/app/stores/branding.ts`** — `useBrandingStore` (setup store, pattern of `stores/auth.ts`
  lines 13–47): `branding = ref<PublicBrandingDto | null>(null)`; `css = computed(() => buildBrandCss(branding.value))`;
  `loadPublic()`: `code = useRuntimeConfig().public.organisationCode`; loader = `call(clients.public.GET('/api/Public/Branding',
  { params: { query: { organisation: code } } }), { toastOnError: false })` → `ok ? data : null`; on the server wrap
  in `cachedServerBranding(code, loader)`; never throws (a failure leaves `null` = defaults). `loadForTenant()`:
  `GET /api/Agent/Branding` (`clients.agent`, `toastOnError: false`), on success replace `branding` (the agent and
  public schemas are structurally identical). `displayName(locale)` → `localizedName(branding.displayName, locale)` or
  `null`; `logoUrl(theme)` → `brandLogoUrl(theme === 'dark' ? (darkLogoId ?? lightLogoId) : lightLogoId)`.
- **Create file: `frontend/app/plugins/branding.ts`** (universal; runs after `api.ts` — alphabetical):

```ts
// Story 58: the organisation's --crm-* overrides and Arabic font, in one <style> in the head. SSR routes (/, /login,
// /portal/**) get it server-rendered; SPA routes (/agent/**, /admin/**) load it before mount. Never throws: a failed
// call keeps tokens.css (D10).
export default defineNuxtPlugin(async (nuxtApp) => {
  const branding = useBrandingStore()
  const i18n = nuxtApp.$i18n
  // After an SSR render Pinia already holds the server's answer. If the server had none, keep it that way during
  // hydration (the HTML was rendered with the defaults - loading now would mismatch).
  if (branding.branding === null && !(import.meta.client && nuxtApp.payload.serverRendered)) await branding.loadPublic()
  useHead({
    style: [{ key: 'crm-brand', id: 'crm-brand', textContent: () => branding.css }],
    title: () => branding.displayName(i18n.locale.value) ?? i18n.t('shell.appName'),
  })
})
```

### F4 — Shell

- **Create file: `frontend/app/components/shell/BrandMark.vue`** — `<img v-if="logoUrl" :src="logoUrl" :alt="name"
  class="h-8 w-auto max-w-40 object-contain" />` else `<span class="truncate text-lg font-semibold">{{ name }}</span>`;
  `name = branding.displayName(locale) ?? t('shell.appName')`, `logoUrl = branding.logoUrl(theme)` (`useTheme`).
- **File: `frontend/app/layouts/agent.vue`** — line 35 → `<ShellBrandMark />` (keep the wrapper's `min-w-0`); lines
  12–19 and 70–78: add `border-s-4 border-transparent` to the link classes and
  `:active-class="item.to === '/agent' ? '' : 'border-accent-600 bg-surface-raised'"`
  `exact-active-class="border-accent-600 bg-surface-raised"` (D8; `/agent` is a prefix of every route, so it is
  exact-only). In `<script>` after line 92: `void useBrandingStore().loadForTenant()` (D5; the layout mounts once per
  signed-in SPA session). Add the nav item `...(can(Permissions.AdminBranding) ? [{ to: '/admin/branding', label: t('branding.nav.branding') }] : [])`
  after line 111. Keep `overflow-x-clip` on `<main>` (line 47).
- **File: `frontend/app/layouts/portal.vue`** — line 9: `class="border-b border-t-4 border-border border-t-accent-600"`;
  lines 13–16: the `NuxtLink`/`span` contain `<ShellBrandMark />` instead of `{{ t('shell.appName') }}` (the link keeps
  its `aria-label`-free text via the image `alt`); line 71: `{{ brandName }}` with
  `const branding = useBrandingStore()` (top of `<script setup>`, line 77 area; also take `locale` from `useI18n()`) and
  `const brandName = computed(() => branding.displayName(locale.value) ?? t('shell.appName'))`.
- **File: `frontend/app/app.vue`** line 2 — unchanged (already `var(--crm-primary-500)`; it re-resolves).

### F5 — UI kit (each gets a `/_dev/ui` section — CLAUDE.md)

- **Create file: `frontend/app/components/ui/ColorField.vue`** — props `label`, `hint?`, `error?`, `disabled?`,
  `required?`; `defineModel<string>()` holding `#RRGGBB`. Layout: label (like `Input.vue` lines 3–6), then a row: native
  `<input type="color">` (`class="h-10 w-12 shrink-0 cursor-pointer rounded-md border border-border bg-surface ui-focus:focus-ring"`,
  `:aria-label="label"`, lower-case value in, upper-case out) and a text `<input dir="ltr" maxlength="7"
  class="…same as Input.vue line 17… font-mono">` with `:aria-invalid`/`aria-describedby`; hint/error like `Input.vue`
  lines 19–20. The text input only emits when it matches `^#[0-9a-fA-F]{6}$` (partial typing stays local).
- **Create file: `frontend/app/components/ui/FileField.vue`** — props `label`, `accept`, `hint?`, `error?`, `busy?`,
  `buttonLabel`; hidden `<input type="file" :accept>` + a `UiButton variant="secondary"` that opens it (`inputRef.click()`);
  emits `select(file: File)`; resets the input value after each pick so the same file can be picked twice; hint/error
  as `Input.vue`.
- **File: `frontend/app/pages/_dev/ui.vue`** — two sections (`color-field`, `file-field`) after "Select" (line 143)
  with default / error / disabled / long-label / Arabic-label states in both theme columns, and entries in `sections`
  (lines 569–590). Add an "Accent" row to the Badge section only if a swatch list exists there (none today — skip).

### F6 — Admin composables (`frontend/app/composables/branding/`)

- `types.ts` — `BrandingSettingsDto`, `BrandingPreviewDto`, `ContrastCheckDto`, `BrandLogoDto`, `ChangeBrandingRequest`
  from `~/api/generated/admin` `components['schemas']`.
- `useBrandingSettings.ts` — `useAsyncData('admin-branding', …, { server: false })` over `GET /api/Admin/Branding`,
  throwing the problem on failure (copy `useBranches.ts` lines 8–23).
- `useBrandingPreview.ts` — `(primary: Ref<string>, accent: Ref<string>)` → `preview = ref<BrandingPreviewDto | null>`,
  `status`; `watch([primary, accent], …, { immediate: true })` → `debounce(load, 250)` (`utils/debounce.ts`, cancel on
  `onScopeDispose`); skips the call unless both match the hex pattern; `GET /api/Admin/Branding/preview` with
  `toastOnError: false`; keeps the last good preview on failure.
- `useBrandingCommands.ts` — `uploadLogo(file: File)` → `call(clients.admin.POST('/api/Admin/Branding/logos', { body: { file },
  bodySerializer: (body) => { const form = new FormData(); if (body.file) form.append('file', body.file); return form } }), { toastOnError: false })`
  (R3); `save(body)` → `PUT /api/Admin/Branding`; `revert(rowVersion)` → `POST /api/Admin/Branding/revert`; all
  `toastOnError: false`.

### F7 — Preview components (`frontend/app/components/branding/`) — tokens only, no literal colour

Props for both: `displayName: string`, `logoUrl: string | null`, `theme: Theme`, `styleVars: Record<string, string>`.
Root: `<div :data-theme="theme" :style="styleVars" class="overflow-hidden rounded-lg border border-border bg-surface text-fg">`
(the nested `data-theme` re-resolves the semantic roles; the inline style overrides primary/accent — main.css "Probe 5").
- `BrandingPreviewAgent.vue` — a header row (logo `<img>` or name), a narrow nav (`branding.preview.navTickets` active
  with `border-s-4 border-accent-600 bg-surface-raised`, `navCustomers` plain), content: heading
  `branding.preview.heading`, text, `UiBadge tone="primary"`, a `text-primary-600 underline` link, `UiButton` primary
  `branding.preview.action`. Stacks to one column under `sm`.
- `BrandingPreviewPortal.vue` — header with `border-t-4 border-t-accent-600`, logo/name; a card with the sign-in title,
  two `UiInput disabled` (email/password, `dir="ltr"`), a primary `UiButton`, the register link.
- `BrandingContrastList.vue` — props `checks: ContrastCheckDto[]`; `<ul>`: label via a fixed map
  `{ PrimaryText: 'branding.contrast.primaryText', AccentOnSurface: 'branding.contrast.accentOnSurface' }`, ratio text
  `t('branding.contrast.ratio', { ratio, required })` with numbers from `useFormat().number(x, { minimumFractionDigits: 2, maximumFractionDigits: 2 })`
  inside `<bdi dir="ltr">`, and `UiBadge` `success`/`danger` with `branding.contrast.pass`/`fail`.

### F8 — Admin page `frontend/app/pages/admin/branding.vue`

`definePageMeta({ layout: 'agent' })`. States (copy `pages/admin/branches.vue` lines 14–26): 403 → `UiErrorState`
forbidden; load error → `UiErrorState` + retry; loading → `UiSkeleton` blocks. Empty state = not applicable (there is
always an effective branding); when `!settings.isCustom` show `branding.defaultNote`.

Layout: `<h1>` + intro; `grid gap-6 lg:grid-cols-2` — **form** column, **preview** column (stacked on mobile; check 375
and 1280).
- Form (`<form @submit.prevent="save">`): `UiInput` display name en (`dir="ltr"`) and ar (`dir="rtl" lang="ar"`);
  `UiColorField` primary and accent; `UiSelect` Arabic font (options from `arabicFontKeys`, labels
  `branding.fonts.<key>` through a fixed map); light and dark logo: thumbnail `<img>` (`brandLogoUrl(id)`, `h-10
  object-contain`) + `UiFileField` (`accept="image/png,image/svg+xml"`, hint `branding.fields.logoHint`) + "Remove"
  `UiButton variant="ghost"`; a client pre-check `file.size > 512 * 1024` shows `errors.code.Organisation.LogoTooLarge`
  on the field without uploading; upload 422 → the problem's localised message on that field (`problemMessage`);
  `UiButton` save; `UiButton variant="secondary"` revert (`:disabled="!settings.canRevert"`) → confirm `UiModal`
  (`branding.actions.revertConfirm`).
- Preview column: `UiTabs` agent/portal, a light/dark `role="group"` toggle (two `UiButton size="sm"`,
  `aria-pressed`), the chosen preview component with `previewStyle(preview.tokens, previewTheme, form.arabicFont)` and
  the form's display name for the current locale and logo, then `BrandingContrastList`.
- Save: 400 → `useFormErrors()` on the fields (`displayNameEn/Ar`, `primaryColour`, `accentColour`, `arabicFont`);
  422 → inline `role="alert"` with `problemMessage(problem, …).description` (server-worded for
  `Organisation.BrandingContrastTooLow`, F9); 409 → inline `branding.conflict` + `refresh()`; 404 (logo) → inline
  message. Success → toast `branding.actions.saved`, `refresh()`, `await useBrandingStore().loadForTenant()` (the admin's
  own shell re-themes at once). Revert success → toast `branding.actions.reverted`, same refresh.

### F9 — Copy, error codes, lint

- **Create files: `frontend/i18n/locales/en/branding.json` and `ar/branding.json`** (top-level key `branding`, Write
  tool). Keys (en → ar): `nav.branding` "Branding" / "الهوية البصرية"; `title` "Branding" / "الهوية البصرية"; `intro`
  "Your organisation's name, logo, colours and Arabic font on the agent workspace and the customer portal." / "اسم منشأتك
  وشعارها وألوانها والخط العربي في مساحة عمل الموظفين وبوابة العملاء."; `defaultNote` "The product's default branding is
  in use." / "تُستخدم الهوية الافتراضية للمنتج."; `fields.displayNameEn` "Display name (English)" / "الاسم المعروض
  (بالإنجليزية)"; `fields.displayNameAr` "Display name (Arabic)" / "الاسم المعروض (بالعربية)"; `fields.primary` "Primary
  colour" / "اللون الأساسي"; `fields.accent` "Accent colour" / "لون التمييز"; `fields.arabicFont` "Arabic font" / "الخط
  العربي"; `fields.lightLogo` "Logo (light theme)" / "الشعار (المظهر الفاتح)"; `fields.darkLogo` "Logo (dark theme)" /
  "الشعار (المظهر الداكن)"; `fields.logoHint` "PNG or SVG, up to 512 KB." / "PNG أو SVG، بحد أقصى 512 كيلوبايت.";
  `fields.chooseFile` "Choose file" / "اختر ملفاً"; `fields.removeLogo` "Remove" / "إزالة"; `fields.uploading`
  "Uploading…" / "جارٍ الرفع…"; `fonts.IbmPlexSansArabic` "IBM Plex Sans Arabic" (both); `fonts.NotoKufiArabic` "Noto Kufi
  Arabic" (both); `fonts.Tajawal` "Tajawal" / "تجوال"; `preview.title` "Preview" / "معاينة"; `preview.agent` "Agent
  workspace" / "مساحة عمل الموظف"; `preview.portal` "Customer portal" / "بوابة العملاء"; `preview.light` "Light" /
  "فاتح"; `preview.dark` "Dark" / "داكن"; `preview.themeLabel` "Preview theme" / "مظهر المعاينة"; `preview.navTickets`
  "Tickets" / "التذاكر"; `preview.navCustomers` "Customers" / "العملاء"; `preview.heading` "Today's queue" / "قائمة
  اليوم"; `preview.body` "Three tickets are waiting for a reply." / "ثلاث تذاكر بانتظار الرد."; `preview.badge` "New" /
  "جديدة"; `preview.link` "View all tickets" / "عرض كل التذاكر"; `preview.action` "Reply" / "رد"; `preview.signInTitle`
  "Sign in to your support portal" / "تسجيل الدخول إلى بوابة الدعم"; `preview.email` "Email" / "البريد الإلكتروني";
  `preview.password` "Password" / "كلمة المرور"; `preview.signIn` "Sign in" / "تسجيل الدخول"; `preview.register` "New
  here? Create your account" / "جديد هنا؟ أنشئ حسابك"; `contrast.title` "Contrast (WCAG AA)" / "التباين (WCAG AA)";
  `contrast.primaryText` "Text on the primary colour" / "النص على اللون الأساسي"; `contrast.accentOnSurface` "Accent
  colour on the page background" / "لون التمييز على خلفية الصفحة"; `contrast.ratio` "{ratio}:1 (needs {required}:1)" /
  "{ratio}:1 (المطلوب {required}:1)"; `contrast.pass` "Passes" / "مقبول"; `contrast.fail` "Too low" / "منخفض جداً";
  `actions.save` "Save branding" / "حفظ الهوية"; `actions.saved` "Branding saved. Other open tabs show it after a
  refresh." / "تم حفظ الهوية. تظهر في الصفحات المفتوحة الأخرى بعد تحديثها."; `actions.revert` "Revert to previous
  version" / "الرجوع إلى النسخة السابقة"; `actions.revertConfirm` "Restore the previous branding? The current one becomes
  the previous version." / "هل تريد استعادة الهوية السابقة؟ ستصبح الهوية الحالية هي النسخة السابقة."; `actions.reverted`
  "Previous branding restored." / "تمت استعادة الهوية السابقة."; `actions.cancel` "Cancel" / "إلغاء"; `actions.close`
  "Close" / "إغلاق"; `load.error` "The branding settings could not be loaded." / "تعذر تحميل إعدادات الهوية.";
  `load.retry` "Try again" / "حاول مجدداً"; `conflict` "Someone else changed the branding. Reload and try again." / "غيّر
  شخص آخر الهوية. أعد التحميل وحاول مجدداً."
- **`i18n/locales/{en,ar}/errors.json`** → under `errors.code.Organisation` (lines 19–25): `BrandingContrastTooLow` "The
  colours do not meet WCAG AA contrast." / "الألوان لا تحقق تباين WCAG AA."; `NoPreviousBranding` "There is no previous
  branding to revert to." / "لا توجد هوية سابقة للرجوع إليها."; `BrandLogoNotFound` "The logo was not found. Upload it
  again." / "لم يتم العثور على الشعار. ارفعه مرة أخرى."; `LogoEmpty` "The logo file is empty." / "ملف الشعار فارغ.";
  `LogoTooLarge` "The logo is larger than 512 KB." / "حجم الشعار أكبر من 512 كيلوبايت."; `LogoTypeNotAllowed` "The logo
  must be a PNG or SVG image." / "يجب أن يكون الشعار صورة PNG أو SVG."; `DarkLogoWithoutLight` "Add a light-theme logo
  before a dark-theme one." / "أضف شعار المظهر الفاتح قبل شعار المظهر الداكن."; `ArabicFontUnsupported` "Choose one of the
  listed Arabic fonts." / "اختر أحد الخطوط العربية المدرجة."; `BrandColourInvalid` "Enter a colour like #1A2B3C." /
  "أدخل لوناً مثل #1A2B3C."; `OrganisationNotFound` "The organisation was not found." / "لم يتم العثور على المنشأة.".
- **File: `frontend/app/api/problem-message.ts`** line 9 — add `'Organisation.BrandingContrastTooLow'` to
  `serverWordedCodes` (its detail names the check and ratio in the request language, B4).
- **File: `frontend/eslint.config.mjs`** — after line 9 add
  ```js
  // Story 58: no literal colour in a class - components and layouts use the --crm-* token utilities only.
  const literalColour = String.raw`(^|:)(bg|text|border(-[setbxy])?|ring(-offset)?|outline|fill|stroke|from|via|to|shadow|decoration|divide|placeholder|caret|accent)-(white|black|slate|gray|zinc|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose)(-|\/|$)`
  const arbitraryColour = String.raw`\[(#|rgba?\(|hsla?\(|oklch\(|oklab\(|color\()`
  ```
  and append `` `/${literalColour}/`, `/${arbitraryColour}/` `` to **both** `vue/no-restricted-class` lists (line 21
  and line 33 — the ui-kit block replaces the base list, so it must repeat them). `neutral` is **not** in the list: it
  is a token scale here (main.css lines 34–44).

### F10 — `pnpm lint` and the colour grep

Run the Verification grep (step 6) — it must print nothing — and `pnpm --dir frontend lint`.

---

## Edge Cases & Failure Modes

- **Palette fails WCAG AA** (`#FFD400` primary, `#F5F5F5` accent) → 422 `Organisation.BrandingContrastTooLow`, detail
  in the request language naming the check, ratio and requirement (`BrandingRefusals.ContrastTooLow`, B4); the domain
  throws the same code in English if a handler skips the pre-check (`BrandPalette.Create`, B1). The admin page shows it
  inline (F8) and the live contrast list already shows "Too low" before saving.
- **Borderline ratios** — `#777777` → 4.48 (fail), `#767676` → 4.54 (pass): the reference pair proving the luminance
  formula (R8). The comparison is `>=` on the unrounded ratio; the DTO rounds to 2 decimals for display only.
- **Malformed colour** (`blue`, `#12345`, `#GGGGGG`) → 400 validation on the field (`OrganisationRules.HexColour`); if
  it ever reached the domain: 422 `Organisation.BrandColourInvalid`.
- **Very dark or very light brand colours in dark theme** → the dark anchor is moved into the window (D7,
  `BrandTokenGenerator.ForDark`); dark never rejects. Near-black primary (`#000000`) in light passes (21:1) and its dark
  anchor becomes a mid-grey — acceptable.
- **Out-of-gamut scale steps** → chroma bisected down (`Oklch.ClampChroma`); `BrandColour.FromLinearRgb` clamps anyway.
- **Logo 513 KB – 1 MiB** → 422 `Organisation.LogoTooLarge` (domain). **> 1 MiB** → rejected by `RequestSizeLimit`
  before the handler (400/413 problem from the global handler — the page pre-checks size so a user never sees it).
- **Wrong type by content** (text renamed `.png`, a JPEG, a GIF, an SVG that does not parse, DTD/XXE payload, root not
  `svg`) → 422 `Organisation.LogoTypeNotAllowed` (`BrandLogo.Sniff`, `SvgSanitizer` returns null on `XmlException`
  — `DtdProcessing.Prohibit` throws on any DTD). An SVG renamed `.png` is accepted **as SVG** (content wins).
- **Malicious SVG** (`onload`, `<script>`, `javascript:` href, `foreignObject`, `style`) → accepted but stored
  sanitised (S2); `branding.http` 8 proves it. Served with `nosniff` + CSP sandbox.
- **Empty upload / missing `file` part** → 400 (model binding) or 422 `Organisation.LogoEmpty`.
- **Logo id of another tenant or unknown** on save → 404 `Organisation.BrandLogoNotFound` (D4, B5.7).
- **Dark logo without light** → 422 `Organisation.DarkLogoWithoutLight` (`Branding.Create`).
- **Unknown Arabic font** (`Arial`, `1`) → 400 (`SupportedArabicFont` compares enum **names**, so `"1"` is rejected).
- **Concurrent admins** → stale `rowVersion` → 409 `Persistence.ConcurrencyConflict` (`TransactionBehavior`); the page
  shows `branding.conflict` and reloads.
- **Revert with no history** (never branded, not seeded — e.g. a non-Development tenant) → 422
  `Organisation.NoPreviousBranding` (pre-check B5.8, backstop `Organisation.RevertBranding`). The button is disabled when
  `canRevert` is false.
- **Saving the branding already in effect** → no change, previous version kept (`ChangeBranding` equality guard, D2).
- **Public branding for an unknown or inactive organisation code** → 404; SSR keeps the defaults and the app name; not
  cached (failures never are). A malformed `NUXT_PUBLIC_ORGANISATION_CODE` → 400 → same defaults.
- **API down during SSR** → `loadPublic` returns null, the portal renders with tokens.css and "Customer Support CRM";
  the client does not retry during hydration (F3 guard) so the HTML matches.
- **Redis down** → `CachingBehavior` degrades to uncached reads (Story 03); branding still correct.
- **Stale branding after save** — Redis: invalidated after commit (D12). Nuxt server cache: ≤ 15 s (D13). Browser tabs:
  after a refresh (the AC); the admin's own tab re-themes immediately (F8).
- **Tampered stored JSON** (manual DB edit making the palette fail) → `BrandingConverter.Deserialize` throws a
  `DomainException` on load → 422/500 on every Organisation read of that row. Only a manual DB edit can cause it; noted.
- **Upload request logged at Debug** → `LoggingBehavior` destructures `UploadBrandLogoCommand` including the bytes
  (Serilog truncates large byte arrays); Debug is off in every environment (`appsettings*.json` `Default:
  Information`). Do not add a Debug override for Organisation.
- **Token injection** — a response token with a bad name/value is dropped client-side (S3); the font rule comes only
  from the fixed map.
- **Specificity** — `:root:root` / doubled attribute selectors (0,2,0) beat tokens.css (0,1,0) regardless of `<style>`
  order; the dark block comes after the light block so `html[data-theme=dark]` resolves to dark (R4).
- **Preview frame nested `data-theme`** inside a light page → semantic roles from tokens.css's `[data-theme='dark']`,
  brand scale from the inline style (inline beats every selector).
- **RTL** — BrandMark `max-w-40` + `object-contain` keeps wide logos inside the header at 375 px; the nav active bar
  uses `border-s-4` (logical); identifiers/ratios/hex values in `<bdi dir="ltr">`.
- **Arabic font only in `ar`** — `html[lang='ar']`; switching locale re-evaluates without reload (`locale-head` updates
  `lang`).

---

## Test Plan

This project ships **without automated tests** (CLAUDE.md, "Testing policy"). Do not add a test project, a `*.spec.ts`,
or a test framework. In their place:

1. **Compiler and build:** `dotnet build backend/Crm.sln` — zero warnings, no `CRM0001` (no new project reference).
   `pnpm --dir frontend typecheck` / `lint` / `build` clean.
2. **Startup guard:** the API starts — the eight new requests each declare exactly one authorisation attribute
   (`RequestAuthorizationGuard`).
3. **Request samples (smoke, manual):** `backend/requests/branding.http` 1–15 top to bottom, each response matching its
   `###` title, in `en` and (requests 3, 9, 12) `ar`. Then re-run `organisation.http` and `portal.http` 1–6 —
   unchanged (regression for the Organisation mapping change and the portal layout).
4. **Colour math (R8):** preview `#777777` → 4.48 fail, `#767676` → 4.54 pass; the generated light `--crm-primary-600`
   equals the input; the dark `--crm-primary-600` for `#0B1F4B` has luminance in `[0.115, 0.180]` (check its contrast
   against `#FFFFFF` ≥ 4.5 in the browser devtools colour picker).
5. **Browser walkthrough (manual; en + ar; light + dark; 375 and 1280 px):** `/portal/login` view-source shows
   `<style id="crm-brand">` with the DEMO tokens and the logo `<img>` (server-rendered); `/login`, `/portal`; sign in
   as admin → `/admin/branding`: change colours (preview + contrast list update), try `#FFD400` (inline Arabic/English
   refusal), upload PNG/SVG logos, a too-large and a wrong file (field errors), save → the agent shell re-themes; refresh
   the portal tab → new colours and logo; revert → previous branding; Arabic font visible only in `ar`. Agent A (no
   `admin.branding`) sees no Branding nav item and gets the forbidden state on `/admin/branding`. `/_dev/ui` shows the
   ColorField and FileField sections in both themes.
6. **Literal-colour check:** the grep in Verification step 6 prints nothing.

---

## Migration / Rollback

- **One migration**, `Organisation` `Branding`: three columns on `organisation.Organisations`, one new table
  `organisation.BrandLogos`. Development applies it on startup (`MigrateAndSeedModulesAsync`); elsewhere
  `backend/scripts/ef.ps1 -Module Organisation database update`.
- **Rollback:** `ef.ps1 -Module Organisation database update Initial` (the previous migration,
  `20260929020009_Initial`) drops the columns and the table (logos and branding are lost — they exist nowhere else);
  revert the commit. Frontend rollback alone is safe: without `plugins/branding.ts` the app renders tokens.css.
- **Half-applied state:** code without the migration → every Organisation query fails (missing columns) — sign-in's
  membership lookup reads `Memberships`, not `Organisations`, but `GetMyScope` and the admin pages would 500. Apply the
  migration before starting the new code. Migration without code → the extra nullable columns and table are inert.
- **Config:** optional `NUXT_PUBLIC_ORGANISATION_CODE` (default `DEMO`). No backend configuration.

---

## Named risks (the implementation session proves each before relying on it)

- **R1 — pnpm and the two font packages.** pnpm is not on PATH (memory note); enable it with corepack as in CLAUDE.md,
  then `pnpm add … --save-exact`. Versions are unknown at planning time — pin what installs and record them here.
  Fallback: ship only the fonts that installed (F0).
- **R2 — EF mapping of a nullable value object through a `ValueConverter`.** Prove first: the generated migration shape
  (B9), a save + reload round-trip of `Branding` and `PreviousBranding` (seed, then `GET /api/Admin/Branding`), that
  `null` stays `NULL`, and that a change is detected (EF compares converted reference types with `Equals` →
  `ValueObject` equality). If change detection misses a swap, add `.Metadata.SetValueComparer(new ValueComparer<Branding?>((a, b) => Equals(a, b), v => v == null ? 0 : v.GetHashCode(), v => v))`.
- **R3 — First multipart endpoint.** Prove: Swashbuckle emits `multipart/form-data` with `file` `format: binary` for
  `IFormFile`; `gen:api` with the `transform` types it as `Blob`; the browser request carries
  `multipart/form-data; boundary=…` through `crmFetch` (it rebuilds the `Request` with `new Request(request, { headers })`
  — check the boundary survives) and no `application/json`; no antiforgery 400 (MVC does not validate antiforgery unless
  asked). Fallback for the type: `transform` returning `File`. Fallback for a lost boundary: in `plugins/api.ts`
  `baseFetch`, leave the request's own `Content-Type` untouched (it already does — it only sets `Accept-Language` and
  `X-Crm-Scope`); if a JSON content type still appears, find where it is added rather than casting a type away.
- **R4 — Unhead `<style>` with a reactive `textContent`.** Prove: SSR HTML contains `<style id="crm-brand">` with the
  tokens (view-source `/portal/login`); after admin save the client tag updates without reload (devtools). If the
  getter is not reactive, use `computed` + `useHead(computed(() => ({ style: [...] })))`; if `textContent` is not
  emitted, use `innerHTML` (the content is pre-validated, S3).
- **R5 — Plugin ordering and hydration.** Prove: `plugins/branding.ts` runs after `api.ts` (`useApi()` available) and
  after Pinia; Pinia hydrates the server's `branding` so the client makes **no** branding call on an SSR page (network
  tab); no hydration warning on `/portal/login` in dev. On `/agent/**` the public call then `/api/Agent/Branding` run
  once.
- **R6 — Rate limit and SSR.** SSR calls come from the Nuxt server's IP; the 15 s server cache (D13) keeps them at ≤ 4/min
  per code. In dev the browser's `/api` calls also arrive through the dev proxy from `::1`, sharing the 60/min Public
  budget — if the walkthrough hits 429, raise `RateLimiting:Public:PermitLimit` in `appsettings.Development.json` only
  and note it.
- **R7 — SVG sanitiser.** Prove with `branding-logo-evil.svg` (B11) that every listed construct is gone and a normal
  SVG (the dev-seed logos, a designer's export with `defs`/gradients) still renders. `XDocument.Save` must not re-add a
  DTD. Check the response headers of the logo endpoint.
- **R8 — Colour math.** Prove the luminance/contrast reference pair (`#777777` 4.48, `#767676` 4.54), that light step
  600 equals the input, that `oklch(0.5 0.17 258)` converts to the `BrandDefaults.Primary` constant (fix the constant if
  not, D10), and the seeded palette passes (≈ 6.9:1 and ≈ 3.6:1). Then copy the default accent scale into tokens.css (F1).
- **R9 — Three `BrandingController` classes.** Prove Swagger generates all three Area documents without an operation-id
  or schema-id clash (the Tickets precedent says yes).
- **R10 — Font CSS paths.** Prove `@fontsource/noto-kufi-arabic/{400,500,600,700}.css` and
  `@fontsource/tajawal/{400,500,700}.css` exist in the installed packages before adding them to `nuxt.config.ts`
  (a missing path fails `pnpm build`).

---

## Verification Steps

1. **Backend builds:** `dotnet build backend/Crm.sln` — clean, zero warnings, no `CRM0001`.
2. **Migration:** `backend/scripts/ef.ps1 -Module Organisation migrations add Branding`; review (B9, R2).
3. **Backend runs:** `docker compose up -d`, then `dotnet run --project backend/src/Crm.Api --launch-profile http`;
   Swagger shows `Branding` in the Agent, Admin and Public documents (R9). Prove R2, R7, R8, then run
   `backend/requests/branding.http` 1–15 and the regression files (Test Plan 3).
4. **Frontend:** `corepack enable pnpm --install-directory "$APPDATA/npm"` (R1); `pnpm --dir frontend install
   --frozen-lockfile` (after F0's `add`, the lockfile is updated — commit it); `pnpm --dir frontend gen:api` (R3);
   `pnpm --dir frontend typecheck`; `pnpm --dir frontend lint`; `pnpm --dir frontend build`.
5. **Frontend runs:** `pnpm --dir frontend dev`; prove R4, R5, R6; walk Test Plan 5 in `en` and `ar`, light and dark,
   at 375 and 1280 px (and 768 for the admin grid).
6. **Literal colours (AC):** from the repo root in Git Bash:
   `grep -rnE "#[0-9a-fA-F]{3,8}\b|rgba?\(|hsla?\(|oklch\(|oklab\(|\b(bg|text|border|ring|fill|stroke|from|to|via|outline|shadow|accent|caret|decoration|divide|placeholder)-(white|black|slate|gray|zinc|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose)\b" frontend/app/components frontend/app/layouts`
   — no output (it prints nothing today; the new `components/branding/*`, `components/ui/ColorField.vue`,
   `FileField.vue`, `components/shell/BrandMark.vue` must keep it that way).
7. **Regression:** staff sign-in, inbox, ticket detail, customer page, the other admin pages, portal sign-in and
   tickets still work; the agent `<main>` still clips horizontal overflow at 375 px.

---

## Done Criteria

- [ ] Changing the primary colour and logo in `/admin/branding` re-themes the agent workspace and the portal after a
      refresh, with no code change (Test Plan 5; `branding.http` 11).
- [ ] The Verification step 6 grep finds no literal colour in `app/components/` or `app/layouts/`; the ESLint guard is in
      place (F9).
- [ ] A palette failing WCAG AA is rejected with a localised explanation — 422 `Organisation.BrandingContrastTooLow`
      in `en` and `ar` (`branding.http` 9–10, the admin page inline message).
- [ ] `/portal/login` shows the organisation's logo and colours before login, **server-rendered** (view-source shows
      `<style id="crm-brand">` and the logo `<img>`).
- [ ] Logo uploads over 512 KB, or not PNG/SVG by content sniffing, are rejected (`branding.http` 8); an SVG with
      scripts/event handlers is stored sanitised and served with `nosniff` + CSP.
- [ ] Revert restores the previous branding; reverting again re-applies (`branding.http` 14).
- [ ] Branding walked in `en` and `ar`, light and dark, at 375 px and 1280 px (admin page, agent shell, portal).
- [ ] Every endpoint exercised from `backend/requests/branding.http`.
- [ ] The public branding is cached (`ICacheableQuery`) and invalidated on save and revert (`branding.http` 11).
- [ ] `dotnet build backend/Crm.sln` clean (zero warnings, no `CRM0001`); the `Organisation` `Branding` migration is
      included; `pnpm typecheck` / `lint` / `build` clean; `openapi/*.json`, `app/api/generated/*.ts` and
      `pnpm-lock.yaml` regenerated and committed.
- [ ] `/_dev/ui` has the ColorField and FileField sections; `AREAS.md`, the `AllowAnonymousRequest` doc comment and
      `CLAUDE.md` updated (B10); risks R1–R10 recorded as proven (or their fallback) in the implementation report.
- [ ] No test project, spec file or test framework added.

**STOP HERE. This is the last MVP story — report to the user and wait for confirmation.**
