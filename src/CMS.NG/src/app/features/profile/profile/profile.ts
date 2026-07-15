import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';

import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { ChipModule } from 'primeng/chip';

import { AuthService } from '@core/services/auth.service';
import {
  PASSWORD_COMPLEXITY_MESSAGE,
  passwordComplexityValidator,
  passwordsMatchValidator
} from '@core/utils/password.validator';

/**
 * 我的個人資料 — the signed-in user's own profile. UserId and roles are read-only (sourced from the
 * session / token, no extra API call, matching the shell); only UserName is editable. Saving PUTs to
 * /api/Auth/profile, which renames the JWT user server-side, then refreshes the name shown in the shell.
 *
 * A separate 變更密碼 form POSTs to /api/Auth/change-password. It validates the new-password complexity
 * and the confirm-match client-side (mirroring the server), but the server stays the authority and no
 * password hash ever crosses the boundary.
 */
@Component({
  selector: 'app-profile',
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule, ChipModule],
  templateUrl: './profile.html',
  styleUrl: './profile.scss'
})
export class Profile {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly messageService = inject(MessageService);

  protected readonly submitting = signal(false);
  protected readonly changingPassword = signal(false);
  protected readonly complexityMessage = PASSWORD_COMPLEXITY_MESSAGE;

  /** Read-only identity, straight from the session profile / token. */
  protected readonly userId = computed(() => this.auth.profile()?.userId ?? '');
  protected readonly roles = this.auth.roles;

  protected readonly form = this.fb.nonNullable.group({
    userName: [this.auth.userName(), [Validators.required]]
  });

  protected readonly passwordForm = this.fb.nonNullable.group(
    {
      currentPassword: ['', [Validators.required]],
      newPassword: ['', [Validators.required, passwordComplexityValidator]],
      confirmPassword: ['', [Validators.required]]
    },
    { validators: passwordsMatchValidator('newPassword', 'confirmPassword') }
  );

  protected submit(): void {
    // Trim in place first: a whitespace-only name then fails `required` and is never sent.
    const userName = this.form.controls.userName.value.trim();
    this.form.controls.userName.setValue(userName);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.auth.updateProfile({ userName }).subscribe({
      next: profile => {
        // Refresh the shell + sessionStorage with the server-confirmed name.
        this.auth.setUserName(profile.userName);
        this.submitting.set(false);
        this.messageService.add({
          severity: 'success',
          summary: '更新成功',
          detail: '使用者名稱已更新。'
        });
      },
      error: (error: HttpErrorResponse) => {
        this.submitting.set(false);
        this.messageService.add({
          severity: 'error',
          summary: error.error?.title ?? '更新失敗',
          detail: error.error?.detail ?? '無法更新使用者名稱。'
        });
      }
    });
  }

  protected invalid(): boolean {
    const control = this.form.controls.userName;
    return control.invalid && (control.touched || control.dirty);
  }

  protected submitPassword(): void {
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    const { currentPassword, newPassword, confirmPassword } = this.passwordForm.getRawValue();
    this.changingPassword.set(true);
    this.auth.changePassword({ currentPassword, newPassword, confirmPassword }).subscribe({
      next: () => {
        this.changingPassword.set(false);
        this.passwordForm.reset();
        this.messageService.add({
          severity: 'success',
          summary: '變更成功',
          detail: '密碼已更新。'
        });
      },
      error: (error: HttpErrorResponse) => {
        this.changingPassword.set(false);
        // Surface the server's message (e.g. wrong current password, or the complexity rule) as-is.
        this.messageService.add({
          severity: 'error',
          summary: error.error?.title ?? '變更密碼失敗',
          detail: error.error?.detail ?? '無法變更密碼。'
        });
      }
    });
  }

  /** True when a password control should show its error (touched/dirty and invalid for that reason). */
  protected passwordRequired(controlName: 'currentPassword' | 'newPassword' | 'confirmPassword'): boolean {
    const control = this.passwordForm.controls[controlName];
    return control.hasError('required') && (control.touched || control.dirty);
  }

  protected showComplexityError(): boolean {
    const control = this.passwordForm.controls.newPassword;
    return control.hasError('passwordComplexity') && (control.touched || control.dirty);
  }

  protected showMismatchError(): boolean {
    const control = this.passwordForm.controls.confirmPassword;
    // Only once the confirm field is filled — an empty confirm shows its own "required" error instead.
    return this.passwordForm.hasError('passwordsMismatch')
      && !control.hasError('required')
      && (control.touched || control.dirty);
  }
}
