import { z } from 'zod'

/** Mirrors backend Identifiers.Validate (^[a-zA-Z0-9-]+$, <= 63 chars). */
export const identifierSchema = z
  .string()
  .min(1, 'Identifier is required.')
  .max(63, 'Identifier must be at most 63 characters.')
  .regex(/^[a-zA-Z0-9-]+$/, 'Only letters, digits, and hyphens (a-z, A-Z, 0-9, -).')

export const vpnTypeSchema = z.enum(['openvpn', 'wireguard'])

/** Extracts a flat {field: message} map from a Zod result for inline field errors. */
export function zodErrors(result: z.ZodError): Record<string, string> {
  const out: Record<string, string> = {}
  for (const issue of result.issues) {
    const key = issue.path.join('.') || '_'
    if (!out[key]) out[key] = issue.message
  }
  return out
}

export const providerSchema = z.object({
  name: z.string().trim().min(1, 'Name is required.'),
  providerType: z.string().trim().min(1, 'Provider type is required.'),
  vpnType: vpnTypeSchema,
})

export const customVpnSchema = (requireConfig: boolean) =>
  z.object({
    name: z.string().trim().min(1, 'Name is required.'),
    vpnType: vpnTypeSchema,
    rawConfig: requireConfig
      ? z.string().trim().min(1, 'Config content (upload a file or paste text) is required.')
      : z.string().nullable().optional(),
  })

export const settingsSchema = z
  .object({
    timezone: z.string().trim().min(1, 'Timezone is required.'),
    gluetunImage: z.string().trim().min(1, 'Gluetun image is required.'),
    socks5Image: z.string().trim().min(1, 'SOCKS5 image is required.'),
    socks5BalancerImage: z.string().trim().min(1, 'Balancer image is required.'),
    portRangeStart: z.number().int().min(1024, 'Start must be ≥ 1024.').max(65535),
    portRangeEnd: z.number().int().min(1024).max(65535, 'End must be ≤ 65535.'),
    balancerPortRangeStart: z.number().int().min(1024, 'Start must be ≥ 1024.').max(65535),
    balancerPortRangeEnd: z.number().int().min(1024).max(65535, 'End must be ≤ 65535.'),
  })
  .refine((v) => v.portRangeEnd - v.portRangeStart >= 2, {
    message: 'Connection port range must span at least 3 ports.',
    path: ['portRangeEnd'],
  })
  .refine((v) => v.balancerPortRangeEnd - v.balancerPortRangeStart >= 2, {
    message: 'Balancer port range must span at least 3 ports.',
    path: ['balancerPortRangeEnd'],
  })

export function connectionValidate(form: {
  identifier: string
  sourceType: string
  providerId: number | null
  customVpnConfigId: number | null
  enableShadowsocks?: boolean
  shadowsocksPassword?: string | null
  /** True when editing a connection that already has a stored password (blank = keep it). */
  hasShadowsocksPassword?: boolean
}): Record<string, string> {
  const errors: Record<string, string> = {}
  const id = identifierSchema.safeParse(form.identifier)
  if (!id.success) errors.identifier = id.error.issues[0].message
  if (form.sourceType === 'provider' && !form.providerId)
    errors.providerId = 'Select a provider.'
  if (form.sourceType === 'custom' && !form.customVpnConfigId)
    errors.customVpnConfigId = 'Select a custom config.'
  // Shadowsocks has no anonymous mode — Gluetun will not start the server without a password.
  if (form.enableShadowsocks && !form.shadowsocksPassword && !form.hasShadowsocksPassword)
    errors.shadowsocksPassword = 'A password is required for Shadowsocks.'
  return errors
}
