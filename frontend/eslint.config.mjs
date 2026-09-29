import withNuxt from './.nuxt/eslint.config.mjs'
import prettier from 'eslint-config-prettier/flat'

// RTL is first-class (CLAUDE.md): a physical-direction utility is a bug. Matched in templates
// (vue/no-restricted-class) and in any string or template literal in script (no-restricted-syntax),
// which is where variant maps build class lists.
const physical = String.raw`(^|\s|:)-?(ml|mr|pl|pr|left|right|border-l|border-r|rounded-l|rounded-r|rounded-tl|rounded-tr|rounded-bl|rounded-br|scroll-ml|scroll-mr|text-left|text-right|float-left|float-right|clear-left|clear-right)(-|\s|$)`
const message =
  'Physical direction utility: use the logical form (ms-/me-/ps-/pe-/start-/end-/border-s/border-e/rounded-s/rounded-e/text-start/text-end).'

export default withNuxt(
  { ignores: ['app/api/generated/**', 'openapi/**'] },
  {
    rules: {
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/no-non-null-assertion': 'error',
      '@typescript-eslint/ban-ts-comment': [
        'error',
        { 'ts-ignore': true, 'ts-nocheck': true, 'ts-expect-error': true },
      ],
      'vue/no-restricted-class': ['error', `/${physical}/`],
      'no-restricted-syntax': [
        'error',
        { selector: `Literal[value=/${physical}/]`, message },
        { selector: `TemplateElement[value.raw=/${physical}/]`, message },
      ],
    },
  },
  {
    // The kit uses ui-hover:/ui-focus: so /_dev/ui can force those states (Task 4, Probe 6).
    files: ['app/components/ui/**/*.vue'],
    rules: {
      'vue/no-restricted-class': ['error', `/${physical}/`, '/(^|:)hover:/', '/(^|:)focus-visible:/'],
    },
  },
).append(prettier)
