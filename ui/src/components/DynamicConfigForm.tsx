import type { ChangeEvent } from 'react'

import type { RegistryEntry, RegistryField } from '../types'

interface Props {
  schema: RegistryEntry
  values: Record<string, unknown>
  onChange: (next: Record<string, unknown>) => void
  disabled?: boolean
}

// Renders a config form purely from registry metadata. Adding a new screener =
// decorate its config class with [ConfigField] in C#; this form picks it up
// automatically on next /api/registry refresh — no UI code change.
export function DynamicConfigForm({ schema, values, onChange, disabled }: Props) {
  const groups = new Map<string, RegistryField[]>()
  for (const f of schema.fields) {
    const list = groups.get(f.group) ?? []
    list.push(f)
    groups.set(f.group, list)
  }

  const setField = (name: string, value: unknown) =>
    onChange({ ...values, [name]: value })

  return (
    <div className="space-y-6">
      {[...groups.entries()].map(([group, fields]) => (
        <section key={group} className="rounded-lg border border-zinc-800 p-4">
          <h3 className="mb-3 text-sm font-semibold uppercase tracking-wider text-zinc-400">
            {group}
          </h3>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            {fields.map(f => (
              <FieldInput
                key={f.name}
                field={f}
                value={values[f.name] ?? f.default}
                onChange={v => setField(f.name, v)}
                disabled={disabled}
              />
            ))}
          </div>
        </section>
      ))}
    </div>
  )
}

function FieldInput({
  field, value, onChange, disabled,
}: {
  field: RegistryField
  value: unknown
  onChange: (v: unknown) => void
  disabled?: boolean
}) {
  const label = (
    <label className="block text-xs font-medium text-zinc-400">
      {field.label}
      {field.unit ? <span className="ml-1 text-zinc-500">({field.unit})</span> : null}
    </label>
  )

  const descr = field.description
    ? <p className="mt-1 text-xs text-zinc-500">{field.description}</p>
    : null

  const baseInput = 'w-full rounded-md border border-zinc-700 bg-zinc-900 px-3 py-2 text-sm text-zinc-100 placeholder:text-zinc-600 focus:border-emerald-500 focus:outline-none disabled:opacity-50'

  if (field.kind === 'boolean') {
    return (
      <div>
        {label}
        <div className="mt-1">
          <input
            type="checkbox"
            checked={Boolean(value)}
            disabled={disabled}
            onChange={(e: ChangeEvent<HTMLInputElement>) => onChange(e.target.checked)}
            className="h-4 w-4 rounded border-zinc-700 bg-zinc-900 text-emerald-500"
          />
        </div>
        {descr}
      </div>
    )
  }

  if (field.kind === 'timeofday') {
    const str = typeof value === 'string' ? value : String(value ?? '00:00:00')
    return (
      <div>
        {label}
        <input
          type="time"
          step={1}
          value={str.length >= 5 ? str.slice(0, 8) : str}
          disabled={disabled}
          onChange={e => onChange(e.target.value.length === 5 ? `${e.target.value}:00` : e.target.value)}
          className={baseInput + ' mt-1'}
        />
        {descr}
      </div>
    )
  }

  if (field.kind === 'text') {
    return (
      <div>
        {label}
        <input
          type="text"
          value={typeof value === 'string' ? value : ''}
          disabled={disabled}
          onChange={e => onChange(e.target.value)}
          className={baseInput + ' mt-1'}
        />
        {descr}
      </div>
    )
  }

  // numeric kinds: number, integer, percent, currency, multiplier
  return (
    <div>
      {label}
      <input
        type="number"
        value={value === null || value === undefined ? '' : Number(value)}
        min={field.min}
        max={field.max}
        step={field.step ?? (field.kind === 'integer' ? 1 : 'any')}
        disabled={disabled}
        onChange={e => {
          const raw = e.target.value
          onChange(raw === '' ? null : Number(raw))
        }}
        className={baseInput + ' mt-1'}
      />
      {descr}
    </div>
  )
}
