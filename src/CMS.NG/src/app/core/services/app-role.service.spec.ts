import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@env';

import { AppRoleService } from './app-role.service';
import { AppRole, AppRoleRequest } from '@core/models/app-role.model';

describe('AppRoleService', () => {
  let service: AppRoleService;
  let httpMock: HttpTestingController;

  const base = `${environment.apiBaseUrl}/app-roles`;

  const role: AppRole = {
    pkid: 1,
    roleId: 'Admin',
    roleName: 'Administrator',
    permissionLevel: 1,
    description: '系統管理員',
    userCount: 3,
    userIds: []
  };

  const request: AppRoleRequest = {
    roleId: 'Editor',
    roleName: 'Editor',
    permissionLevel: 50,
    description: null,
    userIds: ['alice']
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AppRoleService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(AppRoleService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getAll issues GET /app-roles', () => {
    service.getAll().subscribe(roles => expect(roles).toEqual([role]));

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('GET');
    req.flush([role]);
  });

  it('query POSTs the filter body to /app-roles/query', () => {
    const filter = { keyword: 'adm', permissionLevel: 1 };

    service.query(filter).subscribe(roles => expect(roles).toEqual([role]));

    const req = httpMock.expectOne(`${base}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(filter);
    req.flush([role]);
  });

  it('getById encodes the roleId into the URL path', () => {
    // roleId is free-text nvarchar; an unencoded '/' would silently address a different route.
    service.getById('a b/c').subscribe();

    const req = httpMock.expectOne(`${base}/a%20b%2Fc`);
    expect(req.request.method).toBe('GET');
    req.flush(role);
  });

  it('create POSTs to /app-roles', () => {
    service.create(request).subscribe();

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(role);
  });

  it('update PUTs to /app-roles with the key in the body and NO id segment', () => {
    service.update(request).subscribe();

    // The convention: PUT carries the key in the body, so the URL must be the bare collection.
    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('PUT');
    expect(req.request.url).toBe(base);
    expect(req.request.url).not.toContain('/Editor');
    expect(req.request.body.roleId).toBe('Editor');
    req.flush(null);
  });

  it('remove encodes the roleId into the URL path', () => {
    service.remove('x/y').subscribe();

    const req = httpMock.expectOne(`${base}/x%2Fy`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
