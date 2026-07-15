import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@env';

import { CourseGroupService } from './course-group.service';
import { CourseGroup, CourseGroupRequest } from '@core/models/course-group.model';

describe('CourseGroupService', () => {
  let service: CourseGroupService;
  let httpMock: HttpTestingController;

  const base = `${environment.apiBaseUrl}/course-groups`;

  const group: CourseGroup = { pkid: 1, description: '雲端', courseCount: 7 };
  const request: CourseGroupRequest = { pkid: 0, description: '資安' };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [CourseGroupService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(CourseGroupService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getAll issues GET /course-groups', () => {
    service.getAll().subscribe(groups => expect(groups).toEqual([group]));

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('GET');
    req.flush([group]);
  });

  it('query POSTs the filter body to /course-groups/query', () => {
    const filter = { keyword: '雲' };

    service.query(filter).subscribe(groups => expect(groups).toEqual([group]));

    const req = httpMock.expectOne(`${base}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(filter);
    req.flush([group]);
  });

  it('getById puts the numeric pkid straight into the path', () => {
    service.getById(1).subscribe();

    const req = httpMock.expectOne(`${base}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(group);
  });

  it('create POSTs to /course-groups', () => {
    service.create(request).subscribe();

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(group);
  });

  it('update PUTs to /course-groups with the key in the body and NO id segment', () => {
    // The convention: PUT carries the key in the body, so the URL must be the bare collection.
    service.update({ ...request, pkid: 3 }).subscribe();

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('PUT');
    expect(req.request.url).toBe(base);
    expect(req.request.url).not.toContain('/3');
    expect(req.request.body.pkid).toBe(3);
    req.flush(null);
  });

  it('remove DELETEs /course-groups/{pkid}', () => {
    service.remove(3).subscribe();

    const req = httpMock.expectOne(`${base}/3`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
