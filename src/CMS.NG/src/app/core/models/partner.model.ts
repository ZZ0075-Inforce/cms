/**
 * 合作廠商 Partner.
 *
 * Unlike AppRole, `pkid` here IS the primary key (PK_Partner over a smallint IDENTITY), so it
 * addresses the record and needs no URL encoding.
 */
export interface Partner {
  pkid: number;
  name: string;
  appKey: string;
  nameOnPartnerMenu: string;
  nameOnCourseDetailPage: string;
  displayOrder: number;
  imageFilename: string | null;
  /** 對應課程數 — count of Course rows pointing at this partner. */
  courseCount: number;
}

/** Write DTO. pkid travels in the body on update; on create the DB generates it. */
export interface PartnerRequest {
  pkid: number;
  name: string;
  appKey: string;
  nameOnPartnerMenu: string;
  nameOnCourseDetailPage: string;
  displayOrder: number;
  imageFilename: string | null;
}

/** Search DTO for POST /api/partners/query. Partner has no FK, bool or date column to filter on. */
export interface PartnerQuery {
  keyword: string | null;
}

export const EMPTY_PARTNER_QUERY: PartnerQuery = {
  keyword: null
};

/** Slim shape for dropdowns in the tables that FK to Partner (Course, Certification, …). */
export interface PartnerLookup {
  pkid: number;
  name: string;
}
