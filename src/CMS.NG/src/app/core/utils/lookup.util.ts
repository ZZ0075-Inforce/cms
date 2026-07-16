/**
 * Lookup helpers shared by the pages that render an N-N set as display labels.
 */

/**
 * Maps a list of pkids to their labels via a lookup, keeping raw ids the lookup is missing.
 *
 * An id with no matching option renders as `#id` rather than vanishing: a lookup that failed or
 * went stale then shows up on the page instead of silently dropping a row the record really has.
 */
export function resolveLabels<T>(
  pkids: number[],
  options: T[],
  keyOf: (option: T) => number,
  labelOf: (option: T) => string
): string[] {
  const byId = new Map(options.map(option => [keyOf(option), labelOf(option)]));
  return pkids.map(id => byId.get(id) ?? `#${id}`);
}
