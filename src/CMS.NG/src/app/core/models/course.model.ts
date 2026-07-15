/**
 * 課程 Course.
 *
 * `pkid` IS the primary key (PK_Course over an int IDENTITY), so it addresses the record and needs
 * no URL encoding. Dates travel as ISO `yyyy-MM-dd` strings; the form converts to/from `Date` via
 * `core/utils/date.util.ts`. The three `{fk}Name` fields are JOIN-resolved display labels.
 */
export interface Course {
  pkid: number;
  title: string;
  officialTitle: string | null;
  courseId: string;
  prodCourseId: string;
  friendlyUrl: string;
  displayOrder: number;
  partnerPkid: number;
  courseGroupPkid: number | null;
  publishStatusPkid: number;
  scheduleOn: string;
  scheduleOff: string;
  hour: number;
  listPrice: number;
  learningCredit: number;
  material: string | null;
  objective: string | null;
  target: string | null;
  prerequisites: string | null;
  outline: string | null;
  towardCertOrExam: string | null;
  note: string | null;
  otherInfo: string | null;
  canRepeat: boolean;

  /** JOIN-resolved display labels. */
  partnerName: string;
  courseGroupName: string | null;
  publishStatusName: string;

  /** N-N sets, populated only by getById. */
  certificationPkids: number[];
  jobCategoryPkids: number[];
}

/** Write DTO. pkid travels in the body on update; on create the DB generates it. */
export interface CourseRequest {
  pkid: number;
  title: string;
  officialTitle: string | null;
  courseId: string;
  prodCourseId: string;
  friendlyUrl: string;
  displayOrder: number;
  partnerPkid: number;
  courseGroupPkid: number | null;
  publishStatusPkid: number;
  scheduleOn: string;
  scheduleOff: string;
  hour: number;
  listPrice: number;
  learningCredit: number;
  material: string | null;
  objective: string | null;
  target: string | null;
  prerequisites: string | null;
  outline: string | null;
  towardCertOrExam: string | null;
  note: string | null;
  otherInfo: string | null;
  canRepeat: boolean;
  certificationPkids: number[];
  jobCategoryPkids: number[];
}

/** Search DTO for POST /api/courses/query. Omitted/null fields mean "no filter". */
export interface CourseQuery {
  keyword: string | null;
  partnerPkid: number | null;
  courseGroupPkid: number | null;
  publishStatusPkid: number | null;
  canRepeat: boolean | null;
  scheduleOnFrom: string | null;
  scheduleOnTo: string | null;
  scheduleOffFrom: string | null;
  scheduleOffTo: string | null;
}

export const EMPTY_COURSE_QUERY: CourseQuery = {
  keyword: null,
  partnerPkid: null,
  courseGroupPkid: null,
  publishStatusPkid: null,
  canRepeat: null,
  scheduleOnFrom: null,
  scheduleOnTo: null,
  scheduleOffFrom: null,
  scheduleOffTo: null
};

/** Slim shapes for the Course FK dropdowns / N-N multi-selects. */
export interface PublishStatusLookup {
  pkid: number;
  description: string;
}

export interface CertificationLookup {
  pkid: number;
  title: string;
}

export interface JobCategoryLookup {
  pkid: number;
  description: string;
}
