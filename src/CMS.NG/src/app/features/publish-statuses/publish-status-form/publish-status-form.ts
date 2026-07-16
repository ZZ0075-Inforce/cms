import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, of } from 'rxjs';

import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { ToggleSwitchModule } from 'primeng/toggleswitch';

import { PublishStatusService } from '@core/services/publish-status.service';
import { PublishStatus, PublishStatusRequest } from '@core/models/publish-status.model';
import { RowAuditBadge } from '@shared/row-audit-badge/row-audit-badge';

@Component({
  selector: 'app-publish-status-form',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    InputTextModule,
    InputNumberModule,
    ToggleSwitchModule,
    RowAuditBadge
  ],
  templateUrl: './publish-status-form.html',
  styleUrl: './publish-status-form.scss'
})
export class PublishStatusForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(PublishStatusService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  /** The record's pkid in edit mode; null on create (no history yet, so no badge). */
  protected readonly auditPkid = signal<number | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    // pkid is the primary key AND client-supplied (tinyint, not IDENTITY). Enabled on create so the
    // user picks it; disabled on edit because the key is immutable (Course FKs to it, no ON UPDATE
    // CASCADE). tinyint range is 0–255.
    pkid: [0, [Validators.required, Validators.min(0), Validators.max(255)]],
    description: ['', [Validators.required, Validators.maxLength(50)]],
    isDraft: this.fb.nonNullable.control(false),
    isPublished: this.fb.nonNullable.control(false),
    isDiscontinued: this.fb.nonNullable.control(false)
  });

  ngOnInit(): void {
    const routeId = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(routeId !== null);

    // No forkJoin: PublishStatus has no foreign keys, so there is no lookup to load in parallel.
    const load$: Observable<PublishStatus | null> = routeId
      ? this.service.getById(Number(routeId))
      : of(null);

    load$.subscribe({
      next: status => {
        if (status) this.patchFromStatus(status);
        this.loading.set(false);
      },
      error: () => {
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法載入上架狀態資料。'
        });
        this.loading.set(false);
        this.router.navigate(['/publish-statuses']);
      }
    });
  }

  private patchFromStatus(status: PublishStatus): void {
    this.auditPkid.set(status.pkid);
    this.form.patchValue({
      pkid: status.pkid,
      description: status.description,
      isDraft: status.isDraft,
      isPublished: status.isPublished,
      isDiscontinued: status.isDiscontinued
    });

    // pkid is the primary key and immutable — renaming it would orphan the Course rows.
    this.form.controls.pkid.disable();
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    // getRawValue(), NOT value: `value` omits disabled controls, so in edit mode pkid (the key) would
    // be undefined in the payload and the PUT would 404. This is guarded by a unit test.
    const raw = this.form.getRawValue();
    const request: PublishStatusRequest = {
      pkid: raw.pkid,
      description: raw.description.trim(),
      isDraft: raw.isDraft,
      isPublished: raw.isPublished,
      isDiscontinued: raw.isDiscontinued
    };

    this.saving.set(true);
    // create() yields PublishStatus and update() yields void; widen to a common type so the union of
    // the two Observables stays callable.
    const save$: Observable<unknown> = this.isEdit()
      ? this.service.update(request)
      : this.service.create(request);

    save$.subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: '儲存成功',
          detail: `上架狀態「${request.description}」已${this.isEdit() ? '更新' : '新增'}。`
        });
        this.saving.set(false);
        this.router.navigate(['/publish-statuses', request.pkid]);
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'error',
          summary: error.status === 409 ? '主代碼重複' : '儲存失敗',
          detail: this.errorDetail(error, request)
        });
      }
    });
  }

  private errorDetail(error: HttpErrorResponse, request: PublishStatusRequest): string {
    if (error.status === 409) return `主代碼「${request.pkid}」已存在，請改用其他代碼。`;
    if (error.status === 404) return `找不到主代碼「${request.pkid}」的上架狀態。`;
    return error.error?.detail ?? '請稍後再試。';
  }

  protected cancel(): void {
    this.router.navigate(['/publish-statuses']);
  }

  protected invalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.touched || control.dirty);
  }
}
