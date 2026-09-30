<template>
  <div>
    <div class="flex flex-wrap items-center justify-between gap-3">
      <h1 class="text-2xl font-bold text-fg">{{ t('organisation.branches.title') }}</h1>
      <div class="flex items-center gap-3">
        <label class="flex items-center gap-2 text-sm text-fg">
          <input v-model="showInactive" type="checkbox" class="rounded border-border" />
          {{ t('organisation.actions.showInactive') }}
        </label>
        <UiButton @click="openCreate()">{{ t('organisation.branches.create') }}</UiButton>
      </div>
    </div>

    <UiErrorState
      v-if="forbidden"
      class="mt-6"
      :title="t('errors.problem.forbidden')"
      :description="forbiddenDetail"
    />
    <UiErrorState
      v-else-if="error"
      class="mt-6"
      :title="t('organisation.load.error')"
      :retry-label="t('organisation.load.retry')"
      @retry="refresh()"
    />
    <UiTable
      v-else
      class="mt-6"
      :caption="t('organisation.branches.title')"
      :columns="columns"
      :rows="rows"
      :row-key="(row) => row.id ?? ''"
      :loading="status === 'pending'"
    >
      <template #empty>
        <UiEmptyState :title="t('organisation.branches.empty')" />
      </template>
      <template #cell-code="{ row }"><bdi dir="ltr">{{ row.code }}</bdi></template>
      <template #cell-name="{ row }">{{ localizedName(row.name, locale) }}</template>
      <template #cell-status="{ row }">
        <UiBadge :tone="row.isActive ? 'success' : 'neutral'">
          {{ row.isActive ? t('organisation.status.active') : t('organisation.status.inactive') }}
        </UiBadge>
      </template>
      <template #cell-actions="{ row }">
        <div class="flex gap-2">
          <UiButton variant="ghost" size="sm" @click="openRename(row)">{{ t('organisation.actions.rename') }}</UiButton>
          <UiButton v-if="row.isActive" variant="ghost" size="sm" @click="openDeactivate(row)">
            {{ t('organisation.actions.deactivate') }}
          </UiButton>
        </div>
      </template>
    </UiTable>

    <UiModal
      v-model:open="modalOpen"
      :title="editing ? t('organisation.actions.rename') : t('organisation.branches.create')"
      :close-label="t('organisation.actions.close')"
    >
      <form class="flex flex-col gap-3" @submit.prevent="submit()">
        <UiInput
          v-if="!editing"
          v-model="form.code"
          dir="ltr"
          :label="t('organisation.fields.code')"
          :error="formErrors.errorFor('code')"
          required
        />
        <UiInput
          v-model="form.nameEn"
          dir="ltr"
          :label="t('organisation.fields.nameEn')"
          :error="formErrors.errorFor('nameEn')"
          required
        />
        <UiInput
          v-model="form.nameAr"
          dir="rtl"
          lang="ar"
          :label="t('organisation.fields.nameAr')"
          :error="formErrors.errorFor('nameAr')"
          required
        />
        <p v-if="formErrors.formError.value" role="alert" class="text-sm text-danger-600">
          {{ formErrors.formError.value }}
        </p>
      </form>
      <template #footer>
        <UiButton variant="secondary" @click="modalOpen = false">{{ t('organisation.actions.cancel') }}</UiButton>
        <UiButton :loading="submitting" @click="submit()">{{ t('organisation.actions.save') }}</UiButton>
      </template>
    </UiModal>

    <UiModal
      v-model:open="deactivateOpen"
      :title="t('organisation.actions.deactivate')"
      :close-label="t('organisation.actions.close')"
      size="sm"
    >
      <p class="text-sm text-fg">
        {{ t('organisation.confirm.deactivate', { name: deactivateTarget ? localizedName(deactivateTarget.name, locale) : '' }) }}
      </p>
      <template #footer>
        <UiButton variant="secondary" @click="deactivateOpen = false">{{ t('organisation.actions.cancel') }}</UiButton>
        <UiButton variant="danger" :loading="submitting" @click="confirmDeactivate()">
          {{ t('organisation.actions.deactivate') }}
        </UiButton>
      </template>
    </UiModal>
  </div>
</template>

<script setup lang="ts">
import type { BranchDto } from '~/composables/useBranches'
import { localizedName } from '~/utils/scope'
import { ProblemTypes, isProblem } from '~/api/problem'

definePageMeta({ layout: 'agent' })

const { t, locale } = useI18n()
const branches = useBranches()
const showInactive = ref(false)
const activeOnly = computed(() => !showInactive.value)
const { data, status, error, refresh } = branches.list(activeOnly)

const rows = computed(() => data.value ?? [])
const forbidden = computed(() => isProblem(error.value) && error.value.type === ProblemTypes.Forbidden)
const forbiddenDetail = computed(() => (isProblem(error.value) ? (error.value.detail ?? undefined) : undefined))

const columns = computed(() => [
  { key: 'code', label: t('organisation.fields.code') },
  { key: 'name', label: t('organisation.fields.name') },
  { key: 'status', label: t('organisation.fields.status') },
  { key: 'actions', label: t('organisation.fields.actions'), align: 'end' as const },
])

const modalOpen = ref(false)
const editing = ref<BranchDto | null>(null)
const form = reactive({ code: '', nameEn: '', nameAr: '' })
const formErrors = useFormErrors()
const submitting = ref(false)

function openCreate(): void {
  editing.value = null
  form.code = ''
  form.nameEn = ''
  form.nameAr = ''
  formErrors.clear()
  modalOpen.value = true
}

function openRename(row: BranchDto): void {
  editing.value = row
  form.code = row.code ?? ''
  form.nameEn = row.name?.en ?? ''
  form.nameAr = row.name?.ar ?? ''
  formErrors.clear()
  modalOpen.value = true
}

async function submit(): Promise<void> {
  submitting.value = true
  formErrors.clear()

  const result = editing.value
    ? await branches.rename(editing.value.id ?? '', {
        nameAr: form.nameAr,
        nameEn: form.nameEn,
        rowVersion: editing.value.rowVersion ?? '',
      })
    : await branches.create({ code: form.code, nameAr: form.nameAr, nameEn: form.nameEn })

  submitting.value = false

  if (result.ok) {
    modalOpen.value = false
    await refresh()
    return
  }

  if (!formErrors.apply(result.problem)) {
    useToasts().push({
      tone: 'danger',
      title: t(`errors.problem.${result.problem.type.replace('urn:crm:problem:', '')}`),
      description: result.problem.detail,
      correlationId: result.problem.correlationId,
    })
  }
}

const deactivateOpen = ref(false)
const deactivateTarget = ref<BranchDto | null>(null)

function openDeactivate(row: BranchDto): void {
  deactivateTarget.value = row
  deactivateOpen.value = true
}

async function confirmDeactivate(): Promise<void> {
  if (!deactivateTarget.value) return
  submitting.value = true
  await branches.deactivate(deactivateTarget.value.id ?? '', deactivateTarget.value.rowVersion ?? '')
  submitting.value = false
  deactivateOpen.value = false
  await refresh()
}
</script>
