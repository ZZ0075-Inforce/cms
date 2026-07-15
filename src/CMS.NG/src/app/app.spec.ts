import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { App } from './app';

/** Builds an unsigned JWT carrying the given roles — the shell only reads the `role` claim. */
function makeToken(roles: string[]): string {
  const b64url = (obj: object) =>
    btoa(JSON.stringify(obj)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `${b64url({ alg: 'HS256', typ: 'JWT' })}.${b64url({ role: roles })}.sig`;
}

function seedSession(roles: string[], userName = '管理員'): void {
  sessionStorage.setItem(
    'cms-auth',
    JSON.stringify({ userId: 'admin', userName, accessToken: makeToken(roles) })
  );
}

describe('App shell', () => {
  beforeEach(async () => {
    sessionStorage.clear();
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        // App injects AuthService, which injects HttpClient.
        provideHttpClient(),
        provideHttpClientTesting(),
        // The shell hosts <p-toast> and <p-confirmDialog>, which inject these.
        MessageService,
        ConfirmationService
      ]
    }).compileComponents();
  });

  afterEach(() => sessionStorage.clear());

  it('creates the app', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('shows the 系統管理 Admin group for a user whose roles include Admin', () => {
    seedSession(['Admin']);
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('系統管理 Admin');
    expect(text).toContain('角色 AppRole');

    const link: HTMLAnchorElement | null =
      fixture.nativeElement.querySelector('a.nav-item[href="/app-roles"]');
    expect(link).not.toBeNull();
  });

  it('hides the 系統管理 Admin group for a non-Admin user (but keeps the other groups)', () => {
    seedSession(['Editor']);
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).not.toContain('系統管理 Admin');
    expect(fixture.nativeElement.querySelector('a.nav-item[href="/app-roles"]')).toBeNull();

    // Non-admin groups still render.
    expect(text).toContain('課程管理 Course');
    expect(text).toContain('首頁 Home');
  });

  it('shows the signed-in user name and a logout control', () => {
    seedSession(['Admin'], '王小明');
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('王小明');
    expect(text).toContain('登出');
  });

  it('renders no sidebar when there is no session', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('aside.sidebar')).toBeNull();
  });
});
