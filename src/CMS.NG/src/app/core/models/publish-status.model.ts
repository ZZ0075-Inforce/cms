/**
 * 上架狀態 PublishStatus — a fixed enum-like table (草稿／上架／下架).
 *
 * `pkid` IS the primary key, but a tinyint that is NOT an IDENTITY: the client supplies it on
 * create and it is immutable on edit. It is the FK target of Course.PublishStatus_pkid.
 *
 * The slim `PublishStatusLookup` used by the Course form lives in `course.model.ts` — not here.
 */
export interface PublishStatus {
  pkid: number;
  description: string;
  isDraft: boolean;
  isPublished: boolean;
  isDiscontinued: boolean;
  /** 對應課程數 — count of Course rows pointing at this status. Display only. */
  courseCount: number;
}

/** Write DTO. pkid is supplied on create and rides in the body on update (where it is immutable). */
export interface PublishStatusRequest {
  pkid: number;
  description: string;
  isDraft: boolean;
  isPublished: boolean;
  isDiscontinued: boolean;
}

/** Search DTO for POST /api/publish-statuses/query. Keyword on Description + tri-state bit filters. */
export interface PublishStatusQuery {
  keyword: string | null;
  isDraft: boolean | null;
  isPublished: boolean | null;
  isDiscontinued: boolean | null;
}

export const EMPTY_PUBLISH_STATUS_QUERY: PublishStatusQuery = {
  keyword: null,
  isDraft: null,
  isPublished: null,
  isDiscontinued: null
};
