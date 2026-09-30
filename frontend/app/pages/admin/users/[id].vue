<template>
  <div>
    <NuxtLink to="/admin/users" class="text-sm text-primary-600 underline">{{ t('identity.users.back') }}</NuxtLink>

    <div v-if="status === 'pending' && !user" class="mt-6 flex flex-col gap-4" aria-busy="true">
      <UiSkeleton variant="text" />
      <UiSkeleton variant="rect" />
      <UiSkeleton variant="rect" />
    </div>
    <UiEmptyState v-else-if="notFound" class="mt-6" :title="t('identity.users.notFound')" />
    <UiErrorState
      v-else-if="forbidden"
      class="mt-6"
      :title="t('errors.problem.forbidden')"
      :description="forbiddenDetail"
    />
    <UiErrorState
      v-else-if="error || !user"
      class="mt-6"
      :title="t('identity.load.error')"
      :retry-label="t('identity.load.retry')"
      @retry="refresh()"
    />
    <template v-else>
      <div class="mt-4 flex flex-wrap items-center gap-3">
        <h1 class="text-2xl font-bold text-fg">{{ user.displayName }}</h1>
        <UiBadge :tone="user.isActive ? 'success' : 'neutral'">
          {{ user.isActive ? t('identity.status.active') : t('identity.status.inactive') }}
        </UiBadge>
        <UiBadge v-if="user.isLockedOut" tone="warning">{{ t('identity.status.locked') }}</UiBadge>
      </div>
      <p class="mt-1 text-sm text-fg-muted"><bdi dir="ltr">{{ user.email }}</bdi></p>

      <div class="mt-6 grid gap-6 lg:grid-cols-2">
        <section class="rounded-md border border-border p-4" :aria-labelledby="profileHeadingId">
          <h2 :id="profileHeadingId" class="text-lg font-semibold text-fg">{{ t('identity.detail.profile') }}</h2>
          <form class="mt-4 flex flex-col gap-3" novalidate @submit.prevent="saveProfile()">
            <UiInput
              v-model="profile.displayName"
              :label="t('identity.fields.displayName')"
              :error="profileErrors.errorFor('displayName')"
              required
            />
            <UiSelect
              v-model="profile.preferredLocale"
              :label="t('identity.fields.locale')"
              :options="localeOptions"
              :error="profileErrors.errorFor('preferredLocale')"
              required
            />
            <div>
              <UiButton type="submit" :loading="busy === 'profile'">{{ t('identity.actions.save') }}</UiButton>
            </div>
          </form>
        </section>

        <section class="rounded-md border border-border p-4" :aria-labelledby="statusHeadingId">
          <h2 :id="statusHeadingId" class="text-lg font-semibold text-fg">{{ t('identity.detail.status') }}</h2>
          <dl class="mt-4 grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-sm">
            <dt class="text-fg-muted">{{ t('identity.fields.lastSignIn') }}</dt>
            <dd class="text-fg">
              {{ user.lastSignInAtUtc ? format.dateTime(user.lastSignInAtUtc) : t('identity.fields.never') }}
            </dd>
          </dl>
          <div class="mt-4 flex flex-wrap gap-2">
            <UiButton v-if="!user.isActive" variant="secondary" :loading="busy === 'status'" @click="reactivate()">
              {{ t('identity.actions.reactivate') }}
            </UiButton>
            <UiButton v-else-if="!isSelf" variant="danger" @click="deactivateOpen = true">
              {{ t('identity.actions.deactivate') }}
            </UiButton>
            <UiButton variant="secondary" @click="openReset()">{{ t('identity.actions.resetPassword') }}</UiButton>
          </div>
        </section>
      </div>

      <section
        v-if="can(Permissions.AdminOrganisation)"
        class="mt-6 rounded-md border border-border p-4"
        :aria-labelledby="membershipsHeadingId"
      >
        <h2 :id="membershipsHeadingId" class="text-lg font-semibold text-fg">{{ t('identity.detail.memberships') }}</h2>
        <UiTable
          class="mt-4"
          :caption="t('identity.detail.memberships')"
          :columns="membershipColumns"
          :rows="memberships"
          :row-key="(row) => row.id ?? ''"
          :loading="membershipsStatus === 'pending' && memberships.length === 0"
        >
          <template #empty>
            <UiEmptyState :title="t('identity.detail.noMemberships')" :heading-level="3" />
          </template>
          <template #cell-department="{ row }">{{ departmentLabel(row.departmentId) }}</template>
          <template #cell-role="{ row }">
            <select
              class="rounded-md border border-border bg-surface py-1 ps-2 pe-6 text-sm text-fg ui-focus:focus-ring"
              :aria-label="t('identity.fields.role')"
              :value="row.role"
              @change="changeRole(row, ($event.target as HTMLSelectElement).value)"
            >
              <option v-for="role in membershipRoles" :key="role" :value="role">{{ t(`identity.roles.${role}`) }}</option>
            </select>
          </template>
          <template #cell-actions="{ row }">
            <UiButton variant="ghost" size="sm" @click="removeTarget = row">{{ t('identity.actions.remove') }}</UiButton>
          </template>
        </UiTable>

        <form class="mt-4 flex flex-wrap items-end gap-3" @submit.prevent="addMembership()">
          <UiSelect
            v-model="newMembership.departmentId"
            :label="t('identity.fields.department')"
            :options="departmentOptions"
            :placeholder="t('identity.fields.department')"
            class="min-w-48"
          />
          <UiSelect
            v-model="newMembership.role"
            :label="t('identity.fields.role')"
            :options="roleOptions"
            class="min-w-40"
          />
          <UiButton type="submit" :disabled="newMembership.departmentId === ''" :loading="busy === 'membership'">
            {{ t('identity.actions.addMembership') }}
          </UiButton>
        </form>
      </section>
    </template>

    <UiModal
      v-model:open="deactivateOpen"
      :title="t('identity.actions.deactivate')"
      :close-label="t('identity.actions.close')"
      size="sm"
    >
      <p class="text-sm text-fg">{{ t('identity.confirm.deactivate', { name: user?.displayName ?? '' }) }}</p>
      <template #footer>
        <UiButton variant="secondary" @click="deactivateOpen = false">{{ t('identity.actions.cancel') }}</UiButton>
        <UiButton variant="danger" :loading="busy === 'status'" @click="deactivate()">
          {{ t('identity.actions.deactivate') }}
        </UiButton>
      </template>
    </UiModal>

    <UiModal
      v-model:open="resetOpen"
      :title="t('identity.actions.resetPassword')"
      :close-label="t('identity.actions.close')"
      size="sm"
    >
      <form class="flex flex-col gap-3" novalidate @submit.prevent="resetPassword()">
        <UiInput
          v-model="resetForm.newPassword"
          type="password"
          dir="ltr"
          autocomplete="new-password"
          :label="t('identity.fields.newPassword')"
          :hint="t('identity.account.passwordRules')"
          :error="resetErrors.errorFor('newPassword')"
          required
        />
        <button type="submit" class="hidden" tabindex="-1" aria-hidden="true" />
      </form>
      <template #footer>
        <UiButton variant="secondary" @click="resetOpen = false">{{ t('identity.actions.cancel') }}</UiButton>
        <UiButton :loading="busy === 'reset'" @click="resetPassword()">{{ t('identity.actions.resetPassword') }}</UiButton>
      </template>
    </UiModal>

    <UiModal
      :open="removeTarget !== null"
      :title="t('identity.actions.remove')"
      :close-label="t('identity.actions.close')"
      size="sm"
      @update:open="(open) => { if (!open) removeTarget = null }"
    >
      <p class="text-sm text-fg">
        {{
          t('identity.confirm.removeMembership', {
            name: user?.displayName ?? '',
            department: removeTarget ? departmentLabel(removeTarget.departmentId) : '',
          })
        }}
      </p>
      <template #footer>
        <UiButton variant="secondary" @click="removeTarget = null">{{ t('identity.actions.cancel') }}</UiButton>
        <UiButton variant="danger" :loading="busy === 'membership'" @click="confirmRemove()">
          {{ t('identity.actions.remove') }}
        </UiButton>
      </template>
    </UiModal>
  </div>
