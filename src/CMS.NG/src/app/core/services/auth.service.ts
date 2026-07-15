import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import { environment } from '@env';
import {
  AuthProfile,
  LoginRequest,
  LoginResponse,
  ProfileResponse,
  UpdateProfileRequest
} from '@core/models/auth.model';
import { decodeExp, decodeRoles } from '@core/utils/jwt.util';

/** sessionStorage key for the signed-in profile (session, not local: cleared when the tab closes). */
const STORAGE_KEY = 'cms-auth';

/**
 * Owns the login session. The profile is held in a signal so the shell (username, logout, role-gated
 * menu) reacts the instant the session changes, and mirrored to sessionStorage so a page reload keeps
 * the user signed in. Roles are derived from the token, never stored separately.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly base = `${environment.apiBaseUrl}/Auth`;

  private readonly profileSig = signal<AuthProfile | null>(readSession());

  readonly profile = this.profileSig.asReadonly();
  readonly userName = computed(() => this.profileSig()?.userName ?? '');
  readonly roles = computed(() => decodeRoles(this.profileSig()?.accessToken));

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.base}/login`, request);
  }

  /** Updates the signed-in user's own UserName (the server takes the UserId from the token). */
  updateProfile(request: UpdateProfileRequest): Observable<ProfileResponse> {
    return this.http.put<ProfileResponse>(`${this.base}/profile`, request);
  }

  setSession(profile: AuthProfile): void {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(profile));
    this.profileSig.set(profile);
  }

  /**
   * Refreshes just the displayed UserName after a profile update, keeping the same token and userId,
   * so the shell and sessionStorage reflect the new name without a re-login. No-op when signed out.
   */
  setUserName(userName: string): void {
    const current = this.profileSig();
    if (!current) return;
    this.setSession({ ...current, userName });
  }

  clearSession(): void {
    sessionStorage.removeItem(STORAGE_KEY);
    this.profileSig.set(null);
  }

  /** Clears the session and returns to the public Login page. */
  logout(): void {
    this.clearSession();
    this.router.navigateByUrl('/login');
  }

  get token(): string | null {
    return this.profileSig()?.accessToken ?? null;
  }

  isAuthenticated(): boolean {
    const profile = this.profileSig();
    if (!profile) return false;
    // An expired token is useless — treat it as signed-out so the guard redirects to login.
    const exp = decodeExp(profile.accessToken);
    return exp === null || exp * 1000 > Date.now();
  }

  hasRole(role: string): boolean {
    return this.roles().includes(role);
  }
}

function readSession(): AuthProfile | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as AuthProfile) : null;
  } catch {
    return null;
  }
}
