/** Slim AppUser row from GET /api/lookups/app-users. */
export interface AppUserLookup {
  userId: string;
  userName: string;
  isActive: boolean;
}

/** Option label used everywhere a user is shown in a dropdown or chip: "Miles Sun (miles@uuu.com.tw)". */
export function appUserLabel(user: AppUserLookup): string {
  return `${user.userName} (${user.userId})`;
}