</template>

<script setup lang="ts">
import type { MembershipDto } from '~/composables/useMemberships'
import { membershipRoles } from '~/composables/useMemberships'
import { ProblemTypes, problemOf } from '~/api/problem'
import { localizedName } from '~/utils/scope'
import { Permissions } from '~/utils/permissions'

definePageMeta({ layout: 'agent' })

const { t, locale } = useI18n()
const route = useRoute()
const auth = useAuthStore()
const { can } = useCan()
const format = useFormat()
const users = useUsers()
const membershipsApi = useMemberships()

const profileHeadingId = useId()
const statusHeadingId = useId()
const membershipsHeadingId = useId()

const id = computed(() => String(route.params.id))
const { data: user, status, error, refresh } = users.get(id)

const notFound = computed(() => problemOf(error.value)?.type === ProblemTypes.NotFound)
const forbidden = computed(() => problemOf(error.value)?.type === ProblemTypes.Forbidden)
const forbiddenDetail = computed(() => (problemOf(error.value)?.detail ?? undefined))
const isSelf = computed(() => auth.me?.id !== undefined && auth.me.id === user.value?.id)

const localeOptions = computed(() => [
  { value: 'en', label: t('identity.locale.en') },
  { value: 'ar', label: t('identity.locale.ar') },
])

const busy = ref<'profile' | 'status' | 'reset' | 'membership' | null>(null)

// Profile ---------------------------------------------------------------------------------------------------------
const profile = reactive({ displayName: '', preferredLocale: 'en' })
const profileErrors = useFormErrors()

watch(
  user,
  (value) => {
    if (!value) return
    profile.displayName = value.displayName ?? ''
    profile.preferredLocale = value.preferredLocale === 'ar' ? 'ar' : 'en'
  },
  { immediate: true },
)

