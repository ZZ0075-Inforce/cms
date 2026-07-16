import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';

import { PublishStatusService } from '@core/services/publish-status.service';
import { PublishStatus } from '@core/models/publish-status.model';
import { RowAuditBadge } from '@shared/row-audit-badge/row-audit-badge';

@Component({
  selector: 'app-publish-status-detail',
  imports: [RouterLink, ButtonModule, RowAuditBadge],
  templateUrl: './publish-status-detail.html',
  styleUrl: './publish-status-detail.scss'
})
export class PublishStatusDetail implements OnInit {
  private readonly service = inject(PublishStatusService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);

  protected readonly status = signal<PublishStatus | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);

  ngOnInit(): void {
    // pkid is a tinyint (0–255); a route id outside that range can never be a real record.
    const pkid = Number(this.route.snapshot.paramMap.get('id'));
    if (!Number.isInteger(pkid) || pkid < 0 || pkid > 255) {
      this.notFound.set(true);
      this.loading.set(false);
      return;
    }

    // No forkJoin: PublishStatus has no foreign keys, so there is no lookup to resolve.
    this.service.getById(pkid).subscribe({
      next: status => {
        this.status.set(status);
        this.loading.set(false);
      },
      error: () => {
        this.notFound.set(true);
        this.loading.set(false);
      }
    });
  }

  protected confirmDelete(): void {
    const status = this.status();
    if (!status) return;

    this.confirmationService.confirm({
      header: '刪除上架狀態',
      message: `確定要刪除主代碼 <b>${status.pkid}</b>「${status.description}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () =>
        this.service.remove(status.pkid).subscribe({
          next: () => {
            this.messageService.add({
              severity: 'success',
              summary: '刪除成功',
              detail: `上架狀態「${status.description}」已刪除。`
            });
            this.router.navigate(['/publish-statuses']);
          },
          error: (error: HttpErrorResponse) =>
            this.messageService.add({
              severity: 'error',
              summary: error.status === 409 ? '上架狀態使用中' : '刪除失敗',
              detail: error.status === 409
                ? (error.error?.detail ?? `上架狀態「${status.description}」仍被課程使用，無法刪除。`)
                : `無法刪除上架狀態「${status.description}」。`
            })
        })
    });
  }
}
