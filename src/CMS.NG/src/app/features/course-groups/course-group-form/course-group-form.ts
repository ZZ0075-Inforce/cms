import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, of } from 'rxjs';

import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';

import { CourseGroupService } from '@core/services/course-group.service';
import { CourseGroup, CourseGroupRequest } from '@core/models/course-group.model';

@Component({
  selector: 'app-course-group-form',
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule],
  templateUrl: './course-group-form.html',
  styleUrl: './course-group-form.scss'
})
export class CourseGroupForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(CourseGroupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);

  protected readonly form = this.fb.nonNullable.group({
    description: ['', [Validators.required, Validators.maxLength(100)]]
  });

  /** 0 in new mode; the DB pkid in edit mode. Sent in the body on PUT. */
  private pkid = 0;

  ngOnInit(): void {
    const routeId = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(routeId !== null);

    // No forkJoin: CourseGroup has no foreign keys, so there is no lookup to load in parallel.
    const load$: Observable<CourseGroup | null> = routeId
      ? this.service.getById(Number(routeId))
      : of(null);

    load$.subscribe({
      next: group => {
        if (group) this.patchFromGroup(group);
        this.loading.set(false);
      },
      error: () => {
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法載入課程群組資料。'
        });
        this.loading.set(false);
        this.router.navigate(['/course-groups']);
      }
    });
  }

  private patchFromGroup(group: CourseGroup): void {
    this.pkid = group.pkid;
    this.form.patchValue({ description: group.description });
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    // getRawValue(), NOT value: `value` omits disabled controls. No control is disabled here today,
    // but the convention is load-bearing — the day one is, `value` would silently drop it.
    const raw = this.form.getRawValue();
    const request: CourseGroupRequest = {
      pkid: this.pkid,
      description: raw.description.trim()
    };

    this.saving.set(true);
    // create() yields CourseGroup and update() yields void; widen to a common type so the union of
    // the two Observables stays callable.
    const save$: Observable<unknown> = this.isEdit()
      ? this.service.update(request)
      : this.service.create(request);

    save$.subscribe({
      next: created => {
        this.messageService.add({
          severity: 'success',
          summary: '儲存成功',
          detail: `課程群組「${request.description}」已${this.isEdit() ? '更新' : '新增'}。`
        });
        this.saving.set(false);
        // On create the pkid only exists in the response — the request carried 0.
        const pkid = this.isEdit() ? request.pkid : (created as CourseGroup).pkid;
        this.router.navigate(['/course-groups', pkid]);
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '儲存失敗',
          detail: this.errorDetail(error, request.description)
        });
      }
    });
  }

  private errorDetail(error: HttpErrorResponse, description: string): string {
    if (error.status === 404) return `找不到課程群組「${description}」。`;
    return error.error?.detail ?? '請稍後再試。';
  }

  protected cancel(): void {
    this.router.navigate(['/course-groups']);
  }

  protected invalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.touched || control.dirty);
  }
}
