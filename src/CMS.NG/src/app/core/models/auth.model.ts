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
