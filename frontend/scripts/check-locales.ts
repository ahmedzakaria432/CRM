// Fails when the en/ and ar/ locale domains diverge: a file missing on one side, invalid JSON, a top-level
// key that is not the file's own name, a non-string/non-object value, an empty string, or a flattened key
// present on only one side. Node 24 runs this file directly (native type stripping - Probe 13/9). Only
// erasable TypeScript syntax is used: no `enum`, no `namespace`, no parameter properties.
import { readdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const localesRoot = fileURLToPath(new URL('../i18n/locales', import.meta.url))
const locales = ['en', 'ar'] as const
type Locale = (typeof locales)[number]
type JsonValue = string | number | boolean | null | JsonValue[] | { [key: string]: JsonValue }

const problems: string[] = []

function listFiles(locale: Locale): string[] {
  return readdirSync(join(localesRoot, locale))
    .filter((file) => file.endsWith('.json'))
    .sort()
}

function flatten(context: string, prefix: string, value: JsonValue, keys: Set<string>): void {
  if (typeof value === 'string') {
    if (value === '') problems.push(`check-locales: ${context}: empty string at "${prefix}"`)
    keys.add(prefix)
    return
  }
  if (typeof value === 'object' && value !== null && !Array.isArray(value)) {
    for (const [key, child] of Object.entries(value)) {
      flatten(context, `${prefix}.${key}`, child, keys)
    }
    return
  }
  problems.push(`check-locales: ${context}: value at "${prefix}" is neither a string nor an object`)
}

function readDomain(locale: Locale, file: string): Set<string> {
  const context = `${locale}/${file}`
  const keys = new Set<string>()
  const raw = readFileSync(join(localesRoot, locale, file), 'utf8')

  let parsed: unknown
  try {
    parsed = JSON.parse(raw)
  } catch {
    problems.push(`check-locales: ${context}: not valid JSON`)
    return keys
  }

  if (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed)) {
    problems.push(`check-locales: ${context}: top level is not an object`)
    return keys
  }

  const record = parsed as Record<string, JsonValue>
  const domainName = file.slice(0, file.length - '.json'.length)
  const topKeys = Object.keys(record)
  if (topKeys.length !== 1 || topKeys[0] !== domainName) {
    problems.push(`check-locales: ${context}: must have exactly one top-level key, "${domainName}"`)
    return keys
  }

  flatten(context, domainName, record[domainName] as JsonValue, keys)
  return keys
}

const filesByLocale: Record<Locale, string[]> = { en: listFiles('en'), ar: listFiles('ar') }
const allFiles = new Set<string>([...filesByLocale.en, ...filesByLocale.ar])
const keysByLocale: Record<Locale, Map<string, Set<string>>> = { en: new Map(), ar: new Map() }

for (const file of allFiles) {
  for (const locale of locales) {
    if (!filesByLocale[locale].includes(file)) {
      problems.push(`check-locales: ${file}: missing in ${locale}/`)
      continue
    }
    keysByLocale[locale].set(file, readDomain(locale, file))
  }
}

for (const file of allFiles) {
  const enKeys = keysByLocale.en.get(file)
  const arKeys = keysByLocale.ar.get(file)
  if (enKeys === undefined || arKeys === undefined) continue // already reported as missing

  for (const key of enKeys) {
    if (!arKeys.has(key)) problems.push(`check-locales: ar/${file}: missing key ${key}`)
  }
  for (const key of arKeys) {
    if (!enKeys.has(key)) problems.push(`check-locales: en/${file}: missing key ${key}`)
  }
}

if (problems.length > 0) {
  for (const problem of problems) {
    process.stderr.write(`${problem}\n`)
  }
  process.exit(1)
}

process.stdout.write(`check-locales: ${allFiles.size} domain files, en/ar key sets match\n`)
