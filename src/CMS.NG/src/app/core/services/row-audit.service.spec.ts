import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@env';

import { RowAuditService } from './row-audit.service';
import { RowAuditEntry } from '@core/models/row-audit.model';

describe('RowAuditService', () => {
  let service: RowAuditService;
  let httpMock: HttpTestingController;

  const base = `${environment.apiBaseUrl}/rowaudit`;
  const entry: RowAuditEntry = {
    dateTime: '2026-06-04T14:30:00',
    userName: 'alice',
    actionType: 'Update',
    actionDesc: 'Title'
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [RowAuditService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(RowAuditService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('history GETs /rowaudit with tableName and pkid query params', () => {
    service.history('Course', 123).subscribe(rows => expect(rows).toEqual([entry]));

    const req = httpMock.expectOne(r => r.url === base);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('tableName')).toBe('Course');
    expect(req.request.params.get('pkid')).toBe('123');
    req.flush([entry]);
  });
});
