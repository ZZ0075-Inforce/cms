import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Client-side mirror of the backend PasswordPolicy (Infrastructure/PasswordPolicy.cs). The server is the
 * authority — this only spares the user a round-trip and shows the same message inline. Keep the two in
 * step: length >= 8 AND at least 3 of the 4 classes (uppercase / lowercase / digit / symbol).
 */

/** The bilingual message shown when the new password fails the complexity policy. */
export const PASSWORD_COMPLEXITY_MESSAGE =
  '密碼長度至少需 8 碼，且內容須至少包含四種字元的其中三種：大寫英文／小寫英文／數字／符號\n' +
  '(Password must be at least 8 characters and contain at least 3 of the 4 classes: ' +
  'uppercase / lowercase / digit / symbol.)';

/** length >= 8 AND at least 3 of { upper, lower, digit, symbol }. "Symbol" = not a letter or digit. */
export function isPasswordComplexEnough(password: string): boolean {
  if (!password || password.length < 8) return false;

  let classes = 0;
  if (/[A-Z]/.test(password)) classes++;
  if (/[a-z]/.test(password)) classes++;
  if (/[0-9]/.test(password)) classes++;
  if (/[^A-Za-z0-9]/.test(password)) classes++;

  return classes >= 3;
}

/** Flags `{ passwordComplexity: true }` when non-empty and too weak; empty is left to `required`. */
export const passwordComplexityValidator: ValidatorFn = (
  control: AbstractControl
): ValidationErrors | null => {
  const value = (control.value as string) ?? '';
  if (value.length === 0) return null;
  return isPasswordComplexEnough(value) ? null : { passwordComplexity: true };
};

/**
 * Group-level validator: flags `{ passwordsMismatch: true }` on the group when the two named controls
 * differ. Applied to the change-password FormGroup so "new" and "confirm" must match.
 */
export function passwordsMatchValidator(newKey: string, confirmKey: string): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const next = group.get(newKey)?.value;
    const confirm = group.get(confirmKey)?.value;
    return next === confirm ? null : { passwordsMismatch: true };
  };
}
