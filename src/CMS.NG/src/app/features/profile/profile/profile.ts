import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';

import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { ChipModule } from 'primeng/chip';

import { AuthService } from '@core/services/auth.service';

/**
 * 我的個人資料 — the signed-in user's own profile. UserId and roles are read-only (sourced from the
 * session / token, no extra API call, matching the shell); only UserName is editable. Saving PUTs to
 * /api/Auth/profile, which renames the JWT user server-side, then refreshes the name shown in the shell.
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

  /** Read-only identity, straight from the session profile / token. */
  protected readonly userId = computed(() => this.auth.profile()?.userId ?? '');
  protected readonly roles = this.auth.roles;

  protected readonly form = this.fb.nonNullable.group({
    userName: [this.auth.userName(), [Validators.required]]
  });

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
}
