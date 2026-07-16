/**
 * One entry of a single record's 異動紀錄 (row-audit history), as returned by
 * GET /api/rowaudit?tableName=…&pkid=…. Read-only — the UI only ever lists these.
 */
export interface RowAuditEntry {
  /** ISO date-time of the change (server local time, no timezone suffix). */
  dateTime: string;
  /** Who made the change, or "system" when there was no signed-in user. */
  userName: string;
  /** Insert / Update / Delete. */
  actionType: string;
  /** Insert/Delete: the row's first string column; Update: the changed column names. */
  actionDesc: string | null;
}
