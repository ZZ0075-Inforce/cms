import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ChipModule } from 'primeng/chip';

import { CourseService } from '@core/services/course.service';
import { CourseViewService } from '@core/services/course-view.service';
import { Course } from '@core/models/course.model';
import { QrCode } from '@shared/qr-code/qr-code';
import { RowAuditBadge } from '@shared/row-audit-badge/row-audit-badge';

@Component({
  selector: 'app-course-detail',
  imports: [DatePipe, RouterLink, ButtonModule, ChipModule, QrCode, RowAuditBadge],
  templateUrl: './course-detail.html',
  styleUrl: './course-detail.scss'
})
export class CourseDetail implements OnInit {
  private readonly service = inject(CourseService);
  private readonly courseView = inject(CourseViewService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);

  protected readonly course = signal<Course | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  /** N-N pkids resolved to labels for the chips. */
  protected readonly certificationLabels = signal<string[]>([]);
  protected readonly jobCategoryLabels = signal<string[]>([]);

  ngOnInit(): void {
    const pkid = Number(this.route.snapshot.paramMap.get('id'));
    if (!Number.isInteger(pkid) || pkid <= 0) {
      this.notFound.set(true);
      this.loading.set(false);
      return;
    }

    this.courseView.load(pkid).subscribe({
      next: ({ course, certificationLabels, jobCategoryLabels }) => {
        this.course.set(course);
        this.certificationLabels.set(certificationLabels);
        this.jobCategoryLabels.set(jobCategoryLabels);
        this.loading.set(false);
      },
      error: () => {
        this.notFound.set(true);
        this.loading.set(false);
      }
    });
  }

  /** Public course page the QR code points at: /Course/Show/{pkid}/{CourseId}. */
  protected qrUrl(course: Course): string {
    return `https://www.uuu.com.tw/Course/Show/${course.pkid}/${course.courseId}`;
  }

  protected confirmDelete(): void {
    const course = this.course();
    if (!course) return;

    this.confirmationService.confirm({
      header: '刪除課程',
      message: `確定要刪除主代碼 <b>${course.pkid}</b>「${course.courseId} ${course.title}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () =>
        this.service.remove(course.pkid).subscribe({
          next: () => {
            this.messageService.add({
              severity: 'success',
              summary: '刪除成功',
              detail: `課程「${course.title}」已刪除。`
            });
            this.router.navigate(['/courses']);
          },
          error: (error: HttpErrorResponse) =>
            this.messageService.add({
              severity: 'error',
              summary: error.status === 409 ? '課程使用中' : '刪除失敗',
              detail: error.status === 409
                ? (error.error?.detail ?? `課程「${course.title}」仍被其他資料使用，無法刪除。`)
                : `無法刪除課程「${course.title}」。`
            })
        })
    });
  }
}
