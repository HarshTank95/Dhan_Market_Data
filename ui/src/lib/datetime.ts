// Shared parsing helpers for API-emitted timestamps.
//
// EF Core's SQLite provider strips DateTime.Kind on round-trip, so the API
// emits UTC timestamps without a "Z" suffix. JS's Date constructor treats
// those as LOCAL time, which produces 5h30m drift in IST browsers.
// These helpers force UTC interpretation when the marker is missing.

export function parseUtc(iso: string): Date {
  const hasTz = /[Zz]|[+-]\d{2}:?\d{2}$/.test(iso)
  return new Date(hasTz ? iso : iso + 'Z')
}

export function formatIstTime(iso: string): string {
  if (!iso) return ''
  const d = parseUtc(iso)
  if (Number.isNaN(d.getTime())) return ''
  return d.toLocaleTimeString('en-GB', {
    timeZone: 'Asia/Kolkata',
    hour: '2-digit',
    minute: '2-digit',
  })
}
