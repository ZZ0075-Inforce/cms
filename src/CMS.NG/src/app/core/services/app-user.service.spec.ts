import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@env';

import { AppUserService } from './app-user.service';
import { AppUser, AppUserRequest } from '@core/models/app-user.model';

describe('AppUserService', () => {
  let service: AppUserService;
  let httpMock: HttpTestingController;

  const base = `${environment.apiBaseUrl}/app-users`;

  const user: AppUser = {
    pkid: 1,
    userId: 'miles@uuu.com.tw',
    userName: 'Miles Sun',
    isActive: true,
    passwordUpdatedTime: null,
    roleCount: 2,
    roleIds: []
  };

  const request: AppUserRequest = {
    userId: 'editor@uuu.com.tw',
    userName: 'Editor',
    isActive: true,
    roleIds: ['Admin']
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AppUserService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(AppUserService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getAll issues GET /app-users', () => {
    service.getAll().subscribe(users => expect(users).toEqual([user]));

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('GET');
    req.flush([user]);
  });

  it('query POSTs the filter body to /app-users/query', () => {
    const filter = { keyword: 'mil', isActive: true };

    service.query(filter).subscribe(users => expect(users).toEqual([user]));

    const req = httpMock.expectOne(`${base}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(filter);
    req.flush([user]);
  });

  it('getById encodes the userId (email) into the URL path', () => {
    // userId is free-text nvarchar (often an email); an unencoded '/' would address a different route.
    service.getById('miles@uuu.com.tw').subscribe();

    const req = httpMock.expectOne(`${base}/miles%40uuu.com.tw`);
    expect(req.request.method).toBe('GET');
    req.flush(user);
  });

  it('create POSTs to /app-users', () => {
    service.create(request).subscribe();

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(user);
  });

  it('update PUTs to /app-users with the key in the body and NO id segment', () => {
    service.update(request).subscribe();

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('PUT');
    expect(req.request.url).toBe(base);
    expect(req.request.url).not.toContain('/editor');
    expect(req.request.body.userId).toBe('editor@uuu.com.tw');
    req.flush(null);
  });

  it('remove encodes the userId into the URL path', () => {
    service.remove('x/y').subscribe();

    const req = httpMock.expectOne(`${base}/x%2Fy`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('resetPassword POSTs to /app-users/{enc}/reset-password with an empty body', () => {
    service.resetPassword('miles@uuu.com.tw').subscribe();

    const req = httpMock.expectOne(`${base}/miles%40uuu.com.tw/reset-password`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush(null);
  });
});
