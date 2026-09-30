<template>
  <div>
    <div class="flex flex-wrap items-center justify-between gap-3">
      <h1 class="text-2xl font-bold text-fg">{{ t('identity.users.title') }}</h1>
      <UiButton v-if="can(Permissions.AdminUsers)" @click="openCreate()">{{ t('identity.users.create') }}</UiButton>
    </div>

    <form role="search" class="mt-4 flex items-end gap-2" @submit.prevent="applySearch()">
      <div class="w-full max-w-sm">
        <UiInput v-model="searchInput" type="search" :label="t('identity.users.search')" />
      </div>
      <UiButton type="submit" variant="secondary">{{ t('identity.users.searchAction') }}</UiButton>
    </form>

    <UiErrorState
      v-if="forbidden"
      class="mt-6"
      :title="t('errors.problem.forbidden')"
      :description="forbiddenDetail"
    />
    <UiErrorState
      v-else-if="error && status !== 'pending'"
      class="mt-6"
      :title="t('identity.load.error')"
      :retry-label="t('identity.load.retry')"
      @retry="refresh()"
    />
    <template v-else>
      <UiTable
        class="mt-6"
        :caption="t('identity.users.title')"
        :columns="columns"
        :rows="rows"
        :row-key="(row) => row.id ?? ''"
        :loading="status === 'pending'"
      >
        <template #empty>
          <UiEmptyState :title="t('identity.users.empty')" />
        </template>
        <template #cell-name="{ row }">
          <NuxtLink :to="`/admin/users/${row.id}`" class="font-medium text-primary-600 underline">
            {{ row.displayName }}
          </NuxtLink>
        </template>
        <template #cell-email="{ row }"><bdi dir="ltr">{{ row.email }}</bdi></template>
        <template #cell-locale="{ row }">{{ t(`identity.locale.${row.preferredLocale === 'ar' ? 'ar' : 'en'}`) }}</template>
        <template #cell-status="{ row }">
          <UiBadge :tone="statusTone(row)">{{ statusLabel(row) }}</UiBadge>
        </template>
        <template #cell-lastSignIn="{ row }">
          {{ row.lastSignInAtUtc ? format.dateTime(row.lastSignInAtUtc) : t('identity.fields.never') }}
        </template>
      </UiTable>
      <UiPagination
        v-if="total > usersPageSize"
        v-model:page="page"
        class="mt-4"
        :page-size="usersPageSize"
        :total="total"
        :label="t('shell.pagination.label')"
        :previous-label="t('shell.pagination.previous')"
        :next-label="t('shell.pagination.next')"
        :page-label="t('shell.pagination.page')"
      />
    </template>

    <UiModal v-model:open="modalOpen" :title="t('identity.users.create')" :close-label="t('identity.actions.close')">
      <form class="flex flex-col gap-3" novalidate @submit.prevent="submit()">
        <UiInput
          v-model="form.email"
          type="email"
          dir="ltr"
          autocomplete="off"
          :label="t('identity.fields.email')"
          :error="formErrors.errorFor('email') ?? emailTaken"
          required
        />
        <UiInput
          v-model="form.displayName"
          :label="t('identity.fields.displayName')"
          :error="formErrors.errorFor('displayName')"
          required
        />
        <UiSelect
          v-model="form.preferredLocale"
          :label="t('identity.fields.locale')"
          :options="localeOptions"
          :error="formErrors.errorFor('preferredLocale')"
          required
        />
        <UiInput
          v-model="form.initialPassword"
          type="password"
          dir="ltr"
          autocomplete="new-password"
          :label="t('identity.fields.initialPassword')"
          :hint="t('identity.account.passwordRules')"
          :error="formErrors.errorFor('initialPassword')"
          required
        />
        <p v-if="formErrors.formError.value" role="alert" class="text-sm text-danger-600">
          {{ formErrors.formError.value }}
        </p>
        <button type="submit" class="hidden" tabindex="-1" aria-hidden="true" />
      </form>
      <template #footer>
        <UiButton variant="secondary" @click="modalOpen = false">{{ t('identity.actions.cancel') }}</UiButton>
        <UiButton :loading="submitting" @click="submit()">{{ t('identity.actions.save') }}</UiButton>
      </template>
    </UiModal>
  </div>
</template>

<script setup lang="ts">
import type { UserDto } from '~/composables/useUsers'
import { usersPageSize } from '~/composables/useUsers'
import { ProblemTypes, problemOf } from '~/api/problem'
import { Permissions } from '~/utils/permissions'

definePageMeta({ layout: 'agent' })

const { t } = useI18n()
const { can } = useCan()
const format = useFormat()
const users = useUsers()

const searchInput = ref('')
const search = ref('')
const page = ref(1)
const { data, status, error, refresh } = users.list(search, page)

const rows = computed(() => data.value?.items ?? [])
const total = computed(() => data.value?.total ?? 0)
const forbidden = computed(() => problemOf(error.value)?.type === ProblemTypes.Forbidden)
const forbiddenDetail = computed(() => (problemOf(error.value)?.detail ?? undefined))

const columns = computed(() => [
  { key: 'name', label: t('identity.fields.displayName') },
  { key: 'email', label: t('identity.fields.email') },
  { key: 'locale', label: t('identity.fields.locale') },
  { key: 'status', label: t('identity.fields.status') },
  { key: 'lastSignIn', label: t('identity.fields.lastSignIn') },
])

function applySearch(): void {
  // A new search always starts on page 1.
  page.value = 1
  search.value = searchInput.value.trim()
}

function statusTone(user: UserDto): 'success' | 'neutral' | 'warning' {
  if (!user.isActive) return 'neutral'
  return user.isLockedOut ? 'warning' : 'success'
}

function statusLabel(user: UserDto): string {
  if (!user.isActive) return t('identity.status.inactive')
  return user.isLockedOut ? t('identity.status.locked') : t('identity.status.active')
}

const localeOptions = computed(() => [
  { value: 'en', label: t('identity.locale.en') },
  { value: 'ar', label: t('identity.locale.ar') },
])

const modalOpen = ref(false)
const form = reactive({ email: '', displayName: '', preferredLocale: 'en', initialPassword: '' })
const formErrors = useFormErrors()
const emailTaken = ref<string | undefined>(undefined)
const submitting = ref(false)

function openCreate(): void {
  form.email = ''
  form.displayName = ''
  form.preferredLocale = 'en'
  form.initialPassword = ''
  formErrors.clear()
  emailTaken.value = undefined
  modalOpen.value = true
}

async function submit(): Promise<void> {
  if (submitting.value) return
  submitting.value = true
  formErrors.clear()
  emailTaken.value = undefined

  const result = await users.create({ ...form })
  submitting.value = false

  if (result.ok) {
    modalOpen.value = false
    await navigateTo(`/admin/users/${result.data.id}`)
    return
  }

  if (formErrors.apply(result.problem)) return

  if (result.problem.code === 'Identity.EmailTaken') {
    emailTaken.value = t('errors.code.Identity.EmailTaken')
    return
  }

  useToasts().push({
    tone: 'danger',
    title: t(`errors.problem.${result.problem.type.replace('urn:crm:problem:', '')}`),
    description: result.problem.detail,
    correlationId: result.problem.correlationId,
  })
}
</script>
