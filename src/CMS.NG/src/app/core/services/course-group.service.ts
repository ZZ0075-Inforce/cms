import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env';
import { CourseGroup, CourseGroupQuery, CourseGroupRequest } from '@core/models/course-group.model';

@Injectable({ providedIn: 'root' })
export class CourseGroupService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/course-groups`;

  getAll(): Observable<CourseGroup[]> {
    return this.http.get<CourseGroup[]>(this.base);
  }

  query(query: CourseGroupQuery): Observable<CourseGroup[]> {
    return this.http.post<CourseGroup[]>(`${this.base}/query`, query);
  }

  // pkid is a number, so no encodeURIComponent here — contrast AppRoleService, whose key is
  // free-text nvarchar.
  getById(pkid: number): Observable<CourseGroup> {
    return this.http.get<CourseGroup>(`${this.base}/${pkid}`);
  }

  create(request: CourseGroupRequest): Observable<CourseGroup> {
    return this.http.post<CourseGroup>(this.base, request);
  }

  /** Update takes no id segment — the key rides in the body, per the API convention. */
  update(request: CourseGroupRequest): Observable<void> {
    return this.http.put<void>(this.base, request);
  }

  remove(pkid: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${pkid}`);
  }
}
