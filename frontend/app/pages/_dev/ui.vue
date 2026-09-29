<template>
  <div>
    <header class="sticky top-0 z-40 border-b border-border bg-surface p-4">
      <div class="flex flex-wrap items-center justify-between gap-4">
        <nav aria-label="Component index" class="flex flex-wrap gap-x-3 gap-y-1 text-sm">
          <a
            v-for="section in sections"
            :key="section.id"
            :href="`#${section.id}`"
            class="text-fg-muted hover:text-fg hover:underline"
          >
            {{ section.title }}
          </a>
        </nav>
        <div class="flex items-center gap-3">
          <div class="flex gap-1" role="group" aria-label="Preview width">
            <button
              v-for="option in widths"
              :key="option.value"
              type="button"
              class="rounded-md border border-border px-2 py-1 text-xs"
              :class="width === option.value ? 'bg-primary-600 text-fg-inverse' : 'text-fg hover:bg-surface-raised'"
              @click="width = option.value"
            >
              {{ option.label }}
            </button>
          </div>
          <UiThemeToggle light-label="Switch page chrome to light" dark-label="Switch page chrome to dark" />
        </div>
      </div>
    </header>

    <div class="mx-auto p-4" :style="{ maxWidth: widthStyle }">
      <p class="mb-6 max-w-prose text-sm text-fg-muted">
        Every UI kit component, in every state, in both themes. This page is the review surface for a project
        with no component tests (CLAUDE.md, "Testing policy"). It never ships —
        <code class="font-mono">modules/dev-only-pages.ts</code> strips <code class="font-mono">/_dev/**</code>
        from production builds.
      </p>

      <!-- Button -->
      <DevSection anchor-id="button" title="Button">
        <DevStateRow label="default">
          <UiButton>Primary</UiButton>
          <UiButton variant="secondary">Secondary</UiButton>
          <UiButton variant="ghost">Ghost</UiButton>
          <UiButton variant="danger">Danger</UiButton>
        </DevStateRow>
        <DevStateRow label="hover">
          <UiButton data-force="hover">Hover</UiButton>
        </DevStateRow>
        <DevStateRow label="focus">
          <UiButton data-force="focus">Focus</UiButton>
        </DevStateRow>
        <DevStateRow label="disabled">
          <UiButton disabled>Disabled</UiButton>
        </DevStateRow>
        <DevStateRow label="loading">
          <UiButton loading>Loading</UiButton>
        </DevStateRow>
        <DevStateRow label="error">
          <span class="text-xs text-fg-muted">n/a — Button has no error state</span>
        </DevStateRow>
        <DevStateRow label="long content">
          <UiButton class="max-w-xs">{{ longSentence }}</UiButton>
        </DevStateRow>
      </DevSection>

      <!-- Input -->
      <DevSection anchor-id="input" title="Input">
        <DevStateRow label="default">
          <UiInput v-model="inputDefault" label="Name" class="w-64" />
        </DevStateRow>
        <DevStateRow label="hover">
          <span class="text-xs text-fg-muted">n/a — no distinct hover state</span>
        </DevStateRow>
        <DevStateRow label="focus">
          <UiInput label="Name" data-force="focus" class="w-64" />
        </DevStateRow>
        <DevStateRow label="disabled">
          <UiInput label="Name" disabled class="w-64" />
        </DevStateRow>
        <DevStateRow label="loading">
          <span class="text-xs text-fg-muted">n/a — Input has no loading state</span>
        </DevStateRow>
        <DevStateRow label="error">
          <UiInput label="Name" error="'Name' must not be empty." class="w-64" />
        </DevStateRow>
        <DevStateRow label="long content">
          <UiInput :label="longSentence" :model-value="longToken" class="w-64" />
        </DevStateRow>
      </DevSection>

      <!-- Textarea -->
      <DevSection anchor-id="textarea" title="Textarea">
        <DevStateRow label="default">
          <UiTextarea v-model="textareaDefault" label="Lines" class="w-64" />
        </DevStateRow>
        <DevStateRow label="hover">
          <span class="text-xs text-fg-muted">n/a — no distinct hover state</span>
        </DevStateRow>
        <DevStateRow label="focus">
          <UiTextarea label="Lines" data-force="focus" class="w-64" />
        </DevStateRow>
        <DevStateRow label="disabled">
          <UiTextarea label="Lines" disabled class="w-64" />
        </DevStateRow>
        <DevStateRow label="loading">
          <span class="text-xs text-fg-muted">n/a — Textarea has no loading state</span>
        </DevStateRow>
        <DevStateRow label="error">
          <UiTextarea label="Lines" error="At least one line is required." class="w-64" />
        </DevStateRow>
        <DevStateRow label="long content">
          <UiTextarea :label="longSentence" :model-value="`${longToken}\n${arabicString}`" class="w-64" />
        </DevStateRow>
      </DevSection>

      <!-- Select -->
      <DevSection anchor-id="select" title="Select">
        <DevStateRow label="default">
          <UiSelect v-model="selectDefault" label="Priority" :options="selectOptions" class="w-64" />
        </DevStateRow>
        <DevStateRow label="hover">
          <span class="text-xs text-fg-muted">n/a — no distinct hover state</span>
        </DevStateRow>
        <DevStateRow label="focus">
          <UiSelect label="Priority" :options="selectOptions" data-force="focus" class="w-64" />
        </DevStateRow>
        <DevStateRow label="disabled">
          <UiSelect label="Priority" :options="selectOptions" disabled class="w-64" />
        </DevStateRow>
        <DevStateRow label="loading">
          <span class="text-xs text-fg-muted">n/a — Select has no loading state</span>
        </DevStateRow>
        <DevStateRow label="error">
          <UiSelect label="Priority" :options="selectOptions" error="Choose a priority." class="w-64" />
        </DevStateRow>
        <DevStateRow label="long content">
          <UiSelect :label="longSentence" :options="longSelectOptions" class="w-64" />
        </DevStateRow>
      </DevSection>

      <!-- Modal -->
      <DevSection anchor-id="modal" title="Modal">
        <DevStateRow label="default / long content (static preview)">
          <dialog open class="static m-0 w-full max-w-md rounded-lg border border-border bg-surface p-4 text-fg">
            <p class="mb-2 text-xs font-semibold text-fg-muted">Static preview — not interactive</p>
            <h3 class="text-lg font-semibold">{{ longSentence }}</h3>
            <p class="mt-2 break-words">{{ arabicString }}</p>
          </dialog>
        </DevStateRow>
        <DevStateRow label="hover / focus / disabled / loading / error">
          <span class="text-xs text-fg-muted">n/a — see Button above; the close button reuses it</span>
        </DevStateRow>
        <DevStateRow label="interactive (keyboard test target)">
          <UiButton @click="modalOpen = true">Open modal</UiButton>
        </DevStateRow>
      </DevSection>

      <!-- Drawer -->
      <DevSection anchor-id="drawer" title="Drawer">
        <DevStateRow label="default / long content (static preview)">
          <dialog
            open
            class="static m-0 w-full max-w-xs rounded-lg border border-border bg-surface p-4 text-fg"
          >
            <p class="mb-2 text-xs font-semibold text-fg-muted">Static preview — not interactive</p>
            <h3 class="text-lg font-semibold">{{ longSentence }}</h3>
          </dialog>
        </DevStateRow>
        <DevStateRow label="hover / focus / disabled / loading / error">
          <span class="text-xs text-fg-muted">n/a — see Button above; the close button reuses it</span>
        </DevStateRow>
        <DevStateRow label="interactive (keyboard test target)">
          <UiButton @click="drawerOpen = true">Open drawer (start)</UiButton>
        </DevStateRow>
      </DevSection>

      <!-- Table -->
      <DevSection anchor-id="table" title="Table">
        <DevStateRow label="default">
          <UiTable
            caption="Sample rows"
            :columns="tableColumns"
            :rows="tableRows"
            :row-key="(row) => row.id"
            class="w-full"
          >
            <template #cell-name="{ row }">{{ row.name }}</template>
            <template #cell-status="{ row }">{{ row.status }}</template>
          </UiTable>
        </DevStateRow>
        <DevStateRow label="hover / focus">
          <span class="text-xs text-fg-muted">n/a — rows carry no interactive state in this scaffold</span>
        </DevStateRow>
        <DevStateRow label="disabled">
          <span class="text-xs text-fg-muted">n/a — Table has no disabled state</span>
        </DevStateRow>
        <DevStateRow label="loading">
          <UiTable caption="Loading" :columns="tableColumns" :rows="emptyTableRows" :row-key="(row) => row.id" loading class="w-full" />
        </DevStateRow>
        <DevStateRow label="error">
          <span class="text-xs text-fg-muted">n/a — a failed load renders UiErrorState instead, see below</span>
        </DevStateRow>
        <DevStateRow label="empty / long content">
          <UiTable caption="Empty" :columns="tableColumns" :rows="emptyTableRows" :row-key="(row) => row.id" class="w-full">
            <template #empty>
              <UiEmptyState title="No rows" :description="longSentence" />
            </template>
          </UiTable>
        </DevStateRow>
      </DevSection>

      <!-- Badge -->
      <DevSection anchor-id="badge" title="Badge">
        <DevStateRow label="default (all tones)">
          <UiBadge tone="neutral">Neutral</UiBadge>
          <UiBadge tone="primary">Primary</UiBadge>
          <UiBadge tone="success">Success</UiBadge>
          <UiBadge tone="warning">Warning</UiBadge>
          <UiBadge tone="danger">Danger</UiBadge>
          <UiBadge tone="info">Info</UiBadge>
        </DevStateRow>
        <DevStateRow label="hover / focus / disabled / loading">
          <span class="text-xs text-fg-muted">n/a — Badge is not interactive</span>
        </DevStateRow>
        <DevStateRow label="error">
          <UiBadge tone="danger">Failed</UiBadge>
        </DevStateRow>
        <DevStateRow label="long content">
          <UiBadge tone="neutral" class="max-w-xs">{{ longToken }}</UiBadge>
        </DevStateRow>
      </DevSection>

      <!-- Avatar -->
      <DevSection anchor-id="avatar" title="Avatar">
        <DevStateRow label="default (initials)">
          <UiAvatar name="Ahmed Zakaria" size="sm" />
          <UiAvatar name="Ahmed Zakaria" size="md" />
          <UiAvatar name="Ahmed Zakaria" size="lg" />
        </DevStateRow>
        <DevStateRow label="hover / focus / disabled / loading / error">
          <span class="text-xs text-fg-muted">n/a — Avatar is not interactive</span>
        </DevStateRow>
        <DevStateRow label="long content (Arabic initials + broken image)">
          <UiAvatar :name="arabicString" />
          <UiAvatar name="Ahmed Zakaria" src="/does-not-exist.png" />
        </DevStateRow>
      </DevSection>

      <!-- Tabs -->
      <DevSection anchor-id="tabs" title="Tabs">
        <DevStateRow label="default / focus / disabled (arrow keys, Home, End)">
          <UiTabs v-model="tabsActive" :tabs="tabsList" label="Example tabs" class="w-full">
            <template #panel="{ id }">
              <p class="p-2 text-sm">Panel: {{ id }}</p>
            </template>
          </UiTabs>
        </DevStateRow>
        <DevStateRow label="hover / loading / error">
          <span class="text-xs text-fg-muted">n/a — Tabs has no such state</span>
        </DevStateRow>
        <DevStateRow label="long content">
          <UiTabs :tabs="longTabsList" model-value="one" label="Long tab labels" class="w-full">
            <template #panel="{ id }">
              <p class="p-2 text-sm">{{ id }}</p>
            </template>
          </UiTabs>
        </DevStateRow>
      </DevSection>

      <!-- Toast -->
      <DevSection anchor-id="toast" title="Toast">
        <DevStateRow label="default (push into the real toast store)">
          <UiButton size="sm" @click="pushToast('info')">Push info</UiButton>
          <UiButton size="sm" @click="pushToast('success')">Push success</UiButton>
          <UiButton size="sm" @click="pushToast('warning')">Push warning</UiButton>
          <UiButton size="sm" @click="pushToast('danger')">Push danger</UiButton>
        </DevStateRow>
        <DevStateRow label="hover / focus (dismiss button)">
          <UiToast tone="info" title="Hover/focus the dismiss button" dismiss-label="Dismiss" reference-label="Reference" />
        </DevStateRow>
        <DevStateRow label="disabled / loading">
          <span class="text-xs text-fg-muted">n/a — Toast has no such state</span>
        </DevStateRow>
        <DevStateRow label="error (static render)">
          <UiToast
            tone="danger"
            title="Something went wrong"
            description="The server could not complete the request."
            correlation-id="11111111-1111-1111-1111-111111111111"
            dismiss-label="Dismiss"
            reference-label="Reference"
          />
        </DevStateRow>
        <DevStateRow label="long content">
          <UiToast :tone="'warning'" :title="longSentence" :description="arabicString" dismiss-label="Dismiss" reference-label="Reference" />
        </DevStateRow>
      </DevSection>

      <!-- Skeleton -->
      <DevSection anchor-id="skeleton" title="Skeleton">
        <DevStateRow label="default (text, rect, circle)">
          <UiSkeleton variant="text" :lines="3" class="w-48" />
          <UiSkeleton variant="rect" class="w-32" />
          <UiSkeleton variant="circle" class="size-10" />
        </DevStateRow>
        <DevStateRow label="hover / focus / disabled / error">
          <span class="text-xs text-fg-muted">n/a — Skeleton is decorative (aria-hidden)</span>
        </DevStateRow>
        <DevStateRow label="loading">
          <UiSkeleton variant="text" :lines="1" class="w-48" />
        </DevStateRow>
        <DevStateRow label="long content">
          <span class="text-xs text-fg-muted">n/a — Skeleton renders no text content</span>
        </DevStateRow>
      </DevSection>

      <!-- EmptyState -->
      <DevSection anchor-id="empty-state" title="EmptyState">
        <DevStateRow label="default">
          <UiEmptyState title="No tickets yet" description="New tickets will appear here." />
        </DevStateRow>
        <DevStateRow label="hover / focus / disabled / loading / error">
          <span class="text-xs text-fg-muted">n/a — EmptyState has no such state</span>
        </DevStateRow>
        <DevStateRow label="long content">
          <UiEmptyState :title="longSentence" :description="arabicString" />
        </DevStateRow>
      </DevSection>

      <!-- ErrorState -->
      <DevSection anchor-id="error-state" title="ErrorState">
        <DevStateRow label="default">
          <UiErrorState title="Something went wrong" description="Please try again." retry-label="Try again" />
        </DevStateRow>
        <DevStateRow label="hover / focus / disabled / loading">
          <span class="text-xs text-fg-muted">n/a — see Button above for the retry button's own states</span>
        </DevStateRow>
        <DevStateRow label="error (with correlation id)">
          <UiErrorState
            title="Something went wrong"
            description="The server could not complete the request."
            correlation-id="11111111-1111-1111-1111-111111111111"
            reference-label="Reference"
            retry-label="Try again"
          />
        </DevStateRow>
        <DevStateRow label="long content">
          <UiErrorState :title="longSentence" :description="arabicString" retry-label="Try again" />
        </DevStateRow>
      </DevSection>

      <!-- Pagination -->
      <DevSection anchor-id="pagination" title="Pagination">
        <DevStateRow label="default (windowed 1 … 4 5 6 … 20)">
          <UiPagination
            v-model:page="paginationPage"
            :page-size="10"
            :total="200"
            label="Pagination"
            previous-label="Previous"
            next-label="Next"
            page-label="Page"
          />
        </DevStateRow>
        <DevStateRow label="hover / focus">
          <span class="text-xs text-fg-muted">n/a — see Button-style hover/focus above; bind data-force locally to test</span>
        </DevStateRow>
        <DevStateRow label="disabled (at bounds)">
          <UiPagination
            :page="1"
            :page-size="10"
            :total="30"
            label="Pagination at first page"
            previous-label="Previous"
            next-label="Next"
            page-label="Page"
          />
        </DevStateRow>
        <DevStateRow label="loading / error">
          <span class="text-xs text-fg-muted">n/a — Pagination has no such state</span>
        </DevStateRow>
        <DevStateRow label="long content">
          <UiPagination
            :page="500"
            :page-size="10"
            :total="10000"
            label="Pagination with many pages"
            previous-label="Previous"
            next-label="Next"
            page-label="Page"
          />
        </DevStateRow>
      </DevSection>

      <!-- Spinner -->
      <DevSection anchor-id="spinner" title="Spinner">
        <DevStateRow label="default (sm, md, lg)">
          <UiSpinner size="sm" />
          <UiSpinner size="md" />
          <UiSpinner size="lg" />
        </DevStateRow>
        <DevStateRow label="hover / focus / disabled / error / long content">
          <span class="text-xs text-fg-muted">n/a — Spinner is decorative (aria-hidden)</span>
        </DevStateRow>
        <DevStateRow label="loading">
          <UiSpinner />
        </DevStateRow>
      </DevSection>

      <!-- ThemeToggle -->
      <DevSection anchor-id="theme-toggle" title="ThemeToggle">
        <DevStateRow label="default">
          <UiThemeToggle light-label="Switch to light theme" dark-label="Switch to dark theme" />
        </DevStateRow>
        <DevStateRow label="hover">
          <UiButton variant="ghost" size="sm" data-force="hover">☀️</UiButton>
        </DevStateRow>
        <DevStateRow label="focus">
          <UiButton variant="ghost" size="sm" data-force="focus">☀️</UiButton>
        </DevStateRow>
        <DevStateRow label="disabled / loading / error / long content">
          <span class="text-xs text-fg-muted">n/a — ThemeToggle wraps UiButton with fixed icon content</span>
        </DevStateRow>
      </DevSection>
    </div>

    <UiModal v-model:open="modalOpen" title="Example modal" close-label="Close">
      <p class="break-words">{{ longSentence }}</p>
      <template #footer>
        <UiButton variant="ghost" @click="modalOpen = false">Cancel</UiButton>
        <UiButton @click="modalOpen = false">Confirm</UiButton>
      </template>
    </UiModal>

    <UiDrawer v-model:open="drawerOpen" title="Example drawer" close-label="Close" side="start">
      <p class="break-words">{{ longSentence }}</p>
    </UiDrawer>
  </div>
</template>

<script setup lang="ts">
definePageMeta({ layout: false })

const loremWords =
  'Lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod tempor incididunt ut labore et dolore magna aliqua ut enim ad minim veniam quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur excepteur sint occaecat cupidatat non proident sunt in culpa qui officia deserunt mollit anim id est laborum'.split(
    ' ',
  )
const longSentence = Array.from({ length: 80 }, (_, index) => loremWords[index % loremWords.length]).join(' ') + '.'
const longToken = 'A'.repeat(300)
const arabicString = 'مرحبا بكم في نظام دعم العملاء متعدد الأقسام والفروع'

type Width = 375 | 768 | 1280 | 'full'
const widths: ReadonlyArray<{ value: Width; label: string }> = [
  { value: 375, label: '375' },
  { value: 768, label: '768' },
  { value: 1280, label: '1280' },
  { value: 'full', label: 'Full' },
]
const width = ref<Width>('full')
const widthStyle = computed(() => (width.value === 'full' ? 'none' : `${width.value}px`))

const sections: ReadonlyArray<{ id: string; title: string }> = [
  { id: 'button', title: 'Button' },
  { id: 'input', title: 'Input' },
  { id: 'textarea', title: 'Textarea' },
  { id: 'select', title: 'Select' },
  { id: 'modal', title: 'Modal' },
  { id: 'drawer', title: 'Drawer' },
  { id: 'table', title: 'Table' },
  { id: 'badge', title: 'Badge' },
  { id: 'avatar', title: 'Avatar' },
  { id: 'tabs', title: 'Tabs' },
  { id: 'toast', title: 'Toast' },
  { id: 'skeleton', title: 'Skeleton' },
  { id: 'empty-state', title: 'EmptyState' },
  { id: 'error-state', title: 'ErrorState' },
  { id: 'pagination', title: 'Pagination' },
  { id: 'spinner', title: 'Spinner' },
  { id: 'theme-toggle', title: 'ThemeToggle' },
]

const inputDefault = ref('')
const textareaDefault = ref('')

const selectOptions = [
  { value: 'low', label: 'Low' },
  { value: 'medium', label: 'Medium' },
  { value: 'high', label: 'High' },
]
const longSelectOptions = [{ value: 'long', label: longSentence }, ...selectOptions]
const selectDefault = ref('medium')

const modalOpen = ref(false)
const drawerOpen = ref(false)

interface SampleRow {
  id: string
  name: string
  status: string
}
const tableColumns = [
  { key: 'name', label: 'Name' },
  { key: 'status', label: 'Status', align: 'end' as const },
]
const tableRows: ReadonlyArray<SampleRow> = [
  { id: '1', name: 'Ada Lovelace', status: 'Open' },
  { id: '2', name: 'Grace Hopper', status: 'Closed' },
]
const emptyTableRows: ReadonlyArray<SampleRow> = []

const tabsList = [
  { id: 'one', label: 'One' },
  { id: 'two', label: 'Two' },
  { id: 'three', label: 'Three', disabled: true },
]
const tabsActive = ref('one')
const longTabsList = [
  { id: 'one', label: longSentence.slice(0, 60) },
  { id: 'two', label: arabicString },
]

const paginationPage = ref(5)

const toasts = useToasts()
function pushToast(tone: 'info' | 'success' | 'warning' | 'danger'): void {
  toasts.push({ tone, title: `${tone} toast`, description: 'Pushed from /_dev/ui.' })
}
</script>
