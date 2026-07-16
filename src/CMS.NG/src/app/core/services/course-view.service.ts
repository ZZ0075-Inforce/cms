import { Injectable, inject } from '@angular/core';
import { Observable, forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { Course, CertificationLookup, JobCategoryLookup } from '@core/models/course.model';
import { resolveLabels } from '@core/utils/lookup.util';

/** A course plus its two N-N sets already resolved to display labels. */
export interface CourseView {
  course: Course;
  certificationLabels: string[];
  jobCategoryLabels: string[];
}

/**
 * Reads one course the way a page wants to show it: the record plus its N-N sets resolved
 * to labels. Shared by 檢視課程 (course-detail) and the 另存 PDF print view so both render
 * the same data from the same load.
 *
 *   load(pkid)
 *     └─ forkJoin ─┬─ courses.getById(pkid)      → error propagates → caller shows not-found
 *                  ├─ lookups.certifications()   → error → [] → labels degrade to "#id"
 *                  └─ lookups.jobCategories()    → error → [] → labels degrade to "#id"
 *          └─ map → { course, certificationLabels, jobCategoryLabels }
 *
 * The asymmetry is deliberate: the course IS the payload, so losing it is fatal to the page,
 * but a failed lookup only costs prettier labels and must not take the page down with it.
 */
@Injectable({ providedIn: 'root' })
export class CourseViewService {
  private readonly courses = inject(CourseService);
  private readonly lookups = inject(LookupService);

  load(pkid: number): Observable<CourseView> {
    return forkJoin({
      course: this.courses.getById(pkid),
      certifications: this.lookups.certifications().pipe(catchError(() => of([] as CertificationLookup[]))),
      jobCategories: this.lookups.jobCategories().pipe(catchError(() => of([] as JobCategoryLookup[])))
    }).pipe(
      map(({ course, certifications, jobCategories }) => ({
        course,
        certificationLabels:
          resolveLabels(course.certificationPkids, certifications, c => c.pkid, c => c.title),
        jobCategoryLabels:
          resolveLabels(course.jobCategoryPkids, jobCategories, j => j.pkid, j => j.description)
      }))
    );
  }
}
