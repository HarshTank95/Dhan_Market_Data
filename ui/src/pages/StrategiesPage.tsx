import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { DynamicConfigForm } from '../components/DynamicConfigForm'
import { api } from '../lib/api'
import type { RegistryEntry } from '../types'

export function StrategiesPage() {
  const qc = useQueryClient()
  const [selectedId, setSelectedId] = useState<number | null>(null)

  const presets = useQuery({
    queryKey: ['strategies'],
    queryFn: api.listStrategies,
  })
  const screeners = useQuery({
    queryKey: ['registry', 'screeners'],
    queryFn: api.listScreeners,
  })
  const detail = useQuery({
    enabled: selectedId !== null,
    queryKey: ['strategies', selectedId],
    queryFn: () => api.getStrategy(selectedId!),
  })

  const update = useMutation({
    mutationFn: (body: { screenerConfig?: Record<string, unknown>; tradingConfig?: Record<string, unknown> }) =>
      api.updateStrategy(selectedId!, body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['strategies', selectedId] })
      qc.invalidateQueries({ queryKey: ['strategies'] })
    },
  })
  const reset = useMutation({
    mutationFn: () => api.resetStrategy(selectedId!),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['strategies', selectedId] })
      qc.invalidateQueries({ queryKey: ['strategies'] })
    },
  })
  const clone = useMutation({
    mutationFn: (name: string) => api.cloneStrategy(selectedId!, name),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['strategies'] }),
  })

  const screenerSchema: RegistryEntry | undefined = useMemo(
    () => screeners.data?.find(s => s.key === detail.data?.screenerType),
    [screeners.data, detail.data?.screenerType],
  )

  const [draft, setDraft] = useState<Record<string, unknown> | null>(null)
  const screenerValues = draft ?? (detail.data?.screenerConfig as Record<string, unknown> | undefined) ?? {}

  return (
    <div className="grid grid-cols-12 gap-6">
      <aside className="col-span-4">
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wider text-zinc-400">
          Strategy presets
        </h2>
        <ul className="space-y-1">
          {presets.data?.map(p => (
            <li key={p.id}>
              <button
                onClick={() => { setSelectedId(p.id); setDraft(null) }}
                className={
                  'w-full rounded-md border px-3 py-2 text-left text-sm transition ' +
                  (selectedId === p.id
                    ? 'border-emerald-600 bg-emerald-500/10'
                    : 'border-zinc-800 bg-zinc-900 hover:bg-zinc-800')
                }
              >
                <div className="flex items-center justify-between">
                  <span className="font-medium">{p.name}</span>
                  {p.isBuiltIn && <span className="rounded bg-zinc-800 px-1.5 py-0.5 text-xs text-zinc-400">built-in</span>}
                </div>
                <div className="mt-0.5 text-xs text-zinc-500">
                  {p.screenerType} → {p.strategyType}
                </div>
              </button>
            </li>
          ))}
          {presets.isLoading && <li className="text-sm text-zinc-500">Loading…</li>}
          {presets.error && <li className="text-sm text-red-400">{String(presets.error)}</li>}
        </ul>
      </aside>

      <section className="col-span-8">
        {!detail.data && <p className="text-zinc-500">Select a preset to view or edit.</p>}
        {detail.data && (
          <>
            <header className="mb-4 flex items-start justify-between gap-4">
              <div>
                <h2 className="text-xl font-semibold">{detail.data.name}</h2>
                <p className="mt-1 text-sm text-zinc-400">{detail.data.description}</p>
                <p className="mt-1 text-xs text-zinc-500">
                  {detail.data.screenerType} → {detail.data.strategyType}
                  {detail.data.isBuiltIn && ' · built-in (cannot edit; clone or reset)'}
                </p>
              </div>
              <div className="flex shrink-0 gap-2">
                {detail.data.isBuiltIn && (
                  <>
                    <button
                      onClick={() => reset.mutate()}
                      disabled={reset.isPending}
                      className="rounded-md border border-zinc-700 px-3 py-1.5 text-sm hover:bg-zinc-800 disabled:opacity-50"
                    >
                      {reset.isPending ? 'Resetting…' : 'Reset to defaults'}
                    </button>
                    <button
                      onClick={() => {
                        const name = prompt('Name for the clone?', detail.data!.name + ' (custom)')
                        if (name) clone.mutate(name)
                      }}
                      disabled={clone.isPending}
                      className="rounded-md bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-emerald-500 disabled:opacity-50"
                    >
                      Clone to edit
                    </button>
                  </>
                )}
                {!detail.data.isBuiltIn && draft && (
                  <button
                    onClick={() => update.mutate({ screenerConfig: draft })}
                    disabled={update.isPending}
                    className="rounded-md bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-emerald-500 disabled:opacity-50"
                  >
                    {update.isPending ? 'Saving…' : 'Save changes'}
                  </button>
                )}
              </div>
            </header>

            {screenerSchema && (
              <DynamicConfigForm
                schema={screenerSchema}
                values={screenerValues}
                onChange={setDraft}
                disabled={detail.data.isBuiltIn}
              />
            )}
          </>
        )}
      </section>
    </div>
  )
}
