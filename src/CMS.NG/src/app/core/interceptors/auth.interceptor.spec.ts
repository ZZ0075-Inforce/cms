import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';

import { authInterceptor } from './auth.interceptor';
import { AuthService } from '@core/services/auth.service';

const TOKEN = `${btoa('{"alg":"HS256"}')}.${btoa('{"role":["Admin"]}')}.sig`;

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let router: Router;
  let auth: AuthService;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([])
      ]
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('attaches the Bearer header when a token is in session storage', () => {
    auth.setSession({ userId: 'admin', userName: 'A', accessToken: TOKEN });

    http.get('/api/partners').subscribe();

    const req = httpMock.expectOne('/api/partners');
    expect(req.request.headers.get('Authorization')).toBe(`Bearer ${TOKEN}`);
    req.flush([]);
  });

  it('does not attach a header when there is no token', () => {
    http.get('/api/partners').subscribe();

    const req = httpMock.expectOne('/api/partners');
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush([]);
  });

  it('clears session storage and redirects to /login on a 401', () => {
    auth.setSession({ userId: 'admin', userName: 'A', accessToken: TOKEN });
    const navigate = spyOn(router, 'navigateByUrl');

    http.get('/api/partners').subscribe({ next: () => {}, error: () => {} });

    const req = httpMock.expectOne('/api/partners');
    req.flush('nope', { status: 401, statusText: 'Unauthorized' });

    expect(sessionStorage.getItem('cms-auth')).toBeNull();
    expect(navigate).toHaveBeenCalledWith('/login');
  });

  it('passes a 401 from the login endpoint through without redirecting', () => {
    const navigate = spyOn(router, 'navigateByUrl');

    http.post('http://localhost:5000/api/Auth/login', {}).subscribe({ next: () => {}, error: () => {} });

    const req = httpMock.expectOne('http://localhost:5000/api/Auth/login');
    req.flush('bad', { status: 401, statusText: 'Unauthorized' });

    expect(navigate).not.toHaveBeenCalled();
  });
});
