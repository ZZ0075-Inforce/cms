import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, of } from 'rxjs';

import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';

import { PartnerService } from '@core/services/partner.service';
import { Partner, PartnerRequest } from '@core/models/partner.model';
import { RowAuditBadge } from '@shared/row-audit-badge/row-audit-badge';

@Component({
  selector: 'app-partner-form',
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule, InputNumberModule, RowAuditBadge],
  templateUrl: './partner-form.html',
  styleUrl: './partner-form.scss'
})
export class PartnerForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(PartnerService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  /** The record's pkid in edit mode; null on create (no history yet, so no badge). */
  protected readonly auditPkid = signal<number | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(50)]],
    // AppKey stays enabled in edit mode: it is not a key, has no UNIQUE constraint, and nothing
    // FKs to it — renaming it orphans nothing. (Contrast AppRole.roleId, which is disabled.)
    appKey: ['', [Validators.required, Validators.maxLength(10)]],
    nameOnPartnerMenu: ['', [Validators.required, Validators.maxLength(200)]],
    nameOnCourseDetailPage: ['', [Validators.required, Validators.maxLength(50)]],
    displayOrder: [0, [Validators.required, Validators.min(0)]],
    imageFilename: this.fb.control<string | null>(null, Validators.maxLength(50))
  });

  /** 0 in new mode; the DB pkid in edit mode. Sent in the body on PUT. */
  private pkid = 0;

  ngOnInit(): void {
    const routeId = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(routeId !== null);

    // No forkJoin: Partner has no foreign keys, so there is no lookup to load in parallel.
    const load$: Observable<Partner | null> = routeId
      ? this.service.getById(Number(routeId))
      : of(null);

    load$.subscribe({
      next: partner => {
        if (partner) this.patchFromPartner(partner);
        this.loading.set(false);
      },
      error: () => {
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法載入廠商資料。'
        });
        this.loading.set(false);
        this.router.navigate(['/partners']);
      }
    });
  }

  private patchFromPartner(partner: Partner): void {
    this.pkid = partner.pkid;
    this.auditPkid.set(partner.pkid);
    this.form.patchValue({
      name: partner.name,
      appKey: partner.appKey,
      nameOnPartnerMenu: partner.nameOnPartnerMenu,
      nameOnCourseDetailPage: partner.nameOnCourseDetailPage,
      displayOrder: partner.displayOrder,
      imageFilename: partner.imageFilename
    });
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    // getRawValue(), NOT value: `value` omits disabled controls. No control is disabled here today,
    // but the convention is load-bearing — the day one is, `value` would silently drop it.
    const raw = this.form.getRawValue();
    const request: PartnerRequest = {
      pkid: this.pkid,
      name: raw.name.trim(),
      appKey: raw.appKey.trim(),
      nameOnPartnerMenu: raw.nameOnPartnerMenu.trim(),
      nameOnCourseDetailPage: raw.nameOnCourseDetailPage.trim(),
      displayOrder: raw.displayOrder,
      imageFilename: raw.imageFilename?.trim() ? raw.imageFilename.trim() : null
    };

    this.saving.set(true);
    // create() yields Partner and update() yields void; widen to a common type so the union of
    // the two Observables stays callable.
    const save$: Observable<unknown> = this.isEdit()
      ? this.service.update(request)
      : this.service.create(request);

    save$.subscribe({
      next: created => {
        this.messageService.add({
          severity: 'success',
          summary: '儲存成功',
          detail: `廠商「${request.name}」已${this.isEdit() ? '更新' : '新增'}。`
        });
        this.saving.set(false);
        // On create the pkid only exists in the response — the request carried 0.
        const pkid = this.isEdit() ? request.pkid : (created as Partner).pkid;
        this.router.navigate(['/partners', pkid]);
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '儲存失敗',
          detail: this.errorDetail(error, request.name)
        });
      }
    });
  }

  private errorDetail(error: HttpErrorResponse, name: string): string {
    if (error.status === 404) return `找不到廠商「${name}」。`;
    return error.error?.detail ?? '請稍後再試。';
  }

  protected cancel(): void {
    this.router.navigate(['/partners']);
  }

  protected invalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.touched || control.dirty);
  }
}
