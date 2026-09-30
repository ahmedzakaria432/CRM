<template>
  <section :aria-labelledby="headingId" class="rounded-md border border-border bg-surface p-4">
    <h2 :id="headingId" class="sr-only">{{ t('customers.detail.headerLabel') }}</h2>

    <UiSkeleton v-if="status === 'pending' && !data" :lines="4" />
    <UiEmptyState
      v-else-if="notFound"
      :title="t('customers.detail.notFound')"
      :description="t('customers.detail.notFoundDescription')"
    >
      <template #actions>
        <NuxtLink to="/agent/customers" class="text-sm text-primary-600 underline">
          {{ t('customers.detail.back') }}
        </NuxtLink>
      </template>
    </UiEmptyState>
    <UiErrorState
      v-else-if="forbidden"
      :title="t('errors.problem.forbidden')"
      :description="t('errors.code.Organisation.OutOfScope')"
    />
    <UiErrorState
      v-else-if="error && !data"
      :title="t('customers.detail.loadError')"
      :retry-label="t('customers.detail.retry')"
      @retry="refresh()"
    />
    <template v-else-if="data">
      <div class="flex flex-wrap items-start justify-between gap-3">
        <div class="min-w-0">
          <h1 class="break-words text-2xl font-bold text-fg">{{ localizedName(data.displayName, locale) }}</h1>
          <p v-if="otherName" class="break-words text-sm text-fg-muted" :lang="otherName.lang" :dir="otherName.dir">
            {{ otherName.text }}
          </p>
          <div class="mt-2 flex flex-wrap items-center gap-2">
            <bdi dir="ltr" class="font-mono text-sm text-fg-muted">{{ data.number }}</bdi>
            <UiBadge>{{ typeLabel }}</UiBadge>
            <UiBadge :tone="statusTone(data.status)">{{ statusText }}</UiBadge>
          </div>
        </div>
        <div v-if="canWrite" class="flex flex-wrap gap-2">
          <UiButton variant="secondary" @click="editOpen = true">{{ t('customers.detail.edit') }}</UiButton>
          <UiButton v-if="data.status === 'Active'" variant="secondary" @click="confirmOpen = true">
            {{ t('customers.detail.deactivate') }}
          </UiButton>
          <UiButton v-else :loading="changing" variant="secondary" @click="changeStatus(false)">
            {{ t('customers.detail.reactivate') }}
          </UiButton>
        </div>
      </div>

      <dl class="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <div class="min-w-0">
          <dt class="text-xs text-fg-muted">{{ t('customers.fields.department') }}</dt>
          <dd class="break-words text-sm text-fg">{{ departmentText }}</dd>
        </div>
        <div v-if="data.type === 'Organisation'" class="min-w-0">
          <dt class="text-xs text-fg-muted">{{ t('customers.fields.legalName') }}</dt>
          <dd class="break-words text-sm text-fg">{{ data.legalName }}</dd>
        </div>
        <div class="min-w-0">
          <dt class="text-xs text-fg-muted">{{ t('customers.fields.preferredLanguage') }}</dt>
          <dd class="text-sm text-fg">{{ languageText }}</dd>
        </div>
        <div class="min-w-0">
          <dt class="text-xs text-fg-muted">{{ t('customers.fields.createdAt') }}</dt>
          <dd class="text-sm text-fg">{{ data.createdAtUtc ? format.date(data.createdAtUtc) : '' }}</dd>
        </div>
      </dl>

      <CustomerFormDrawer
        v-if="canWrite"
        v-model:open="editOpen"
        mode="edit"
        :customer="data"
        @saved="customer.publish"
        @conflict="customer.reload()"
      />
      <UiModal
        v-if="canWrite"
        v-model:open="confirmOpen"
        :title="t('customers.detail.deactivateTitle')"
        :close-label="t('customers.form.close')"
      >
        <p class="text-sm text-fg">{{ t('customers.detail.deactivateDescription') }}</p>
        <template #footer>
          <UiButton variant="secondary" @click="confirmOpen = false">{{ t('customers.detail.cancel') }}</UiButton>
          <UiButton variant="danger" :loading="changing" @click="changeStatus(true)">
            {{ t('customers.detail.confirm') }}
          </UiButton>
        </template>
      </UiModal>
    </template>
  </section>
</template>

<script setup lang="ts">
import { useCustomer } from '~/composables/customers/useCustomer'
import { useCustomerCommands } from '~/composables/customers/useCustomerCommands'
import { ProblemTypes, problemOf } from '~/api/problem'
import { Permissions } from '~/utils/permissions'
import { localizedName } from '~/utils/scope'
import { statusTone } from '~/utils/customers'

interface Props {
  customerId: string
}
const props = defineProps<Props>()

const { t, locale } = useI18n()
const { can } = useCan()
const format = useFormat()
const scope = useScopeStore()
const toasts = useToasts()
const commands = useCustomerCommands()
const customer = useCustomer(props.customerId)
const { data, status, error, refresh } = customer.load('profile')
const headingId = useId()

const canWrite = computed(() => can(Permissions.CustomersWrite))
const editOpen = ref(false)
const confirmOpen = ref(false)
const changing = ref(false)

const notFound = computed(() => problemOf(error.value)?.type === ProblemTypes.NotFound)
const forbidden = computed(() => problemOf(error.value)?.type === ProblemTypes.Forbidden)

const otherName = computed(() => {
  const name = data.value?.displayName
  if (!name?.ar || !name.en || name.ar === name.en) return undefined
  return locale.value === 'ar'
    ? ({ text: name.en, lang: 'en', dir: 'ltr' } as const)
    : ({ text: name.ar, lang: 'ar', dir: 'rtl' } as const)
})

const typeLabel = computed(() => {
  const type = data.value?.type
  return type === 'Individual' || type === 'Organisation' ? t(`customers.type.${type}`) : ''
})
const statusText = computed(() => {
  const value = data.value?.status
  return value === 'Active' || value === 'Inactive' ? t(`customers.status.${value}`) : ''
})
const languageText = computed(() => {
  const value = data.value?.preferredLanguage
  return value === 'ar' || value === 'en' ? t(`customers.language.${value}`) : '—'
})
const departmentText = computed(() => {
  const department = scope.myScope?.departments?.find((entry) => entry.departmentId === data.value?.departmentId)
  if (department === undefined) return '—'
  return t('organisation.scope.option', {
    department: localizedName(department.departmentName, locale.value),
    branch: localizedName(department.branchName, locale.value),
  })
})

async function changeStatus(deactivate: boolean): Promise<void> {
  const current = data.value
  if (changing.value || current === undefined) return
  changing.value = true
  const rowVersion = current.rowVersion ?? ''
  const result = deactivate
    ? await commands.deactivate(props.customerId, rowVersion)
    : await commands.reactivate(props.customerId, rowVersion)
  changing.value = false
  confirmOpen.value = false
  if (result.ok) {
    customer.publish(result.data)
    toasts.push({ tone: 'success', title: t(deactivate ? 'customers.detail.deactivated' : 'customers.detail.reactivated') })
    return
  }
  if (result.problem.type === ProblemTypes.Conflict) await customer.reload()
}
</script>
