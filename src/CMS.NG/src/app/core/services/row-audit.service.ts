import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env';
import { RowAuditEntry } from '@core/models/row-audit.model';

/** Read side of the cross-cutting audit trail. One call: a record's full history, newest first. */
@Injectable({ providedIn: 'root' })
export class RowAuditService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/rowaudit`;

  /** GET /api/rowaudit?tableName={tableName}&pkid={pkid}. */
  history(tableName: string, pkid: number): Observable<RowAuditEntry[]> {
    const params = new HttpParams()
      .set('tableName', tableName)
      .set('pkid', pkid);
    return this.http.get<RowAuditEntry[]>(this.base, { params });
  }
}
