import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { MessageService } from 'primeng/api';

import { authInterceptor } from './auth.interceptor';
import { AuthService } from '@core/services/auth.service';

const TOKEN = `${btoa('{"alg":"HS256"}')}.${btoa('{"role":["Admin"]}')}.sig`;

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let router: Router;
  let auth: AuthService;
  let messageService: MessageService;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        MessageService
      ]
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    auth = TestBed.inject(AuthService);
    messageService = TestBed.inject(MessageService);
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

  it('shows a friendly toast with the safe message on a 500, without redirecting', () => {
    auth.setSession({ userId: 'admin', userName: 'A', accessToken: TOKEN });
    const navigate = spyOn(router, 'navigateByUrl');
    const add = spyOn(messageService, 'add');

    http.get('/api/partners').subscribe({ next: () => {}, error: () => {} });

    const req = httpMock.expectOne('/api/partners');
    req.flush({ message: 'An unexpected error occurred.' }, { status: 500, statusText: 'Server Error' });

    expect(add).toHaveBeenCalledWith(jasmine.objectContaining({
      severity: 'error',
      detail: 'An unexpected error occurred.'
    }));
    expect(navigate).not.toHaveBeenCalled();            // a 500 does not touch the session
    expect(sessionStorage.getItem('cms-auth')).not.toBeNull();
  });

  it('falls back to a generic message when a 500 body carries none, and 401 still redirects', () => {
    const add = spyOn(messageService, 'add');
    auth.setSession({ userId: 'admin', userName: 'A', accessToken: TOKEN });
    const navigate = spyOn(router, 'navigateByUrl');

    // 500 with no message → generic fallback, no redirect.
    http.get('/api/courses').subscribe({ next: () => {}, error: () => {} });
    httpMock.expectOne('/api/courses').flush(null, { status: 503, statusText: 'Unavailable' });
    expect(add).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'error' }));
    expect(navigate).not.toHaveBeenCalled();

    // 401 still clears the session and redirects to Login (unchanged behaviour).
    http.get('/api/courses').subscribe({ next: () => {}, error: () => {} });
    httpMock.expectOne('/api/courses').flush('nope', { status: 401, statusText: 'Unauthorized' });
    expect(sessionStorage.getItem('cms-auth')).toBeNull();
    expect(navigate).toHaveBeenCalledWith('/login');
  });
});
