import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';

import { PartnerService } from '@core/services/partner.service';
import { Partner } from '@core/models/partner.model';

@Component({
  selector: 'app-partner-detail',
  imports: [RouterLink, ButtonModule],
  templateUrl: './partner-detail.html',
  styleUrl: './partner-detail.scss'
})
export class PartnerDetail implements OnInit {
  private readonly service = inject(PartnerService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);

  protected readonly partner = signal<Partner | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);

  ngOnInit(): void {
    const pkid = Number(this.route.snapshot.paramMap.get('id'));
    if (!Number.isInteger(pkid) || pkid <= 0) {
      this.notFound.set(true);
      this.loading.set(false);
      return;
    }

    // No forkJoin: Partner has no foreign keys, so there is no lookup to resolve.
    this.service.getById(pkid).subscribe({
      next: partner => {
        this.partner.set(partner);
        this.loading.set(false);
      },
      error: () => {
        this.notFound.set(true);
        this.loading.set(false);
      }
    });
  }

  protected confirmDelete(): void {
    const partner = this.partner();
    if (!partner) return;

    this.confirmationService.confirm({
      header: '刪除廠商',
      message: `確定要刪除主代碼 <b>${partner.pkid}</b>「${partner.name}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () =>
        this.service.remove(partner.pkid).subscribe({
          next: () => {
            this.messageService.add({
              severity: 'success',
              summary: '刪除成功',
              detail: `廠商「${partner.name}」已刪除。`
            });
            this.router.navigate(['/partners']);
          },
          error: (error: HttpErrorResponse) =>
            this.messageService.add({
              severity: 'error',
              summary: error.status === 409 ? '廠商使用中' : '刪除失敗',
              detail: error.status === 409
                ? (error.error?.detail ?? `廠商「${partner.name}」仍被其他資料使用，無法刪除。`)
                : `無法刪除廠商「${partner.name}」。`
            })
        })
    });
  }
}
