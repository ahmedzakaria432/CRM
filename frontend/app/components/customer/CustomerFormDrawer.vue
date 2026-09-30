<template>
  <UiDrawer
    v-model:open="open"
    size="lg"
    :title="mode === 'create' ? t('customers.form.createTitle') : t('customers.form.editTitle')"
    :close-label="t('customers.form.close')"
  >
    <form class="flex flex-col gap-3" novalidate @submit.prevent="submit()">
      <UiInput
        v-model="form.displayNameEn"
        dir="ltr"
        lang="en"
        maxlength="200"
        :label="t('customers.fields.displayNameEn')"
        :hint="t('customers.fields.nameHint')"
        :error="formErrors.errorFor('displayNameEn')"
      />
      <UiInput
        v-model="form.displayNameAr"
        dir="rtl"
        lang="ar"
        maxlength="200"
        :label="t('customers.fields.displayNameAr')"
        :hint="t('customers.fields.nameHint')"
        :error="formErrors.errorFor('displayNameAr')"
      />
      <UiSelect
        v-model="form.type"
        :label="t('customers.fields.type')"
        :options="typeOptions"
        :error="formErrors.errorFor('type')"
        required
      />
      <UiInput
        v-if="form.type === 'Organisation'"
        v-model="form.legalName"
        maxlength="300"
        :label="t('customers.fields.legalName')"
        :error="formErrors.errorFor('legalName')"
        required
      />
      <UiSelect
        v-model="form.preferredLanguage"
        :label="t('customers.fields.preferredLanguage')"
        :options="languageOptions"
        :error="formErrors.errorFor('preferredLanguage')"
        required
      />
      <UiSelect
        v-if="mode === 'create' && showDepartment"
        v-model="form.departmentId"
        :label="t('customers.fields.department')"
        :placeholder="t('customers.fields.department')"
        :options="departmentOptions"
        :error="formErrors.errorFor('departmentId')"
        required
      />

      <fieldset v-if="mode === 'create'" class="flex flex-col gap-3 rounded-md border border-border p-3">
        <legend class="px-1 text-sm font-medium text-fg">{{ t('customers.form.primaryContact') }}</legend>
        <p class="text-xs text-fg-muted">{{ t('customers.form.primaryContactHint') }}</p>
        <UiInput
          v-model="form.contact.name"
          :label="t('customers.contacts.fields.name')"
          :error="formErrors.errorFor('contacts[0].name')"
        />
        <UiInput
          v-model="form.contact.jobTitle"
          :label="t('customers.contacts.fields.jobTitle')"
          :error="formErrors.errorFor('contacts[0].jobTitle')"
        />
        <UiInput
          v-model="form.contact.email"
          type="email"
          dir="ltr"
          autocomplete="off"
          :label="t('customers.contacts.fields.email')"
          :error="formErrors.errorFor('contacts[0].email')"
        />
        <UiInput
          v-model="form.contact.phone"
          type="tel"
          dir="ltr"
          autocomplete="off"
          :label="t('customers.contacts.fields.phone')"
          :hint="t('customers.contacts.fields.phoneHint')"
          :error="formErrors.errorFor('contacts[0].phone')"
        />
        <UiCheckbox
          v-model="form.contact.hasPortalAccess"
          :label="t('customers.contacts.fields.hasPortalAccess')"
          :error="formErrors.errorFor('contacts[0].hasPortalAccess')"
        />
      </fieldset>

      <div
        v-if="duplicates.length > 0"
        role="alert"
        class="rounded-md border border-warning-500 bg-warning-50 p-3 text-warning-700"
      >
        <p class="text-sm font-semibold">{{ t('customers.duplicate.title') }}</p>
        <p class="mt-1 text-sm">{{ t('customers.duplicate.description') }}</p>
        <ul class="mt-2 flex flex-col gap-2">
          <li v-for="candidate in duplicates" :key="candidate.id" class="min-w-0 rounded-md bg-surface p-2 text-fg">
            <div class="flex flex-wrap items-center gap-2">
              <NuxtLink
                :to="`/agent/customers/${candidate.id}`"
                target="_blank"
                class="break-words font-medium text-primary-600 underline"
              >
                {{ localizedName(candidate.displayName, locale) }}
              </NuxtLink>
              <bdi dir="ltr" class="font-mono text-xs text-fg-muted">{{ candidate.number }}</bdi>
              <UiBadge size="sm" :tone="statusTone(candidate.status)">{{ candidateStatus(candidate.status) }}</UiBadge>
            </div>
            <p class="mt-1 break-words text-xs text-fg-muted">
              {{ t('customers.duplicate.matched', { values: matchedValues(candidate) }) }}
            </p>
            <NuxtLink
              :to="`/agent/customers/${candidate.id}`"
              target="_blank"
              class="mt-1 inline-block text-xs text-primary-600 underline"
            >
              {{ t('customers.duplicate.open') }}
            </NuxtLink>
          </li>
        </ul>
      </div>

      <p v-if="formErrors.formError.value" role="alert" class="text-sm text-danger-600">
        {{ formErrors.formError.value }}
      </p>
      <button type="submit" class="hidden" tabindex="-1" aria-hidden="true" />
    </form>
    <template #footer>
      <template v-if="duplicates.length > 0">
        <UiButton variant="secondary" @click="duplicates = []">{{ t('customers.duplicate.back') }}</UiButton>
        <UiButton :loading="submitting" @click="submit(true)">{{ t('customers.duplicate.createAnyway') }}</UiButton>
      </template>
      <template v-else>
        <UiButton variant="secondary" @click="open = false">{{ t('customers.form.cancel') }}</UiButton>
        <UiButton :loading="submitting" @click="submit()">
          {{ mode === 'create' ? t('customers.form.create') : t('customers.form.save') }}
        </UiButton>
      </template>
    </template>
  </UiDrawer>
