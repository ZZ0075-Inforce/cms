import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env';
import { AppUser, AppUserQuery, AppUserRequest } from '@core/models/app-user.model';

@Injectable({ providedIn: 'root' })
export class AppUserService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/app-users`;

  getAll(): Observable<AppUser[]> {
    return this.http.get<AppUser[]>(this.base);
  }

  query(query: AppUserQuery): Observable<AppUser[]> {
    return this.http.post<AppUser[]>(`${this.base}/query`, query);
  }

  // userId is a free-text nvarchar key (often an email), so it must be encoded into the URL path.
  getById(userId: string): Observable<AppUser> {
    return this.http.get<AppUser>(`${this.base}/${encodeURIComponent(userId)}`);
  }

  create(request: AppUserRequest): Observable<AppUser> {
    return this.http.post<AppUser>(this.base, request);
  }

  /** Update takes no id segment — the key rides in the body, per the API convention. */
  update(request: AppUserRequest): Observable<void> {
    return this.http.put<void>(this.base, request);
  }

  remove(userId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${encodeURIComponent(userId)}`);
  }

  /** Resets the password to the SysConfig default (server-side). No password value is sent. */
  resetPassword(userId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${encodeURIComponent(userId)}/reset-password`, {});
  }
}
