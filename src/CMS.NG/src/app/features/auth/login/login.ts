import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';

import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';

import { AuthService } from '@core/services/auth.service';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule],
  templateUrl: './login.html',
  styleUrl: './login.scss'
})
export class Login {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly submitting = signal(false);

  protected readonly form = this.fb.nonNullable.group({
    userId: ['', [Validators.required]],
    password: ['', [Validators.required]]
  });

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    const { userId, password } = this.form.getRawValue();

    this.auth.login({ userId, password }).subscribe({
      next: profile => {
        this.auth.setSession(profile);
        this.submitting.set(false);
        // Land on the app's default route; the guard now lets us through.
        this.router.navigateByUrl('/');
      },
      error: (error: HttpErrorResponse) => {
        this.submitting.set(false);
        // The API returns a generic 401 that never says which check failed — surface it as-is.
        this.messageService.add({
          severity: 'error',
          summary: error.error?.title ?? '登入失敗',
          detail: error.error?.detail ?? '使用者代碼或密碼錯誤。'
        });
      }
    });
  }

  protected invalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.touched || control.dirty);
  }
}
