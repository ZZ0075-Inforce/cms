import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ChipModule } from 'primeng/chip';

import { AppRoleService } from '@core/services/app-role.service';
import { LookupService } from '@core/services/lookup.service';
import { AppRole } from '@core/models/app-role.model';
import { AppUserLookup, appUserLabel } from '@core/models/app-user.model';

@Component({
  selector: 'app-role-detail',
  imports: [RouterLink, ButtonModule, ChipModule],
  templateUrl: './app-role-detail.html',
  styleUrl: './app-role-detail.scss'
})
export class AppRoleDetail implements OnInit {
  private readonly service = inject(AppRoleService);
  private readonly lookupService = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);

  protected readonly role = signal<AppRole | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  /** Assigned users rendered as "UserName (UserId)" labels rather than raw ids. */
  protected readonly userLabels = signal<string[]>([]);

  ngOnInit(): void {
    const roleId = this.route.snapshot.paramMap.get('id');
    if (!roleId) {
      this.notFound.set(true);
      this.loading.set(false);
      return;
    }

    forkJoin({
      role: this.service.getById(roleId),
      users: this.lookupService.appUsers().pipe(catchError(() => of([] as AppUserLookup[])))
    }).subscribe({
      next: ({ role, users }) => {
        this.role.set(role);
        this.userLabels.set(toUserLabels(role.userIds ?? [], users));
        this.loading.set(false);
      },
      error: () => {
        this.notFound.set(true);
        this.loading.set(false);
      }
    });
  }

  protected confirmDelete(): void {
    const role = this.role();
    if (!role) return;

    this.confirmationService.confirm({
      header: '刪除角色',
      message: `確定要刪除主代碼 <b>${role.pkid}</b>「${role.roleId}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () =>
        this.service.remove(role.roleId).subscribe({
          next: () => {
            this.messageService.add({
              severity: 'success',
              summary: '刪除成功',
              detail: `角色「${role.roleId}」已刪除。`
            });
            this.router.navigate(['/app-roles']);
          },
          error: () =>
            this.messageService.add({
              severity: 'error',
              summary: '刪除失敗',
              detail: `無法刪除角色「${role.roleId}」。`
            })
        })
    });
  }
}

/** Falls back to the raw userId when a user is missing from the lookup (e.g. deleted). */
function toUserLabels(userIds: string[], users: AppUserLookup[]): string[] {
  const byId = new Map(users.map(user => [user.userId, user]));
  return userIds.map(id => {
    const user = byId.get(id);
    return user ? appUserLabel(user) : id;
  });
}