</template>

<script setup lang="ts">
import { useCustomerCommands } from '~/composables/customers/useCustomerCommands'
import type {
  CreateCustomerRequest,
  CustomerDto,
  DuplicateCandidateDto,
  UpdateCustomerRequest,
} from '~/composables/customers/types'
import { ProblemTypes } from '~/api/problem'
import { isolateLtr } from '~/utils/format'
import { localizedName } from '~/utils/scope'
import { createFieldForCode, customerTypes, profileFieldForCode, statusTone } from '~/utils/customers'

interface Props {
  mode: 'create' | 'edit'
  customer?: CustomerDto
}
const props = withDefaults(defineProps<Props>(), { customer: undefined })
const emit = defineEmits<{ created: [CustomerDto]; saved: [CustomerDto]; conflict: [] }>()
const open = defineModel<boolean>('open', { default: false })

const { t, locale } = useI18n()
const scope = useScopeStore()
const toasts = useToasts()
const commands = useCustomerCommands()
const formErrors = useFormErrors()

const submitting = ref(false)
const duplicates = ref<DuplicateCandidateDto[]>([])

function uiLanguage(): 'ar' | 'en' {
  return locale.value === 'ar' ? 'ar' : 'en'
}

const form = reactive({
  displayNameEn: '',
  displayNameAr: '',
  type: 'Individual',
  legalName: '',
  preferredLanguage: uiLanguage(),
  departmentId: '',
  contact: { name: '', jobTitle: '', email: '', phone: '', hasPortalAccess: false },
})

function reset(): void {
  const source = props.mode === 'edit' ? props.customer : undefined
  form.displayNameEn = source?.displayName?.en ?? ''
  form.displayNameAr = source?.displayName?.ar ?? ''
  form.type = source?.type === 'Organisation' ? 'Organisation' : 'Individual'
  form.legalName = source?.legalName ?? ''
  form.preferredLanguage = source?.preferredLanguage === 'ar' || source?.preferredLanguage === 'en' ? source.preferredLanguage : uiLanguage()
  form.departmentId = ''
  form.contact = { name: '', jobTitle: '', email: '', phone: '', hasPortalAccess: false }
  formErrors.clear()
  duplicates.value = []
}

