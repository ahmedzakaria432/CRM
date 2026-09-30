<template>
  <UiDrawer v-model:open="open" size="lg" :title="t('tickets.create.title')" :close-label="t('tickets.create.close')">
    <form class="flex flex-col gap-3" novalidate @submit.prevent="submit()">
      <CustomerPicker
        v-model="form.customer"
        :label="t('tickets.create.customer')"
        :hint="t('tickets.create.customerHint')"
        :error="formErrors.errorFor('customerId')"
        required
      />
      <div>
        <UiSelect
          v-model="form.contactId"
          :label="t('tickets.create.contact')"
          :options="contactOptions"
          :disabled="form.customer === null"
          :error="formErrors.errorFor('contactId')"
        />
        <p v-if="contactsFailed" role="alert" class="mt-1 text-xs text-danger-600">{{ t('tickets.create.contactsError') }}</p>
      </div>
      <UiSelect
        v-if="showDepartment"
        v-model="form.departmentId"
        :label="t('tickets.create.department')"
        :placeholder="t('tickets.create.department')"
        :options="departmentOptions"
        :error="formErrors.errorFor('departmentId')"
        required
      />
      <div>
        <UiSelect
          v-model="form.categoryId"
          :label="t('tickets.create.category')"
          :options="categoryOptions"
          :error="formErrors.errorFor('categoryId')"
        />
        <p v-if="categoriesFailed" role="alert" class="mt-1 text-xs text-danger-600">{{ t('tickets.create.categoriesError') }}</p>
      </div>
      <UiInput
        v-model="form.subject"
        maxlength="200"
        :label="t('tickets.create.subject')"
        :error="formErrors.errorFor('subject')"
        required
      />
      <UiTextarea
        v-model="form.description"
        :rows="5"
        maxlength="4000"
        :label="t('tickets.create.description')"
        :error="formErrors.errorFor('description')"
        required
      />
      <UiSelect
        v-model="form.priority"
        :label="t('tickets.create.priority')"
        :options="priorityOptions"
        :error="formErrors.errorFor('priority')"
      />
      <p v-if="formErrors.formError.value" role="alert" class="text-sm text-danger-600">
        {{ formErrors.formError.value }}
      </p>
      <button type="submit" class="hidden" tabindex="-1" aria-hidden="true" />
    </form>
    <template #footer>
      <UiButton variant="secondary" @click="open = false">{{ t('tickets.create.cancel') }}</UiButton>
      <UiButton :loading="submitting" @click="submit()">{{ t('tickets.create.submit') }}</UiButton>
    </template>
  </UiDrawer>
</template>

<script setup lang="ts">
import { useCustomerLookup } from '~/composables/customers/useCustomerLookup'
import type { ContactDto, CustomerLookupDto } from '~/composables/customers/types'
import { useTicketCommands } from '~/composables/tickets/useTicketCommands'
import type { AgentTicketCategoryDto, CreateTicketRequest, TicketDto } from '~/composables/tickets/types'
import { localizedName } from '~/utils/scope'
import { createTicketFieldForCode, ticketPriorities } from '~/utils/tickets'

interface Props {
  customer?: CustomerLookupDto | null
}
const props = withDefaults(defineProps<Props>(), { customer: null })
const emit = defineEmits<{ created: [TicketDto] }>()
const open = defineModel<boolean>('open', { default: false })

interface CreateForm {
  customer: CustomerLookupDto | null
  contactId: string
  departmentId: string
  categoryId: string
  subject: string
  description: string
  priority: string
}

const { t, locale } = useI18n()
const scope = useScopeStore()
const toasts = useToasts()
const api = useApi()
const commands = useTicketCommands()
const lookup = useCustomerLookup()
const formErrors = useFormErrors()

const submitting = ref(false)
const contacts = ref<ContactDto[]>([])
const contactsFailed = ref(false)
const categories = ref<AgentTicketCategoryDto[]>([])
const categoriesFailed = ref(false)

const form = reactive<CreateForm>({
  customer: null,
  contactId: '',
  departmentId: '',
  categoryId: '',
  subject: '',
  description: '',
  priority: 'Normal',
})

