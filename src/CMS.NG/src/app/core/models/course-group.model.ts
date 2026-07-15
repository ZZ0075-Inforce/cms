/**
 * 課程群組 CourseGroup.
 *
 * `pkid` IS the primary key (PK_CourseGroup over a smallint IDENTITY), so it addresses the record
 * and needs no URL encoding.
 */
export interface CourseGroup {
  pkid: number;
  /** 群組名稱 — maps to the DB column `Description`, the group's only real field. */
  description: string;
  /**
   * 對應課程數 — count of Course rows in this group. Also the number of courses that would be
   * cascade-deleted along with the group, which the list surfaces in the delete-confirm warning.
   */
  courseCount: number;
}

/** Write DTO. pkid travels in the body on update; on create the DB generates it. */
export interface CourseGroupRequest {
  pkid: number;
  description: string;
}

/** Search DTO for POST /api/course-groups/query. Description is the only column to filter on. */
export interface CourseGroupQuery {
  keyword: string | null;
}

export const EMPTY_COURSE_GROUP_QUERY: CourseGroupQuery = {
  keyword: null
};

/** Slim shape for dropdowns in the tables that FK to CourseGroup (Course, …). Label is description. */
export interface CourseGroupLookup {
  pkid: number;
  description: string;
}