watch(open, (value) => {
  if (value) reset()
})
// Editing any field hides the duplicate warning; the next submit re-checks.
watch(form, () => {
  duplicates.value = []
}, { deep: true })

const typeOptions = computed(() => customerTypes.map((value) => ({ value, label: t(`customers.type.${value}`) })))
const languageOptions = computed(() => [
  { value: 'en', label: t('customers.language.en') },
  { value: 'ar', label: t('customers.language.ar') },
])

// The department select is only needed when the server cannot pick one: scope "all" and several departments.
const showDepartment = computed(
  () => (scope.myScope?.activeDepartmentId ?? null) === null && scope.activeDepartments.length > 1,
)
const departmentOptions = computed(() =>
  scope.activeDepartments.flatMap((department) =>
    department.departmentId === undefined
      ? []
      : [
          {
            value: department.departmentId,
            label: t('organisation.scope.option', {
              department: localizedName(department.departmentName, locale.value),
              branch: localizedName(department.branchName, locale.value),
            }),
          },
        ],
  ),
)

function blankToNull(value: string): string | null {
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

function candidateStatus(value: string | null | undefined): string {
  return value === 'Active' || value === 'Inactive' ? t(`customers.status.${value}`) : ''
}

function matchedValues(candidate: DuplicateCandidateDto): string {
  const separator = locale.value === 'ar' ? '، ' : ', '
  return [...(candidate.matchedEmails ?? []), ...(candidate.matchedPhones ?? [])].map(isolateLtr).join(separator)
}

function profileBody() {
  return {
    displayNameEn: blankToNull(form.displayNameEn),
    displayNameAr: blankToNull(form.displayNameAr),
    type: form.type,
    legalName: form.type === 'Organisation' ? blankToNull(form.legalName) : null,
    preferredLanguage: form.preferredLanguage,
  }
}

function createBody(confirmDuplicate: boolean): CreateCustomerRequest {
  const c = form.contact
  const hasContact = [c.name, c.jobTitle, c.email, c.phone].some((value) => value.trim() !== '') || c.hasPortalAccess
  return {
    ...profileBody(),
    contacts: hasContact
      ? [
          {
            name: c.name.trim(),
            jobTitle: blankToNull(c.jobTitle),
            email: blankToNull(c.email),
            phone: blankToNull(c.phone),
            isPrimary: true,
            hasPortalAccess: c.hasPortalAccess,
          },
        ]
      : [],
    confirmDuplicate,
    departmentId: showDepartment.value && form.departmentId !== '' ? form.departmentId : null,
  }
}

async function submit(confirmDuplicate = false): Promise<void> {
  if (submitting.value) return
  submitting.value = true
  formErrors.clear()

  if (props.mode === 'create') {
    const result = await commands.create(createBody(confirmDuplicate))
    submitting.value = false
    if (result.ok) {
      const outcome = result.data
      if (outcome.created && outcome.customer) {
        toasts.push({ tone: 'success', title: t('customers.form.created') })
        open.value = false
        emit('created', outcome.customer)
      } else {
        duplicates.value = outcome.duplicateCandidates ?? []
      }
      return
    }
    if (formErrors.applyCode(result.problem, createFieldForCode)) return
    if (formErrors.apply(result.problem)) return
    commands.toast(result.problem)
    return
  }

  const current = props.customer
  if (current?.id === undefined) {
    submitting.value = false
    return
  }
  // Read at submit time: a reload after a conflict (or a contact command) may have moved it.
  const body: UpdateCustomerRequest = { ...profileBody(), rowVersion: current.rowVersion ?? '' }
  const result = await commands.update(current.id, body)
  submitting.value = false
  if (result.ok) {
    toasts.push({ tone: 'success', title: t('customers.form.saved') })
    open.value = false
    emit('saved', result.data)
    return
  }
  if (result.problem.type === ProblemTypes.Conflict) {
    commands.toast(result.problem)
    emit('conflict')
    return
  }
  if (formErrors.applyCode(result.problem, profileFieldForCode)) return
  if (formErrors.apply(result.problem)) return
  commands.toast(result.problem)
}
</script>
