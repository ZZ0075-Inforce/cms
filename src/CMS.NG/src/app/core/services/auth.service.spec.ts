import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { environment } from '@env';

import { AuthService } from './auth.service';

/** Builds an unsigned JWT with the given payload — the frontend never verifies the signature. */
function makeToken(payload: object): string {
  const b64url = (obj: object) =>
    btoa(JSON.stringify(obj)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `${b64url({ alg: 'HS256', typ: 'JWT' })}.${b64url(payload)}.sig`;
}

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('login POSTs credentials to /Auth/login', () => {
    service.login({ userId: 'admin', password: 'pw' }).subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/Auth/login`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ userId: 'admin', password: 'pw' });
    req.flush({ userId: 'admin', userName: 'A', accessToken: 't' });
  });

  it('setSession stores the profile; clearSession removes it', () => {
    service.setSession({ userId: 'admin', userName: '管理員', accessToken: makeToken({ role: ['Admin'] }) });

    expect(sessionStorage.getItem('cms-auth')).not.toBeNull();
    expect(service.userName()).toBe('管理員');
    expect(service.isAuthenticated()).toBeTrue();

    service.clearSession();

    expect(sessionStorage.getItem('cms-auth')).toBeNull();
    expect(service.userName()).toBe('');
    expect(service.isAuthenticated()).toBeFalse();
  });

  it('decodes roles from the token — array, single string, and none', () => {
    service.setSession({ userId: 'a', userName: 'a', accessToken: makeToken({ role: ['Admin', 'Editor'] }) });
    expect(service.roles()).toEqual(['Admin', 'Editor']);
    expect(service.hasRole('Admin')).toBeTrue();

    service.setSession({ userId: 'a', userName: 'a', accessToken: makeToken({ role: 'Editor' }) });
    expect(service.roles()).toEqual(['Editor']);
    expect(service.hasRole('Admin')).toBeFalse();

    service.setSession({ userId: 'a', userName: 'a', accessToken: makeToken({}) });
    expect(service.roles()).toEqual([]);
  });

  it('treats an expired token as signed-out', () => {
    const past = Math.floor(Date.now() / 1000) - 60;
    service.setSession({ userId: 'a', userName: 'a', accessToken: makeToken({ role: ['Admin'], exp: past }) });
    expect(service.isAuthenticated()).toBeFalse();
  });
});
