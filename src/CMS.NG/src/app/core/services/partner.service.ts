import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env';
import { Partner, PartnerQuery, PartnerRequest } from '@core/models/partner.model';

@Injectable({ providedIn: 'root' })
export class PartnerService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/partners`;

  getAll(): Observable<Partner[]> {
    return this.http.get<Partner[]>(this.base);
  }

  query(query: PartnerQuery): Observable<Partner[]> {
    return this.http.post<Partner[]>(`${this.base}/query`, query);
  }

  // pkid is a number, so no encodeURIComponent here — contrast AppRoleService, whose key is
  // free-text nvarchar.
  getById(pkid: number): Observable<Partner> {
    return this.http.get<Partner>(`${this.base}/${pkid}`);
  }

  create(request: PartnerRequest): Observable<Partner> {
    return this.http.post<Partner>(this.base, request);
  }

  /** Update takes no id segment — the key rides in the body, per the API convention. */
  update(request: PartnerRequest): Observable<void> {
    return this.http.put<void>(this.base, request);
  }

  remove(pkid: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${pkid}`);
  }
}
