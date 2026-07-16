import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@env';

import { PublishStatusService } from './publish-status.service';
import { PublishStatus, PublishStatusRequest } from '@core/models/publish-status.model';

describe('PublishStatusService', () => {
  let service: PublishStatusService;
  let httpMock: HttpTestingController;

  const base = `${environment.apiBaseUrl}/publish-statuses`;

  const status: PublishStatus = {
    pkid: 1,
    description: '草稿',
    isDraft: true,
    isPublished: false,
    isDiscontinued: false,
    courseCount: 3
  };

  const request: PublishStatusRequest = {
    pkid: 9,
    description: '已上架',
    isDraft: false,
    isPublished: true,
    isDiscontinued: false
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [PublishStatusService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(PublishStatusService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getAll issues GET /publish-statuses', () => {
    service.getAll().subscribe(statuses => expect(statuses).toEqual([status]));

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('GET');
    req.flush([status]);
  });

  it('query POSTs the filter body to /publish-statuses/query', () => {
    const filter = { keyword: '上架', isDraft: null, isPublished: true, isDiscontinued: null };

    service.query(filter).subscribe(statuses => expect(statuses).toEqual([status]));

    const req = httpMock.expectOne(`${base}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(filter);
    req.flush([status]);
  });

  it('getById puts the numeric pkid straight into the path', () => {
    service.getById(1).subscribe();

    const req = httpMock.expectOne(`${base}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(status);
  });

  it('create POSTs to /publish-statuses with the client-supplied pkid in the body', () => {
    service.create(request).subscribe();

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    expect(req.request.body.pkid).toBe(9);   // pkid is supplied, not DB-generated
    req.flush(status);
  });

  it('update PUTs to /publish-statuses with the key in the body and NO id segment', () => {
    // The convention: PUT carries the key in the body, so the URL must be the bare collection.
    service.update({ ...request, pkid: 3 }).subscribe();

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('PUT');
    expect(req.request.url).toBe(base);
    expect(req.request.url).not.toContain('/3');
    expect(req.request.body.pkid).toBe(3);
    req.flush(null);
  });

  it('remove DELETEs /publish-statuses/{pkid}', () => {
    service.remove(3).subscribe();

    const req = httpMock.expectOne(`${base}/3`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
