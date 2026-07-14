import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { MultiSelectModule } from 'primeng/multiselect';
import { CardModule } from 'primeng/card';

import { AppRoleService } from '@core/services/app-role.service';
import { LookupService } from '@core/services/lookup.service';
import { AppRole, AppRoleRequest } from '@core/models/app-role.model';
import { AppUserLookup, appUserLabel } from '@core/models/app-user.model';

/** Mirrors the API's [RegularExpression] on RoleId — the key must stay URL-path safe. */
const ROLE_ID_PATTERN = /^[A-Za-z0-9_-]+$/;

interface UserOption {
  userId: string;
  label: string;
}

@Component({
  selector: 'app-role-form',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    InputTextModule,
    InputNumberModule,
    MultiSelectModule,
    CardModule
  ],
  templateUrl: './app-role-form.html',
  styleUrl: './app-role-form.scss'
})
export class AppRoleForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(AppRoleService);
  private readonly lookupService = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly userOptions = signal<UserOption[]>([]);

  protected readonly form = this.fb.nonNullable.group({
    roleId: ['', [Validators.required, Validators.maxLength(200), Validators.pattern(ROLE_ID_PATTERN)]],
    roleName: ['', [Validators.required, Validators.maxLength(200)]],
    // 100 matches the DB default (DF_AppRole_Privilege).
    permissionLevel: [100, [Validators.required, Validators.min(0), Validators.max(9999)]],
    // Description is nvarchar(400) NULL — optional, despite the red asterisk in the mockup.
    description: this.fb.control<string | null>(null, Validators.maxLength(400)),
    userIds: this.fb.nonNullable.control<string[]>([])
  });

  private roleId: string | null = null;

  ngOnInit(): void {
    this.roleId = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(this.roleId !== null);

    forkJoin({
      users: this.lookupService.appUsers().pipe(catchError(() => of([] as AppUserLookup[]))),
      role: this.roleId ? this.service.getById(this.roleId) : of(null)
    }).subscribe({
      next: ({ users, role }) => {
        this.userOptions.set(
          users.map(user => ({ userId: user.userId, label: appUserLabel(user) }))
        );
        if (role) this.patchFromRole(role);
        this.loading.set(false);
      },
      error: () => {
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法載入角色資料。'
        });
        this.loading.set(false);
        this.router.navigate(['/app-roles']);
      }
    });
  }

  private patchFromRole(role: AppRole): void {
    this.form.patchValue({
      roleId: role.roleId,
      roleName: role.roleName,
      permissionLevel: role.permissionLevel,
      description: role.description,
      userIds: role.userIds ?? []
    });

    // RoleId is the primary key and immutable — renaming it would orphan the AppUserRole rows.
    this.form.controls.roleId.disable();
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    // getRawValue(), NOT value: `value` omits disabled controls, so in edit mode roleId would be
    // undefined in the payload and the PUT would 404. This is guarded by a unit test.
    const raw = this.form.getRawValue();
    const request: AppRoleRequest = {
      roleId: raw.roleId,
      roleName: raw.roleName,
      permissionLevel: raw.permissionLevel,
      description: raw.description?.trim() ? raw.description.trim() : null,
      userIds: raw.userIds ?? []
    };

    this.saving.set(true);
    // create() yields AppRole and update() yields void; widen to a common type so the union of
    // the two Observables stays callable.
    const save$: Observable<unknown> = this.isEdit()
      ? this.service.update(request)
      : this.service.create(request);

    save$.subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: '儲存成功',
          detail: `角色「${request.roleId}」已${this.isEdit() ? '更新' : '新增'}。`
        });
        this.saving.set(false);
        this.router.navigate(['/app-roles', request.roleId]);
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'error',
          summary: error.status === 409 ? '角色代碼重複' : '儲存失敗',
          detail: this.errorDetail(error, request.roleId)
        });
      }
    });
  }

  private errorDetail(error: HttpErrorResponse, roleId: string): string {
    if (error.status === 409) return `角色代碼「${roleId}」已存在，請改用其他代碼。`;
    if (error.status === 404) return `找不到角色「${roleId}」。`;
    return error.error?.detail ?? '請稍後再試。';
  }

  protected cancel(): void {
    this.router.navigate(['/app-roles']);
  }

  protected invalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.touched || control.dirty);
  }
}
