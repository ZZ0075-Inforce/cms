import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env';
import {
  PublishStatus,
  PublishStatusQuery,
  PublishStatusRequest
} from '@core/models/publish-status.model';

@Injectable({ providedIn: 'root' })
export class PublishStatusService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/publish-statuses`;

  getAll(): Observable<PublishStatus[]> {
    return this.http.get<PublishStatus[]>(this.base);
  }

  query(query: PublishStatusQuery): Observable<PublishStatus[]> {
    return this.http.post<PublishStatus[]>(`${this.base}/query`, query);
  }

  // pkid is a number, so no encodeURIComponent here — contrast AppRoleService, whose key is free-text.
  getById(pkid: number): Observable<PublishStatus> {
    return this.http.get<PublishStatus>(`${this.base}/${pkid}`);
  }

  create(request: PublishStatusRequest): Observable<PublishStatus> {
    return this.http.post<PublishStatus>(this.base, request);
  }

  /** Update takes no id segment — the key rides in the body, per the API convention. */
  update(request: PublishStatusRequest): Observable<void> {
    return this.http.put<void>(this.base, request);
  }

  remove(pkid: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${pkid}`);
  }
}
