/**
 * Minimal, signature-agnostic JWT payload reader. The API validates the signature; the SPA only needs
 * a couple of claims for UI gating, so this just base64url-decodes the payload — it does NOT verify.
 *
 * Only ASCII claims (`role`, `exp`) are read here. Never decode `userName` this way: it is Chinese, and
 * `atob` is Latin-1 — it would mojibake multi-byte UTF-8. userName comes from the login response body.
 */

interface JwtPayload {
  role?: string | string[];
  exp?: number;
}

function decodePayload(token: string | null | undefined): JwtPayload | null {
  const part = token?.split('.')[1];
  if (!part) return null;
  try {
    // base64url -> base64, then pad the length up to a multiple of 4 so atob accepts it.
    const base64 = part.replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');
    return JSON.parse(atob(padded)) as JwtPayload;
  } catch {
    return null;
  }
}

/**
 * The user's roles from the `role` claim. JsonWebTokenHandler serialises a single role as a string,
 * several as an array, and omits the claim when there are none — normalise all three to a string[].
 */
export function decodeRoles(token: string | null | undefined): string[] {
  const role = decodePayload(token)?.role;
  return Array.isArray(role) ? role : role ? [role] : [];
}

/** The token's expiry as epoch seconds, or null when the claim is absent or the token is unparseable. */
export function decodeExp(token: string | null | undefined): number | null {
  return decodePayload(token)?.exp ?? null;
}
