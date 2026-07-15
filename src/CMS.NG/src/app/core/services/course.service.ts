import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env';
import { Course, CourseQuery, CourseRequest } from '@core/models/course.model';

@Injectable({ providedIn: 'root' })
export class CourseService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/courses`;

  getAll(): Observable<Course[]> {
    return this.http.get<Course[]>(this.base);
  }

  query(query: CourseQuery): Observable<Course[]> {
    return this.http.post<Course[]>(`${this.base}/query`, query);
  }

  // pkid is a number, so no encodeURIComponent here.
  getById(pkid: number): Observable<Course> {
    return this.http.get<Course>(`${this.base}/${pkid}`);
  }

  create(request: CourseRequest): Observable<Course> {
    return this.http.post<Course>(this.base, request);
  }

  /** Update takes no id segment — the key rides in the body, per the API convention. */
  update(request: CourseRequest): Observable<void> {
    return this.http.put<void>(this.base, request);
  }

  remove(pkid: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${pkid}`);
  }
}
