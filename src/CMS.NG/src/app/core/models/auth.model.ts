/** Credentials posted to POST /api/Auth/login. */
export interface LoginRequest {
  userId: string;
  password: string;
}

/** The login response: the user's profile plus a signed 24-hour HS256 access token. */
export interface LoginResponse {
  userId: string;
  userName: string;
  accessToken: string;
}

/**
 * The signed-in profile persisted to sessionStorage. Same shape as {@link LoginResponse} — roles are
 * NOT stored here; they are decoded from the token's `role` claim on demand (see jwt.util).
 */
export interface AuthProfile {
  userId: string;
  userName: string;
  accessToken: string;
}

/** Body of PUT /api/Auth/profile — only UserName is sent; the target UserId comes from the JWT. */
export interface UpdateProfileRequest {
  userName: string;
}

/** The response from PUT /api/Auth/profile: the authenticated user's id and newly-saved UserName. */
export interface ProfileResponse {
  userId: string;
  userName: string;
}

/**
 * Body of POST /api/Auth/change-password. All three are raw passwords — the server verifies the current
 * one against the stored hash and never accepts or returns a hash. The target UserId comes from the JWT.
 */
export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}
