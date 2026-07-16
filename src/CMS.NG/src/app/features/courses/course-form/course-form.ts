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
import { SelectModule } from 'primeng/select';
import { MultiSelectModule } from 'primeng/multiselect';
import { DatePickerModule } from 'primeng/datepicker';
import { TextareaModule } from 'primeng/textarea';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { CardModule } from 'primeng/card';

import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { Course, CourseRequest } from '@core/models/course.model';
import { toIso, fromIso, addYears } from '@core/utils/date.util';
import { RowAuditBadge } from '@shared/row-audit-badge/row-audit-badge';

interface Option { pkid: number; label: string; }

@Component({
  selector: 'app-course-form',
  imports: [
    ReactiveFormsModule, ButtonModule, InputTextModule, InputNumberModule, SelectModule,
    MultiSelectModule, DatePickerModule, TextareaModule, ToggleSwitchModule, CardModule,
    RowAuditBadge
  ],
  templateUrl: './course-form.html',
  styleUrl: './course-form.scss'
})
export class CourseForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(CourseService);
  private readonly lookupService = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  /** The record's pkid in edit mode; null on create (no history yet, so no badge). */
  protected readonly auditPkid = signal<number | null>(null);

  protected readonly partnerOptions = signal<Option[]>([]);
  protected readonly courseGroupOptions = signal<Option[]>([]);
  protected readonly publishStatusOptions = signal<Option[]>([]);
  protected readonly certificationOptions = signal<Option[]>([]);
  protected readonly jobCategoryOptions = signal<Option[]>([]);

  protected readonly form = this.fb.group({
    title: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(200)]),
    officialTitle: this.fb.control<string | null>(null, Validators.maxLength(300)),
    courseId: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(50)]),
    prodCourseId: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(50)]),
    friendlyUrl: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(100)]),
    displayOrder: this.fb.nonNullable.control(0, [Validators.required, Validators.min(0)]),
    partnerPkid: this.fb.control<number | null>(null, Validators.required),
    courseGroupPkid: this.fb.control<number | null>(null),
    publishStatusPkid: this.fb.control<number | null>(null, Validators.required),
    scheduleOn: this.fb.control<Date | null>(null, Validators.required),
    scheduleOff: this.fb.control<Date | null>(null, Validators.required),
    hour: this.fb.nonNullable.control(0, [Validators.required, Validators.min(0)]),
    listPrice: this.fb.nonNullable.control(0, [Validators.required, Validators.min(0)]),
    learningCredit: this.fb.nonNullable.control(0, [Validators.required, Validators.min(0)]),
    material: this.fb.control<string | null>(null, Validators.maxLength(500)),
    objective: this.fb.control<string | null>(null, Validators.maxLength(4000)),
    target: this.fb.control<string | null>(null, Validators.maxLength(500)),
    prerequisites: this.fb.control<string | null>(null, Validators.maxLength(4000)),
    outline: this.fb.control<string | null>(null),
    towardCertOrExam: this.fb.control<string | null>(null),
    note: this.fb.control<string | null>(null, Validators.maxLength(4000)),
    otherInfo: this.fb.control<string | null>(null, Validators.maxLength(4000)),
    canRepeat: this.fb.nonNullable.control(false),
    certificationPkids: this.fb.nonNullable.control<number[]>([]),
    jobCategoryPkids: this.fb.nonNullable.control<number[]>([])
  });

  /** 0 in new mode; the DB pkid in edit mode. Sent in the body on PUT. */
  private pkid = 0;

  ngOnInit(): void {
    const routeId = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(routeId !== null);

    // 下架日期 auto-defaults to 上架日期 + 10 年. emitEvent:false stops the loop; in edit mode the
    // loaded scheduleOff (patched right after scheduleOn) overwrites this.
    this.form.controls.scheduleOn.valueChanges.subscribe(value => {
      if (value instanceof Date) {
        this.form.controls.scheduleOff.setValue(addYears(value, 10), { emitEvent: false });
      }
    });

    forkJoin({
      partners: this.lookupService.partners().pipe(catchError(() => of([]))),
      courseGroups: this.lookupService.courseGroups().pipe(catchError(() => of([]))),
      publishStatuses: this.lookupService.publishStatuses().pipe(catchError(() => of([]))),
      certifications: this.lookupService.certifications().pipe(catchError(() => of([]))),
      jobCategories: this.lookupService.jobCategories().pipe(catchError(() => of([]))),
      course: routeId ? this.service.getById(Number(routeId)) : of<Course | null>(null)
    }).subscribe({
      next: ({ partners, courseGroups, publishStatuses, certifications, jobCategories, course }) => {
        this.partnerOptions.set(partners.map(p => ({ pkid: p.pkid, label: p.name })));
        this.courseGroupOptions.set(courseGroups.map(g => ({ pkid: g.pkid, label: g.description })));
        this.publishStatusOptions.set(publishStatuses.map(s => ({ pkid: s.pkid, label: s.description })));
        this.certificationOptions.set(certifications.map(c => ({ pkid: c.pkid, label: c.title })));
        this.jobCategoryOptions.set(jobCategories.map(j => ({ pkid: j.pkid, label: j.description })));
        if (course) this.patchFromCourse(course);
        this.loading.set(false);
      },
      error: () => {
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法載入課程資料。'
        });
        this.loading.set(false);
        this.router.navigate(['/courses']);
      }
    });
  }

  private patchFromCourse(course: Course): void {
    this.pkid = course.pkid;
    this.auditPkid.set(course.pkid);
    // scheduleOn before scheduleOff so the auto-default fires first, then the loaded value wins.
    this.form.patchValue({
      title: course.title,
      officialTitle: course.officialTitle,
      courseId: course.courseId,
      prodCourseId: course.prodCourseId,
      friendlyUrl: course.friendlyUrl,
      displayOrder: course.displayOrder,
      partnerPkid: course.partnerPkid,
      courseGroupPkid: course.courseGroupPkid,
      publishStatusPkid: course.publishStatusPkid,
      scheduleOn: fromIso(course.scheduleOn),
      scheduleOff: fromIso(course.scheduleOff),
      hour: course.hour,
      listPrice: course.listPrice,
      learningCredit: course.learningCredit,
      material: course.material,
      objective: course.objective,
      target: course.target,
      prerequisites: course.prerequisites,
      outline: course.outline,
      towardCertOrExam: course.towardCertOrExam,
      note: course.note,
      otherInfo: course.otherInfo,
      canRepeat: course.canRepeat,
      certificationPkids: course.certificationPkids,
      jobCategoryPkids: course.jobCategoryPkids
    });
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    // getRawValue(), NOT value — the convention guards against a disabled control silently dropping.
    const raw = this.form.getRawValue();
    const request: CourseRequest = {
      pkid: this.pkid,
      title: raw.title.trim(),
      officialTitle: blank(raw.officialTitle),
      courseId: raw.courseId.trim(),
      prodCourseId: raw.prodCourseId.trim(),
      friendlyUrl: raw.friendlyUrl.trim(),
      displayOrder: raw.displayOrder,
      partnerPkid: raw.partnerPkid!,               // required → non-null past validation
      courseGroupPkid: raw.courseGroupPkid,
      publishStatusPkid: raw.publishStatusPkid!,
      scheduleOn: toIso(raw.scheduleOn)!,
      scheduleOff: toIso(raw.scheduleOff)!,
      hour: raw.hour,
      listPrice: raw.listPrice,
      learningCredit: raw.learningCredit,
      material: blank(raw.material),
      objective: blank(raw.objective),
      target: blank(raw.target),
      prerequisites: blank(raw.prerequisites),
      outline: blank(raw.outline),
      towardCertOrExam: blank(raw.towardCertOrExam),
      note: blank(raw.note),
      otherInfo: blank(raw.otherInfo),
      canRepeat: raw.canRepeat,
      certificationPkids: raw.certificationPkids,
      jobCategoryPkids: raw.jobCategoryPkids
    };

    this.saving.set(true);
    const save$: Observable<unknown> = this.isEdit()
      ? this.service.update(request)
      : this.service.create(request);

    save$.subscribe({
      next: created => {
        this.messageService.add({
          severity: 'success',
          summary: '儲存成功',
          detail: `課程「${request.title}」已${this.isEdit() ? '更新' : '新增'}。`
        });
        this.saving.set(false);
        // On create the pkid only exists in the response — the request carried 0.
        const pkid = this.isEdit() ? request.pkid : (created as Course).pkid;
        this.router.navigate(['/courses', pkid]);
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '儲存失敗',
          detail: this.errorDetail(error, request.title)
        });
      }
    });
  }

  private errorDetail(error: HttpErrorResponse, title: string): string {
    if (error.status === 404) return `找不到課程「${title}」。`;
    return error.error?.detail ?? '請稍後再試。';
  }

  protected cancel(): void {
    this.router.navigate(['/courses']);
  }

  protected invalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.touched || control.dirty);
  }
}

/** Blank text lands as null, not an empty string. */
function blank(value: string | null): string | null {
  const trimmed = value?.trim();
  return trimmed ? trimmed : null;
}
