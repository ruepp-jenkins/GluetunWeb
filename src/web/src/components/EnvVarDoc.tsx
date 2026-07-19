import { envDocs, type EnvDoc, type EnvDocKey } from '../data/envCatalog'

/**
 * Always-visible documentation block shown beneath a field: the env var name, a plain
 * description, and a concrete example. Deliberately NOT a tooltip — the docs are permanent
 * on-screen per the interface requirements.
 */
export function EnvVarDoc({ doc: key }: { doc: EnvDocKey }) {
  const doc: EnvDoc = envDocs[key]
  return (
    <div className="mt-1 border-l-2 border-line pl-2 text-[11px] leading-relaxed text-muted">
      {doc.env && (
        <span className="mr-1 rounded-sm bg-panel-2 px-1 py-px text-phosphor-dim">{doc.env}</span>
      )}
      <span>{doc.description}</span>
      {doc.example && (
        <div className="mt-0.5 text-faint">
          e.g. <span className="whitespace-pre-wrap text-amber">{doc.example}</span>
        </div>
      )}
    </div>
  )
}
