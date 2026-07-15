import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@env';

import { CourseService } from './course.service';
import { Course, CourseRequest } from '@core/models/course.model';

describe('CourseService', () => {
  let service: CourseService;
  let httpMock: HttpTestingController;

  const base = `${environment.apiBaseUrl}/courses`;

  const course = { pkid: 1, title: 'Azure', courseId: 'AZ-900' } as Course;
  const request = { pkid: 0, title: 'CCNA', courseId: 'CCNA' } as CourseRequest;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [CourseService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(CourseService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getAll issues GET /courses', () => {
    service.getAll().subscribe(courses => expect(courses).toEqual([course]));

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('GET');
    req.flush([course]);
  });

  it('query POSTs the filter body to /courses/query', () => {
    const filter = { keyword: 'azure', partnerPkid: 1 };

    service.query(filter as never).subscribe(courses => expect(courses).toEqual([course]));

    const req = httpMock.expectOne(`${base}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(filter);
    req.flush([course]);
  });

  it('getById puts the numeric pkid straight into the path', () => {
    service.getById(1).subscribe();

    const req = httpMock.expectOne(`${base}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(course);
  });

  it('create POSTs to /courses', () => {
    service.create(request).subscribe();

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(course);
  });

  it('update PUTs to /courses with the key in the body and NO id segment', () => {
    service.update({ ...request, pkid: 3 }).subscribe();

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('PUT');
    expect(req.request.url).toBe(base);
    expect(req.request.url).not.toContain('/3');
    expect(req.request.body.pkid).toBe(3);
    req.flush(null);
  });

  it('remove DELETEs /courses/{pkid}', () => {
    service.remove(3).subscribe();

    const req = httpMock.expectOne(`${base}/3`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
