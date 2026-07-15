import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { CardModule } from 'primeng/card';

import { AppUserService } from '@core/services/app-user.service';
import { LookupService } from '@core/services/lookup.service';
import { AppUser, AppUserRequest } from '@core/models/app-user.model';
import { AppRoleLookup, appRoleLabel } from '@core/models/app-role.model';

/** Mirrors the API's [RegularExpression] on UserId — forbid whitespace and slashes (URL-path safe). */
const USER_ID_PATTERN = /^[^\s/\\]+$/;

interface RoleOption {
  roleId: string;
  label: string;
}

@Component({
  selector: 'app-user-form',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    InputTextModule,
    MultiSelectModule,
    ToggleSwitchModule,
    CardModule
  ],
  templateUrl: './app-user-form.html',
  styleUrl: './app-user-form.scss'
})
export class AppUserForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(AppUserService);
  private readonly lookupService = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly roleOptions = signal<RoleOption[]>([]);

  protected readonly form = this.fb.nonNullable.group({
    userId: ['', [Validators.required, Validators.maxLength(200), Validators.pattern(USER_ID_PATTERN)]],
    userName: ['', [Validators.required, Validators.maxLength(200)]],
    // Matches the DB default (DF_AppUser_IsActive = 1).
    isActive: this.fb.nonNullable.control(true),
    roleIds: this.fb.nonNullable.control<string[]>([])
  });

  private userId: string | null = null;

  ngOnInit(): void {
    this.userId = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(this.userId !== null);

    forkJoin({
      roles: this.lookupService.appRoles().pipe(catchError(() => of([] as AppRoleLookup[]))),
      user: this.userId ? this.service.getById(this.userId) : of(null)
    }).subscribe({
      next: ({ roles, user }) => {
        this.roleOptions.set(
          roles.map(role => ({ roleId: role.roleId, label: appRoleLabel(role) }))
        );
        if (user) this.patchFromUser(user);
        this.loading.set(false);
      },
      error: () => {
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法載入使用者資料。'
        });
        this.loading.set(false);
        this.router.navigate(['/app-users']);
      }
    });
  }

  private patchFromUser(user: AppUser): void {
    this.form.patchValue({
      userId: user.userId,
      userName: user.userName,
      isActive: user.isActive,
      roleIds: user.roleIds ?? []
    });

    // UserId is the primary key and immutable — renaming it would orphan the AppUserRole rows.
    this.form.controls.userId.disable();
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    // getRawValue(), NOT value: `value` omits disabled controls, so in edit mode userId would be
    // undefined in the payload and the PUT would 404. This is guarded by a unit test.
    const raw = this.form.getRawValue();
    const request: AppUserRequest = {
      userId: raw.userId,
      userName: raw.userName,
      isActive: raw.isActive,
      roleIds: raw.roleIds ?? []
    };

    this.saving.set(true);
    // create() yields AppUser and update() yields void; widen to a common type so the union of
    // the two Observables stays callable.
    const save$: Observable<unknown> = this.isEdit()
      ? this.service.update(request)
      : this.service.create(request);

    save$.subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: '儲存成功',
          detail: `使用者「${request.userId}」已${this.isEdit() ? '更新' : '新增'}。`
        });
        this.saving.set(false);
        this.router.navigate(['/app-users', request.userId]);
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'error',
          summary: error.status === 409 ? '使用者代碼重複' : '儲存失敗',
          detail: this.errorDetail(error, request.userId)
        });
      }
    });
  }

  private errorDetail(error: HttpErrorResponse, userId: string): string {
    if (error.status === 409) return `使用者代碼「${userId}」已存在，請改用其他代碼。`;
    if (error.status === 404) return `找不到使用者「${userId}」。`;
    return error.error?.detail ?? '請稍後再試。';
  }

  protected cancel(): void {
    this.router.navigate(['/app-users']);
  }

  protected invalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.touched || control.dirty);
  }
}