/** Every write carries the row version the page last read; a 409 means someone else changed the user. */
async function afterWrite(ok: boolean, problemType?: string): Promise<void> {
  // A concurrency conflict already raised the shared toast; reload so the next attempt uses the new row version.
  if (ok || problemType === ProblemTypes.Conflict) await refresh()
}

async function saveProfile(): Promise<void> {
  if (!user.value || busy.value !== null) return
  busy.value = 'profile'
  profileErrors.clear()
  const result = await users.updateProfile(id.value, {
    displayName: profile.displayName,
    preferredLocale: profile.preferredLocale,
    rowVersion: user.value.rowVersion ?? '',
  })
  busy.value = null
  if (result.ok) {
    useToasts().push({ tone: 'success', title: t('identity.saved') })
    await afterWrite(true)
    return
  }
  profileErrors.apply(result.problem)
  await afterWrite(false, result.problem.type)
}

// Account status --------------------------------------------------------------------------------------------------
const deactivateOpen = ref(false)

async function deactivate(): Promise<void> {
  if (!user.value || busy.value !== null) return
  busy.value = 'status'
  const result = await users.deactivate(id.value, user.value.rowVersion ?? '')
  busy.value = null
  deactivateOpen.value = false
  await afterWrite(result.ok, result.ok ? undefined : result.problem.type)
}

async function reactivate(): Promise<void> {
  if (!user.value || busy.value !== null) return
  busy.value = 'status'
  const result = await users.reactivate(id.value, user.value.rowVersion ?? '')
  busy.value = null
  await afterWrite(result.ok, result.ok ? undefined : result.problem.type)
}

const resetOpen = ref(false)
const resetForm = reactive({ newPassword: '' })
const resetErrors = useFormErrors()

function openReset(): void {
  resetForm.newPassword = ''
  resetErrors.clear()
  resetOpen.value = true
}

async function resetPassword(): Promise<void> {
  if (!user.value || busy.value !== null) return
  busy.value = 'reset'
  resetErrors.clear()
  const result = await users.resetPassword(id.value, {
    newPassword: resetForm.newPassword,
    rowVersion: user.value.rowVersion ?? '',
  })
  busy.value = null
  if (result.ok) {
    resetOpen.value = false
    useToasts().push({ tone: 'success', title: t('identity.saved') })
    await afterWrite(true)
    return
  }
  resetErrors.apply(result.problem)
  await afterWrite(false, result.problem.type)
}

// Memberships (Organisation's endpoints; shown only with admin.organisation) ----------------------------------------
const canOrganisation = can(Permissions.AdminOrganisation)
const membershipsRef = canOrganisation
  ? membershipsApi.listForUser(id)
  : { data: ref<MembershipDto[] | undefined>(undefined), status: ref('idle'), refresh: async () => {} }
const memberships = computed(() => membershipsRef.data.value ?? [])
const membershipsStatus = membershipsRef.status

const departmentsRef = canOrganisation ? useDepartments().list(ref(undefined), ref(false)) : null
const departments = computed(() => departmentsRef?.data.value ?? [])

function departmentLabel(departmentId: string | undefined): string {
  const department = departments.value.find((d) => d.id === departmentId)
  return department ? localizedName(department.name, locale.value) : (departmentId ?? '')
}

const membershipColumns = computed(() => [
  { key: 'department', label: t('identity.fields.department') },
  { key: 'role', label: t('identity.fields.role') },
  { key: 'actions', label: t('identity.fields.actions'), align: 'end' as const },
])

const roleOptions = computed(() => membershipRoles.map((role) => ({ value: role, label: t(`identity.roles.${role}`) })))

const departmentOptions = computed(() => {
  const joined = new Set(memberships.value.map((m) => m.departmentId))
  return departments.value
    .filter((d) => d.isActive && !joined.has(d.id))
    .map((d) => ({ value: d.id ?? '', label: localizedName(d.name, locale.value) }))
})

const newMembership = reactive({ departmentId: '', role: 'Agent' as string })

async function addMembership(): Promise<void> {
  if (busy.value !== null || newMembership.departmentId === '') return
  busy.value = 'membership'
  const result = await membershipsApi.add({
    userId: id.value,
    departmentId: newMembership.departmentId,
    role: newMembership.role,
  })
  busy.value = null
  if (result.ok) newMembership.departmentId = ''
  await membershipsRef.refresh()
}

async function changeRole(row: MembershipDto, role: string): Promise<void> {
  if (role === row.role) return
  const result = await membershipsApi.changeRole(row.id ?? '', { role, rowVersion: row.rowVersion ?? '' })
  // Reload either way: on success for the new row version, on failure to put the select back.
  await membershipsRef.refresh()
  if (result.ok) useToasts().push({ tone: 'success', title: t('identity.saved') })
}

const removeTarget = ref<MembershipDto | null>(null)

async function confirmRemove(): Promise<void> {
  const target = removeTarget.value
  if (!target) return
  busy.value = 'membership'
  await membershipsApi.remove(target.id ?? '')
  busy.value = null
  removeTarget.value = null
  await membershipsRef.refresh()
}
</script>
