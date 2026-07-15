import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@env';

import { PartnerService } from './partner.service';
import { Partner, PartnerRequest } from '@core/models/partner.model';

describe('PartnerService', () => {
  let service: PartnerService;
  let httpMock: HttpTestingController;

  const base = `${environment.apiBaseUrl}/partners`;

  const partner: Partner = {
    pkid: 1,
    name: 'Microsoft',
    appKey: 'MS',
    nameOnPartnerMenu: 'Microsoft 微軟',
    nameOnCourseDetailPage: '微軟',
    displayOrder: 10,
    imageFilename: 'ms.png',
    courseCount: 7
  };

  const request: PartnerRequest = {
    pkid: 0,
    name: 'Cisco',
    appKey: 'CSCO',
    nameOnPartnerMenu: 'Cisco 思科',
    nameOnCourseDetailPage: '思科',
    displayOrder: 20,
    imageFilename: null
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [PartnerService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(PartnerService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getAll issues GET /partners', () => {
    service.getAll().subscribe(partners => expect(partners).toEqual([partner]));

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('GET');
    req.flush([partner]);
  });

  it('query POSTs the filter body to /partners/query', () => {
    const filter = { keyword: 'micro' };

    service.query(filter).subscribe(partners => expect(partners).toEqual([partner]));

    const req = httpMock.expectOne(`${base}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(filter);
    req.flush([partner]);
  });

  it('getById puts the numeric pkid straight into the path', () => {
    service.getById(1).subscribe();

    const req = httpMock.expectOne(`${base}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(partner);
  });

  it('create POSTs to /partners', () => {
    service.create(request).subscribe();

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(partner);
  });

  it('update PUTs to /partners with the key in the body and NO id segment', () => {
    // The convention: PUT carries the key in the body, so the URL must be the bare collection.
    service.update({ ...request, pkid: 3 }).subscribe();

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('PUT');
    expect(req.request.url).toBe(base);
    expect(req.request.url).not.toContain('/3');
    expect(req.request.body.pkid).toBe(3);
    req.flush(null);
  });

  it('remove DELETEs /partners/{pkid}', () => {
    service.remove(3).subscribe();

    const req = httpMock.expectOne(`${base}/3`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
