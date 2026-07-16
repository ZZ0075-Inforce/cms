import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';

import { CourseGroupService } from '@core/services/course-group.service';
import { CourseGroup } from '@core/models/course-group.model';
import { RowAuditBadge } from '@shared/row-audit-badge/row-audit-badge';

@Component({
  selector: 'app-course-group-detail',
  imports: [RouterLink, ButtonModule, RowAuditBadge],
  templateUrl: './course-group-detail.html',
  styleUrl: './course-group-detail.scss'
})
export class CourseGroupDetail implements OnInit {
  private readonly service = inject(CourseGroupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);

  protected readonly group = signal<CourseGroup | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);

  ngOnInit(): void {
    const pkid = Number(this.route.snapshot.paramMap.get('id'));
    if (!Number.isInteger(pkid) || pkid <= 0) {
      this.notFound.set(true);
      this.loading.set(false);
      return;
    }

    // No forkJoin: CourseGroup has no foreign keys, so there is no lookup to resolve.
    this.service.getById(pkid).subscribe({
      next: group => {
        this.group.set(group);
        this.loading.set(false);
      },
      error: () => {
        this.notFound.set(true);
        this.loading.set(false);
      }
    });
  }

  protected confirmDelete(): void {
    const group = this.group();
    if (!group) return;

    // Same cascade warning as the list: deleting the group also deletes its courses.
    const warning = group.courseCount > 0
      ? `<div style="margin-top:.6rem;color:#dc2626;font-weight:600">此群組下的 ${group.courseCount} 門課程將一併被刪除，且無法復原。</div>`
      : '';

    this.confirmationService.confirm({
      header: '刪除課程群組',
      message: `確定要刪除主代碼 <b>${group.pkid}</b>「${group.description}」？${warning}`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () =>
        this.service.remove(group.pkid).subscribe({
          next: () => {
            this.messageService.add({
              severity: 'success',
              summary: '刪除成功',
              detail: `課程群組「${group.description}」已刪除。`
            });
            this.router.navigate(['/course-groups']);
          },
          error: (error: HttpErrorResponse) =>
            this.messageService.add({
              severity: 'error',
              summary: error.status === 409 ? '群組使用中' : '刪除失敗',
              detail: error.status === 409
                ? (error.error?.detail ?? `課程群組「${group.description}」仍被廠商群組使用，無法刪除。`)
                : `無法刪除課程群組「${group.description}」。`
            })
        })
    });
  }
}
