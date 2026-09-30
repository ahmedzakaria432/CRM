<template>
  <div>
    <div class="flex flex-wrap items-center justify-between gap-3">
      <h1 class="text-2xl font-bold text-fg">{{ t('tickets.categories.title') }}</h1>
      <div class="flex flex-wrap items-center gap-3">
        <UiSelect v-model="departmentFilter" :label="t('tickets.categories.fields.department')" :options="filterOptions" />
        <label class="flex items-center gap-2 text-sm text-fg">
          <input v-model="showInactive" type="checkbox" class="rounded border-border" />
          {{ t('organisation.actions.showInactive') }}
        </label>
        <UiButton @click="openCreate()">{{ t('tickets.categories.create') }}</UiButton>
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
      :caption="t('tickets.categories.title')"
      :columns="columns"
      :rows="rows"
      :row-key="(row) => row.id ?? ''"
      :loading="status === 'pending'"
    >
      <template #empty>
        <UiEmptyState :title="t('tickets.categories.empty')" />
      </template>
      <template #cell-name="{ row }">{{ localizedName(row.name, locale) }}</template>
      <template #cell-department="{ row }">{{ departmentName(row.departmentId) }}</template>
      <template #cell-displayOrder="{ row }">{{ format.number(row.displayOrder ?? 0) }}</template>
      <template #cell-customerVisible="{ row }">
        <UiBadge :tone="row.isCustomerVisible ? 'info' : 'neutral'">
          {{ row.isCustomerVisible ? t('tickets.categories.visibility.visible') : t('tickets.categories.visibility.internal') }}
        </UiBadge>
      </template>
      <template #cell-status="{ row }">
        <UiBadge :tone="row.isActive ? 'success' : 'neutral'">
          {{ row.isActive ? t('organisation.status.active') : t('organisation.status.inactive') }}
        </UiBadge>
      </template>
      <template #cell-actions="{ row }">
        <div class="flex gap-2">
          <UiButton variant="ghost" size="sm" @click="openEdit(row)">{{ t('tickets.categories.actions.edit') }}</UiButton>
          <UiButton v-if="row.isActive" variant="ghost" size="sm" @click="openDeactivate(row)">
            {{ t('organisation.actions.deactivate') }}
          </UiButton>
          <UiButton v-else variant="ghost" size="sm" @click="reactivate(row)">
            {{ t('tickets.categories.actions.reactivate') }}
          </UiButton>
        </div>
      </template>
    </UiTable>

    <UiModal
      v-model:open="modalOpen"
      :title="editing ? t('tickets.categories.edit') : t('tickets.categories.create')"
      :close-label="t('organisation.actions.close')"
    >
      <form class="flex flex-col gap-3" @submit.prevent="submit()">
        <UiSelect
          v-if="!editing"
          v-model="form.departmentId"
          :label="t('tickets.categories.fields.department')"
          :options="departmentOptions"
          :error="formErrors.errorFor('departmentId')"
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
        <UiInput
          v-model="form.displayOrder"
          type="number"
          dir="ltr"
          :label="t('tickets.categories.fields.displayOrder')"
          :error="formErrors.errorFor('displayOrder')"
          required
        />
        <UiCheckbox v-model="form.isCustomerVisible" :label="t('tickets.categories.fields.customerVisible')" />
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
        {{ t('tickets.categories.confirm.deactivate', { name: deactivateTarget ? localizedName(deactivateTarget.name, locale) : '' }) }}
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
import type { TicketCategoryDto } from '~/composables/useTicketCategories'
import { localizedName } from '~/utils/scope'
import { ProblemTypes, isProblem } from '~/api/problem'

definePageMeta({ layout: 'agent' })

const { t, locale } = useI18n()
const format = useFormat()
const categories = useTicketCategories()
const departments = useDepartments()

const ALL = ''
const departmentFilter = ref(ALL)
const showInactive = ref(false)
const activeOnly = computed(() => !showInactive.value)
const departmentId = computed<string | undefined>(() => (departmentFilter.value === ALL ? undefined : departmentFilter.value))
const { data, status, error, refresh } = categories.list(departmentId, activeOnly)

// Active departments only, for the pickers (deactivated departments are hidden from every picker, CLAUDE.md).
const alwaysActive = ref(true)
const noBranchFilter = computed<string | undefined>(() => undefined)
const { data: departmentList } = departments.list(noBranchFilter, alwaysActive)

const rows = computed(() => data.value ?? [])
const forbidden = computed(() => isProblem(error.value) && error.value.type === ProblemTypes.Forbidden)
const forbiddenDetail = computed(() => (isProblem(error.value) ? (error.value.detail ?? undefined) : undefined))

function departmentName(id: string | undefined): string {
  const department = departmentList.value?.find((d) => d.id === id)
  return department ? localizedName(department.name, locale.value) : ''
}

const departmentOptions = computed(
  () => departmentList.value?.map((d) => ({ value: d.id ?? '', label: localizedName(d.name, locale.value) })) ?? [],
)
const filterOptions = computed(() => [
  { value: ALL, label: t('tickets.categories.allDepartments') },
  ...departmentOptions.value,
])

const columns = computed(() => [
  { key: 'name', label: t('organisation.fields.name') },
  { key: 'department', label: t('tickets.categories.fields.department') },
  { key: 'displayOrder', label: t('tickets.categories.fields.displayOrder') },
  { key: 'customerVisible', label: t('tickets.categories.fields.customerVisible') },
  { key: 'status', label: t('organisation.fields.status') },
  { key: 'actions', label: t('organisation.fields.actions'), align: 'end' as const },
])

const modalOpen = ref(false)
const editing = ref<TicketCategoryDto | null>(null)
const form = reactive({ departmentId: '', nameEn: '', nameAr: '', displayOrder: '0', isCustomerVisible: true })
const formErrors = useFormErrors()
const submitting = ref(false)

function openCreate(): void {
  editing.value = null
  form.departmentId = ''
  form.nameEn = ''
  form.nameAr = ''
  form.displayOrder = '0'
  form.isCustomerVisible = true
  formErrors.clear()
  modalOpen.value = true
}

function openEdit(row: TicketCategoryDto): void {
  editing.value = row
  form.nameEn = row.name?.en ?? ''
  form.nameAr = row.name?.ar ?? ''
  form.displayOrder = String(row.displayOrder ?? 0)
  form.isCustomerVisible = row.isCustomerVisible ?? false
  formErrors.clear()
  modalOpen.value = true
}

async function submit(): Promise<void> {
  submitting.value = true
  formErrors.clear()

  const fields = {
    nameAr: form.nameAr,
    nameEn: form.nameEn,
    displayOrder: Number(form.displayOrder),
    isCustomerVisible: form.isCustomerVisible,
  }
  const result = editing.value
    ? await categories.update(editing.value.id ?? '', { ...fields, rowVersion: editing.value.rowVersion ?? '' })
    : await categories.create({ ...fields, departmentId: form.departmentId })

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
const deactivateTarget = ref<TicketCategoryDto | null>(null)

function openDeactivate(row: TicketCategoryDto): void {
  deactivateTarget.value = row
  deactivateOpen.value = true
}

async function confirmDeactivate(): Promise<void> {
  if (!deactivateTarget.value) return
  submitting.value = true
  await categories.deactivate(deactivateTarget.value.id ?? '', deactivateTarget.value.rowVersion ?? '')
  submitting.value = false
  deactivateOpen.value = false
  await refresh()
}

async function reactivate(row: TicketCategoryDto): Promise<void> {
  await categories.reactivate(row.id ?? '', row.rowVersion ?? '')
  await refresh()
}
</script>
