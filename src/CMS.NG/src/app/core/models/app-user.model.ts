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

/**
 * 使用者 AppUser.
 *
 * `pkid` is an IDENTITY column but NOT the primary key — `userId` is. pkid is display-only (主代碼)
 * and never addresses a record; every API call keys on userId.
 *
 * PasswordHash never crosses the API boundary and is absent from every model here.
 */
export interface AppUser {
  pkid: number;
  userId: string;
  userName: string;
  isActive: boolean;
  /** Set on create / reset-password / change-password only. Server local wall-clock (DateTime.Now),
   *  zoneless — render as-is; treating it as UTC shifts it by the server offset. */
  passwordUpdatedTime: string | null;
  /** 角色數 — count of AppUserRole rows. */
  roleCount: number;
  /** Assigned roles. Populated only by getById. */
  roleIds: string[];
}

/** Write DTO. userId travels in the body on both create and update. No password field. */
export interface AppUserRequest {
  userId: string;
  userName: string;
  isActive: boolean;
  roleIds: string[];
}

/** Search DTO for POST /api/app-users/query. Omitted/null fields mean "no filter". */
export interface AppUserQuery {
  keyword: string | null;
  isActive: boolean | null;
}

export const EMPTY_APP_USER_QUERY: AppUserQuery = {
  keyword: null,
  isActive: null
};
