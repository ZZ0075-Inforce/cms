import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { environment } from '@env';

import { Login } from './login';

describe('Login', () => {
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    sessionStorage.clear();
    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        MessageService
      ]
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  function internals(fixture: { componentInstance: Login }) {
    return fixture.componentInstance as unknown as {
      form: { setValue: (v: { userId: string; password: string }) => void };
      submit: () => void;
    };
  }

  it('posts credentials and, on success, stores the session and navigates to /', () => {
    const navigate = spyOn(router, 'navigateByUrl');
    const fixture = TestBed.createComponent(Login);
    const cmp = internals(fixture);
    cmp.form.setValue({ userId: 'admin', password: 'pw' });

    cmp.submit();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/Auth/login`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ userId: 'admin', password: 'pw' });
    req.flush({ userId: 'admin', userName: '管理員', accessToken: 'header.payload.sig' });

    expect(sessionStorage.getItem('cms-auth')).not.toBeNull();
    expect(navigate).toHaveBeenCalledWith('/');
  });

  it('does not call the API when the form is empty', () => {
    const fixture = TestBed.createComponent(Login);
    internals(fixture).submit();
    httpMock.expectNone(`${environment.apiBaseUrl}/Auth/login`);
  });
});
