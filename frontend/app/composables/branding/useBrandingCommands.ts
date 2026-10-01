import type { ChangeBrandingRequest } from './types'

/** Every command returns an ApiResult and never toasts: the page shows a refusal inline, next to the field it concerns. */
export function useBrandingCommands() {
  const { clients, call } = useApi()
  const quiet = { toastOnError: false }

  return {
    // The first multipart call: the body is rebuilt as FormData, so openapi-fetch sends no JSON content type and
    // the browser adds the multipart boundary itself.
    uploadLogo: (file: File) =>
      call(
        clients.admin.POST('/api/Admin/Branding/logos', {
          body: { file },
          bodySerializer: (body) => {
            const form = new FormData()
            if (body?.file) form.append('file', body.file)
            return form
          },
        }),
        quiet,
      ),
    save: (body: ChangeBrandingRequest) => call(clients.admin.PUT('/api/Admin/Branding', { body }), quiet),
    revert: (rowVersion: string) => call(clients.admin.POST('/api/Admin/Branding/revert', { body: { rowVersion } }), quiet),
  }
}
