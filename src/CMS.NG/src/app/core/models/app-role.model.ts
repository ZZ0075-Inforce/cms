/**
 * 角色 AppRole.
 *
 * `pkid` is an IDENTITY column but NOT the primary key — `roleId` is. pkid is display-only
 * (主代碼) and is never used to address a record; every API call keys on roleId.
 */
export interface AppRole {
  pkid: number;
  roleId: string;
  roleName: string;
  permissionLevel: number;
  description: string | null;
  /** 使用者數 — count of AppUserRole rows. */
  userCount: number;
  /** Assigned users. Populated only by getById. */
  userIds: string[];
}

/** Write DTO. roleId travels in the body on both create and update. */
export interface AppRoleRequest {
  roleId: string;
  roleName: string;
  permissionLevel: number;
  description: string | null;
  userIds: string[];
}

/** Search DTO for POST /api/app-roles/query. Omitted/null fields mean "no filter". */
export interface AppRoleQuery {
  keyword: string | null;
  permissionLevel: number | null;
}

export const EMPTY_APP_ROLE_QUERY: AppRoleQuery = {
  keyword: null,
  permissionLevel: null
};

/** Slim AppRole row from GET /api/lookups/app-roles (AppRole is the FK target of AppUserRole). */
export interface AppRoleLookup {
  roleId: string;
  roleName: string;
}

/** Option label used where a role is shown in a dropdown or chip: "Administrator (Admin)". */
export function appRoleLabel(role: AppRoleLookup): string {
  return `${role.roleName} (${role.roleId})`;
}
