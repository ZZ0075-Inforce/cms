import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env';
import { AppUserLookup } from '@core/models/app-user.model';
import { AppRoleLookup } from '@core/models/app-role.model';
import { PartnerLookup } from '@core/models/partner.model';
import { CourseGroupLookup } from '@core/models/course-group.model';
import { CertificationLookup, JobCategoryLookup, PublishStatusLookup } from '@core/models/course.model';
import { PromotionLookup, TrainingCenterLookup } from '@core/models/featured-promo-item.model';

@Injectable({ providedIn: 'root' })
export class LookupService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/lookups`;

  appUsers(): Observable<AppUserLookup[]> {
    return this.http.get<AppUserLookup[]>(`${this.base}/app-users`);
  }

  /** AppRole is the FK target of AppUserRole — the AppUser N-N (roles assigned to a user). */
  appRoles(): Observable<AppRoleLookup[]> {
    return this.http.get<AppRoleLookup[]>(`${this.base}/app-roles`);
  }

  /** Partner is the FK target of Course, Certification, PartnerCourseGroup, Promotion2, Seminar. */
  partners(): Observable<PartnerLookup[]> {
    return this.http.get<PartnerLookup[]>(`${this.base}/partners`);
  }

  /** CourseGroup is the FK target of Course (Course.CourseGroup_pkid). */
  courseGroups(): Observable<CourseGroupLookup[]> {
    return this.http.get<CourseGroupLookup[]>(`${this.base}/course-groups`);
  }

  /** PublishStatus is the FK target of Course.PublishStatus_pkid (a fixed enum table). */
  publishStatuses(): Observable<PublishStatusLookup[]> {
    return this.http.get<PublishStatusLookup[]>(`${this.base}/publish-statuses`);
  }

  /** Certification — the Course N-N (CourseInCertification). Title is RTRIMmed server-side. */
  certifications(): Observable<CertificationLookup[]> {
    return this.http.get<CertificationLookup[]>(`${this.base}/certifications`);
  }

  /** JobCategory — the Course N-N (CourseJobCategories). */
  jobCategories(): Observable<JobCategoryLookup[]> {
    return this.http.get<JobCategoryLookup[]>(`${this.base}/job-categories`);
  }

  /** TrainingCenter — the FK target of FeaturedPromoItem.TrainingCenter_pkid; the board's centre tabs. */
  trainingCenters(): Observable<TrainingCenterLookup[]> {
    return this.http.get<TrainingCenterLookup[]>(`${this.base}/training-centers`);
  }

  /**
   * Resolves a Promotion2 by its unique PromoCode, for the FeaturedPromoItem edit form. The code is a
   * URL path segment, so it is encodeURIComponent'd. 404s when no promotion carries that code.
   */
  promotionByCode(code: string): Observable<PromotionLookup> {
    return this.http.get<PromotionLookup>(`${this.base}/promotions/by-code/${encodeURIComponent(code)}`);
  }
}
