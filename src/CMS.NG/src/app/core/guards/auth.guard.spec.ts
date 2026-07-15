import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter
} from '@angular/router';

import { authGuard } from './auth.guard';
import { AuthService } from '@core/services/auth.service';

const TOKEN = `${btoa('{"alg":"HS256"}')}.${btoa('{"role":["Admin"]}')}.sig`;

describe('authGuard', () => {
  let router: Router;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    });
    router = TestBed.inject(Router);
  });

  afterEach(() => sessionStorage.clear());

  function run() {
    return TestBed.runInInjectionContext(() =>
      authGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot)
    );
  }

  it('redirects to /login when there is no token', () => {
    const result = run();
    expect(result instanceof UrlTree).toBeTrue();
    expect(router.serializeUrl(result as UrlTree)).toBe('/login');
  });

  it('allows activation when a valid token is present', () => {
    TestBed.inject(AuthService).setSession({ userId: 'admin', userName: 'A', accessToken: TOKEN });
    expect(run()).toBeTrue();
  });
});
