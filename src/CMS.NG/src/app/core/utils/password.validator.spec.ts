import { FormControl, FormGroup } from '@angular/forms';
import {
  isPasswordComplexEnough,
  passwordComplexityValidator,
  passwordsMatchValidator
} from './password.validator';

describe('password.validator', () => {
  describe('isPasswordComplexEnough', () => {
    it('accepts length >= 8 with at least 3 of the 4 classes', () => {
      expect(isPasswordComplexEnough('Abcdef12')).toBeTrue(); // upper + lower + digit
      expect(isPasswordComplexEnough('abcdef1!')).toBeTrue(); // lower + digit + symbol
      expect(isPasswordComplexEnough('Abcd12!@')).toBeTrue(); // all four
    });

    it('rejects passwords shorter than 8', () => {
      expect(isPasswordComplexEnough('Ab1!')).toBeFalse();
      expect(isPasswordComplexEnough('Abc123')).toBeFalse();
    });

    it('rejects passwords with fewer than 3 classes', () => {
      expect(isPasswordComplexEnough('abcdefgh')).toBeFalse();      // lower only
      expect(isPasswordComplexEnough('abcdefgh12345')).toBeFalse(); // lower + digit only
      expect(isPasswordComplexEnough('!!!!!!!!####')).toBeFalse();  // symbol only
    });

    it('rejects empty input', () => {
      expect(isPasswordComplexEnough('')).toBeFalse();
    });
  });

  describe('passwordComplexityValidator', () => {
    it('flags a weak non-empty password and passes a strong one', () => {
      expect(passwordComplexityValidator(new FormControl('weak'))).toEqual({ passwordComplexity: true });
      expect(passwordComplexityValidator(new FormControl('Abcd12!@'))).toBeNull();
    });

    it('leaves an empty value to the required validator', () => {
      expect(passwordComplexityValidator(new FormControl(''))).toBeNull();
    });
  });

  describe('passwordsMatchValidator', () => {
    const build = (a: string, b: string) =>
      new FormGroup(
        { newPassword: new FormControl(a), confirmPassword: new FormControl(b) },
        { validators: passwordsMatchValidator('newPassword', 'confirmPassword') }
      );

    it('passes when the two controls match, flags when they differ', () => {
      expect(build('Abcd12!@', 'Abcd12!@').errors).toBeNull();
      expect(build('Abcd12!@', 'different').errors).toEqual({ passwordsMismatch: true });
    });
  });
});
