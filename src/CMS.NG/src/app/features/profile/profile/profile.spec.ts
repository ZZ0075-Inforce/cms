import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { environment } from '@env';

import { Profile } from './profile';
import { AuthService } from '@core/services/auth.service';

/** Builds an unsigned JWT with the given payload — the frontend never verifies the signature. */
function makeToken(payload: object): string {
  const b64url = (obj: object) =>
    btoa(JSON.stringify(obj)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `${b64url({ alg: 'HS256', typ: 'JWT' })}.${b64url(payload)}.sig`;
}

function seedSession(userName = '管理員', roles: string[] = ['Admin', 'Editor']): void {
  sessionStorage.setItem(
    'cms-auth',
    JSON.stringify({ userId: 'admin', userName, accessToken: makeToken({ role: roles }) })
  );
}

function setUserName(fixture: { nativeElement: HTMLElement }, value: string): void {
  const input = fixture.nativeElement.querySelector('#userName') as HTMLInputElement;
  input.value = value;
  input.dispatchEvent(new Event('input'));
}

function submit(fixture: { nativeElement: HTMLElement }): void {
  (fixture.nativeElement.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
}

describe('Profile page', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    sessionStorage.clear();
    seedSession(); // AuthService reads sessionStorage at construction — seed before injecting it.
    await TestBed.configureTestingModule({
      imports: [Profile],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        MessageService
      ]
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('shows UserId and roles read-only, with UserName editable', () => {
    const fixture = TestBed.createComponent(Profile);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;

    // UserId is shown but disabled — cannot be changed.
    const userIdInput = el.querySelector<HTMLInputElement>('#userId')!;
    expect(userIdInput.value).toBe('admin');
    expect(userIdInput.disabled).toBeTrue();

    // Roles are display-only (rendered as chips); there is no control to edit them.
    const text = el.textContent as string;
    expect(text).toContain('Admin');
    expect(text).toContain('Editor');

    // UserName is the one editable field, pre-filled with the current name.
    const userNameInput = el.querySelector<HTMLInputElement>('#userName')!;
    expect(userNameInput.disabled).toBeFalse();
    expect(userNameInput.value).toBe('管理員');
  });

  it('saving PUTs the trimmed UserName and refreshes the shell + session', () => {
    const auth = TestBed.inject(AuthService);
    const fixture = TestBed.createComponent(Profile);
    fixture.detectChanges();

    setUserName(fixture, '  新名字  ');
    submit(fixture);

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/Auth/profile`);
    expect(req.request.method).toBe('PUT');
    // UserId is NOT in the body — only the trimmed UserName.
    expect(req.request.body).toEqual({ userName: '新名字' });
    req.flush({ userId: 'admin', userName: '新名字' });

    // The shell binds to auth.userName(); confirm it and sessionStorage now hold the new name.
    expect(auth.userName()).toBe('新名字');
    expect(JSON.parse(sessionStorage.getItem('cms-auth')!).userName).toBe('新名字');
  });

  it('does not PUT when the UserName is blank', () => {
    const fixture = TestBed.createComponent(Profile);
    fixture.detectChanges();

    setUserName(fixture, '   ');
    submit(fixture);

    httpMock.expectNone(`${environment.apiBaseUrl}/Auth/profile`);
  });
});
