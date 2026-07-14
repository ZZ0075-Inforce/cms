import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env';
import { AppRole, AppRoleQuery, AppRoleRequest } from '@core/models/app-role.model';

@Injectable({ providedIn: 'root' })
export class AppRoleService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/app-roles`;

  getAll(): Observable<AppRole[]> {
    return this.http.get<AppRole[]>(this.base);
  }

  query(query: AppRoleQuery): Observable<AppRole[]> {
    return this.http.post<AppRole[]>(`${this.base}/query`, query);
  }

  // roleId is a free-text nvarchar key, so it must be encoded before going into the URL path.
  getById(roleId: string): Observable<AppRole> {
    return this.http.get<AppRole>(`${this.base}/${encodeURIComponent(roleId)}`);
  }

  create(request: AppRoleRequest): Observable<AppRole> {
    return this.http.post<AppRole>(this.base, request);
  }

  /** Update takes no id segment — the key rides in the body, per the API convention. */
  update(request: AppRoleRequest): Observable<void> {
    return this.http.put<void>(this.base, request);
  }

  remove(roleId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${encodeURIComponent(roleId)}`);
  }
}
