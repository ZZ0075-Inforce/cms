import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ChipModule } from 'primeng/chip';
import { TagModule } from 'primeng/tag';

import { AppUserService } from '@core/services/app-user.service';
import { LookupService } from '@core/services/lookup.service';
import { AppUser } from '@core/models/app-user.model';
import { AppRoleLookup, appRoleLabel } from '@core/models/app-role.model';
import { RowAuditBadge } from '@shared/row-audit-badge/row-audit-badge';

@Component({
  selector: 'app-user-detail',
  imports: [DatePipe, RouterLink, ButtonModule, ChipModule, TagModule, RowAuditBadge],
  templateUrl: './app-user-detail.html',
  styleUrl: './app-user-detail.scss'
})
export class AppUserDetail implements OnInit {
  private readonly service = inject(AppUserService);
  private readonly lookupService = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);

  protected readonly user = signal<AppUser | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  /** Assigned roles rendered as "RoleName (RoleId)" labels rather than raw ids. */
  protected readonly roleLabels = signal<string[]>([]);

  ngOnInit(): void {
    const userId = this.route.snapshot.paramMap.get('id');
    if (!userId) {
      this.notFound.set(true);
      this.loading.set(false);
      return;
    }

    forkJoin({
      user: this.service.getById(userId),
      roles: this.lookupService.appRoles().pipe(catchError(() => of([] as AppRoleLookup[])))
    }).subscribe({
      next: ({ user, roles }) => {
        this.user.set(user);
        this.roleLabels.set(toRoleLabels(user.roleIds ?? [], roles));
        this.loading.set(false);
      },
      error: () => {
        this.notFound.set(true);
        this.loading.set(false);
      }
    });
  }

  protected confirmDelete(): void {
    const user = this.user();
    if (!user) return;

    this.confirmationService.confirm({
      header: '刪除使用者',
      message: `確定要刪除主代碼 <b>${user.pkid}</b>「${user.userId}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () =>
        this.service.remove(user.userId).subscribe({
          next: () => {
            this.messageService.add({
              severity: 'success',
              summary: '刪除成功',
              detail: `使用者「${user.userId}」已刪除。`
            });
            this.router.navigate(['/app-users']);
          },
          error: () =>
            this.messageService.add({
              severity: 'error',
              summary: '刪除失敗',
              detail: `無法刪除使用者「${user.userId}」。`
            })
        })
    });
  }

  protected confirmResetPassword(): void {
    const user = this.user();
    if (!user) return;

    this.confirmationService.confirm({
      header: '重設密碼',
      message: `確定要將使用者「${user.userId}」的密碼重設為系統預設密碼？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '重設',
      rejectLabel: '取消',
      accept: () =>
        this.service.resetPassword(user.userId).subscribe({
          next: () => {
            this.messageService.add({
              severity: 'success',
              summary: '重設成功',
              detail: `使用者「${user.userId}」的密碼已重設為系統預設密碼。`
            });
            // Refresh so 密碼更新時間 reflects the reset.
            this.service.getById(user.userId).subscribe(fresh => this.user.set(fresh));
          },
          error: () =>
            this.messageService.add({
              severity: 'error',
              summary: '重設失敗',
              detail: `無法重設使用者「${user.userId}」的密碼。`
            })
        })
    });
  }
}

/** Falls back to the raw roleId when a role is missing from the lookup (e.g. deleted). */
function toRoleLabels(roleIds: string[], roles: AppRoleLookup[]): string[] {
  const byId = new Map(roles.map(role => [role.roleId, role]));
  return roleIds.map(id => {
    const role = byId.get(id);
    return role ? appRoleLabel(role) : id;
  });
}
