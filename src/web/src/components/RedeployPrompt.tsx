import { useState } from 'react'
import { api, ApiError } from '../api/client'
import type { CredentialUsage } from '../api/types'
import { Modal } from './Modal'
import { Banner, Button, StatusBadge } from './ui'
import { Table, Th, Td, Tr } from './Table'

type Outcome = 'pending' | 'busy' | 'ok' | 'failed'

/**
 * Offered after a credential in use is edited. Secrets are baked into a container at creation, so
 * running connections keep the old ones until recreated — and a plain restart reuses the same
 * container, so it does *not* pick the change up. Redeploying is the only way, and it drops the
 * tunnel for a few seconds, which is why this asks rather than doing it silently.
 */
export function RedeployPrompt({
  credentialName,
  usages,
  onClose,
}: {
  credentialName: string
  usages: CredentialUsage[]
  onClose: () => void
}) {
  const running = usages.filter((u) => u.running)
  const others = usages.filter((u) => !u.running)

  const [outcomes, setOutcomes] = useState<Record<number, Outcome>>({})
  const [errors, setErrors] = useState<Record<number, string>>({})
  const [busy, setBusy] = useState(false)
  const [done, setDone] = useState(false)

  async function redeployAll() {
    setBusy(true)
    // Sequential: the backend serializes container work anyway, and one-at-a-time keeps the
    // interruption to a single connection at a time rather than dropping every tunnel at once.
    for (const u of running) {
      setOutcomes((o) => ({ ...o, [u.connectionId]: 'busy' }))
      try {
        await api.deployConnection(u.connectionId)
        setOutcomes((o) => ({ ...o, [u.connectionId]: 'ok' }))
      } catch (err) {
        setOutcomes((o) => ({ ...o, [u.connectionId]: 'failed' }))
        setErrors((e) => ({
          ...e,
          [u.connectionId]: err instanceof ApiError ? err.message : 'Deploy failed.',
        }))
      }
    }
    setBusy(false)
    setDone(true)
  }

  const failed = Object.values(outcomes).filter((o) => o === 'failed').length

  return (
    <Modal
      title="apply credential change?"
      onClose={busy ? () => {} : onClose}
      wide
      footer={
        done ? (
          <Button variant="primary" onClick={onClose}>
            close
          </Button>
        ) : (
          <>
            <Button variant="ghost" onClick={onClose} disabled={busy}>
              not now
            </Button>
            <Button variant="primary" onClick={() => void redeployAll()} disabled={busy}>
              {busy ? 'redeploying…' : `redeploy ${running.length}`}
            </Button>
          </>
        )
      }
    >
      <div className="grid gap-3">
        {!done && (
          <Banner kind="warn">
            <span className="text-ink">{credentialName}</span> was saved, but running connections
            still use the <span className="text-ink">old</span> secrets — a container's environment
            is fixed when it is created. Redeploying recreates them, which drops each tunnel for a
            few seconds. A plain <span className="text-ink">restart</span> would not help: it reuses
            the same container.
          </Banner>
        )}

        {done && (
          <Banner kind={failed > 0 ? 'error' : 'ok'}>
            {failed > 0
              ? `${failed} of ${running.length} failed to redeploy — see below.`
              : `Redeployed ${running.length} connection${running.length === 1 ? '' : 's'} with the new secrets.`}
          </Banner>
        )}

        {running.length > 0 && (
          <section className="grid gap-2">
            <h3 className="text-[11px] uppercase tracking-widest text-phosphor-dim">
              Will be redeployed · {running.length}
            </h3>
            <Table
              head={
                <>
                  <Th>Connection</Th>
                  <Th>Via</Th>
                  <Th>Status</Th>
                  <Th className="text-right">Result</Th>
                </>
              }
            >
              {running.map((u) => (
                <Tr key={u.connectionId}>
                  <Td className="text-ink">{u.identifier}</Td>
                  <Td className="text-cyan">{u.via}</Td>
                  <Td>
                    <StatusBadge status={u.status} />
                  </Td>
                  <Td className="text-right">
                    <OutcomeCell
                      outcome={outcomes[u.connectionId] ?? 'pending'}
                      error={errors[u.connectionId]}
                    />
                  </Td>
                </Tr>
              ))}
            </Table>
          </section>
        )}

        {running.length === 0 && (
          <Banner kind="info">
            No running connections use this credential — nothing to interrupt. The change applies
            whenever they are next deployed.
          </Banner>
        )}

        {others.length > 0 && (
          <section className="grid gap-1">
            <h3 className="text-[11px] uppercase tracking-widest text-muted">
              Not running · left alone
            </h3>
            <p className="text-[11px] text-faint">
              {others.map((u) => u.identifier).join(', ')} — deploying these would also{' '}
              <span className="text-ink">start</span> them, so they are skipped. They pick up the new
              secrets on their next deploy.
            </p>
          </section>
        )}
      </div>
    </Modal>
  )
}

function OutcomeCell({ outcome, error }: { outcome: Outcome; error?: string }) {
  if (outcome === 'busy') return <span className="text-amber">···</span>
  if (outcome === 'ok') return <span className="text-phosphor">✓ redeployed</span>
  if (outcome === 'failed') {
    return (
      <span className="text-danger" title={error}>
        ✗ failed
      </span>
    )
  }
  return <span className="text-faint">waiting</span>
}
