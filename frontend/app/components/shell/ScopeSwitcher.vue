<template>
  <UiSpinner v-if="scope.status === 'loading'" size="sm" />
  <UiButton v-else-if="scope.status === 'error'" variant="ghost" size="sm" @click="reload()">
    {{ t('organisation.load.retry') }}
  </UiButton>
  <UiSelect
    v-else-if="scope.status === 'ready' && options.length > 1"
    v-model="active"
    :label="t('organisation.scope.label')"
    :options="optionsWithAll"
    class="w-32 min-w-0 sm:w-auto sm:min-w-48"
  />
</template>

<script setup lang="ts">
import { allDepartments, localizedName } from '~/utils/scope'

const { t, locale } = useI18n()
const { scope, reload } = useMyScope()

const options = computed(() =>
  scope.activeDepartments.map((department) => ({
    value: department.departmentId ?? '',
    label: t('organisation.scope.option', {
      department: localizedName(department.departmentName, locale.value),
      branch: localizedName(department.branchName, locale.value),
    }),
  })),
)

const allOption = computed(() => ({ value: allDepartments, label: t('organisation.scope.all') }))

const optionsWithAll = computed(() => [allOption.value, ...options.value])

const active = computed<string>({
  get: () => scope.myScope?.activeDepartmentId ?? allDepartments,
  set: (value) => scope.setActive(value),
})
</script>