function reset(): void {
  form.customer = props.customer
  form.contactId = ''
  form.departmentId = ''
  form.categoryId = ''
  form.subject = ''
  form.description = ''
  form.priority = 'Normal'
  contacts.value = []
  contactsFailed.value = false
  formErrors.clear()
}

watch(open, (value) => {
  if (value) reset()
})

// D12: the department is always sent explicitly, so the category list always matches the ticket's department.
const showDepartment = computed(
  () => (scope.myScope?.activeDepartmentId ?? null) === null && scope.activeDepartments.length > 1,
)
const targetDepartmentId = computed(() =>
  showDepartment.value
    ? form.departmentId
    : (scope.myScope?.activeDepartmentId ?? scope.activeDepartments[0]?.departmentId ?? ''),
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

const priorityOptions = computed(() => ticketPriorities.map((value) => ({ value, label: t(`tickets.priority.${value}`) })))

// Choosing a customer answers the "Choose a customer first" error.
watch(
  () => form.customer,
  (customer) => {
    if (customer !== null) formErrors.clear('customerId')
  },
)

// --- contacts of the chosen customer (default: the primary one) ---
let contactsController: AbortController | null = null
watch(
  () => form.customer?.id,
  async (customerId) => {
    contactsController?.abort()
    contacts.value = []
    contactsFailed.value = false
    form.contactId = ''
    if (customerId === undefined) return
    const controller = new AbortController()
    contactsController = controller
    const result = await lookup.get(customerId, controller.signal)
    if (controller.signal.aborted) return
    if (!result.ok) {
      contactsFailed.value = true
      return
    }
    contacts.value = result.data.contacts ?? []
    form.contactId = contacts.value.find((contact) => contact.isPrimary)?.id ?? ''
  },
)
onScopeDispose(() => contactsController?.abort())

const contactOptions = computed(() => [
  { value: '', label: t('tickets.create.noContact') },
  ...contacts.value.flatMap((contact) =>
    contact.id === undefined
      ? []
      : [{ value: contact.id, label: [contact.name, contact.email || contact.phone].filter(Boolean).join(' · ') }],
  ),
])

// --- categories of the target department ---
let categoriesSequence = 0
watch(
  targetDepartmentId,
  async (departmentId) => {
    form.categoryId = ''
    categories.value = []
    categoriesFailed.value = false
    if (departmentId === '') return
    const sequence = (categoriesSequence += 1)
    const result = await api.call(
      api.clients.agent.GET('/api/Agent/TicketCategories', { params: { query: { departmentId } } }),
      { toastOnError: false },
    )
    if (sequence !== categoriesSequence) return
    if (!result.ok) {
      categoriesFailed.value = true
      return
    }
    categories.value = result.data
  },
  { immediate: true },
)
const categoryOptions = computed(() => [
  { value: '', label: t('tickets.create.noCategory') },
  ...categories.value.flatMap((category) =>
    category.id === undefined ? [] : [{ value: category.id, label: localizedName(category.name, locale.value) }],
  ),
])

async function submit(): Promise<void> {
  if (submitting.value) return
  formErrors.clear()
  const customer = form.customer
  if (customer === null) {
    formErrors.fieldErrors.value = { customerId: [t('tickets.create.chooseCustomerFirst')] }
    return
  }
  submitting.value = true
  const body: CreateTicketRequest = {
    customerId: customer.id,
    contactId: form.contactId || null,
    subject: form.subject.trim(),
    description: form.description.trim(),
    priority: form.priority,
    categoryId: form.categoryId || null,
    departmentId: targetDepartmentId.value || null,
  }
  const result = await commands.create(body)
  submitting.value = false
  if (result.ok) {
    toasts.push({ tone: 'success', title: t('tickets.create.created', { number: result.data.number ?? '' }) })
    open.value = false
    emit('created', result.data)
    return
  }
  if (formErrors.applyCode(result.problem, createTicketFieldForCode)) return
  if (formErrors.apply(result.problem)) return
  api.toast(result.problem)
}
</script>
