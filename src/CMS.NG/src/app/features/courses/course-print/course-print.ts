import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { ButtonModule } from 'primeng/button';
import { ChipModule } from 'primeng/chip';

import { CourseViewService, CourseView } from '@core/services/course-view.service';
import { Course } from '@core/models/course.model';
import { QrCode } from '@shared/qr-code/qr-code';

/** A label/value pair that survived the empty-drop filter. */
interface PrintRow {
  label: string;
  value: string;
}

/**
 * 課程明細另存 PDF — the customer-facing view of one course.
 *
 * This is deliberately NOT 檢視課程 (course-detail) with fields hidden. The internal columns
 * simply do not exist here, so a field added to the admin page later cannot leak onto a document
 * that goes to a customer. What ships out is decided by the two lists below, and nowhere else:
 *
 *   Course record ──► summaryRows() + contentRows()  ──► the sheet
 *                     (an allow-list; everything                │
 *                      not named here never renders)            └─► window.print() → 另存 PDF
 *
 * Deliberately excluded as internal: pkid, displayOrder, friendlyUrl, prodCourseId,
 * publishStatusName, scheduleOn/scheduleOff, note, the Row Audit badge, the action toolbar.
 * See the comments on those lists for the judgement calls.
 *
 * The shell (sidebar/chrome) is stripped at print time by the `@media print` block in the global
 * `src/styles.scss` — a component's styles are view-encapsulated and cannot reach `.app-shell`.
 */
@Component({
  selector: 'app-course-print',
  imports: [RouterLink, ButtonModule, ChipModule, QrCode],
  templateUrl: './course-print.html',
  styleUrl: './course-print.scss'
})
export class CoursePrint implements OnInit {
  private readonly courseView = inject(CourseViewService);
  private readonly route = inject(ActivatedRoute);

  protected readonly view = signal<CourseView | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);

  /**
   * Letterhead on the customer-facing sheet. Taken from the app's own brand mark; confirm the real
   * 抬頭 (and whether a logo image is wanted) with 業務 before this goes to customers.
   */
  protected readonly companyName = 'UWA';

  ngOnInit(): void {
    const pkid = Number(this.route.snapshot.paramMap.get('id'));
    if (!Number.isInteger(pkid) || pkid <= 0) {
      this.notFound.set(true);
      this.loading.set(false);
      return;
    }

    this.courseView.load(pkid).subscribe({
      next: view => {
        this.view.set(view);
        this.loading.set(false);
      },
      error: () => {
        this.notFound.set(true);
        this.loading.set(false);
      }
    });
  }

  /**
   * The scalar facts a customer buys on. `title` is omitted — it is the sheet's heading.
   *
   * `courseId` (AZ-900) stays: it is the code customers quote, and it is already public in the
   * course page URL. `prodCourseId` (科目代碼) is dropped as an internal product code — it appears
   * nowhere public. `publishStatusName` and scheduleOn/scheduleOff are CMS publishing workflow,
   * not class dates, so they are dropped too.
   */
  protected readonly summaryRows = computed<PrintRow[]>(() => {
    const course = this.view()?.course;
    if (!course) return [];

    return present([
      ['官方課程名稱', course.officialTitle],
      ['課程代碼', course.courseId],
      ['原廠', course.partnerName],
      ['課程群組', course.courseGroupName],
      ['時數', course.hour ? `${course.hour} 小時` : null],
      ['定價', course.listPrice != null ? `NT$ ${course.listPrice.toLocaleString('zh-TW')}` : null],
      ['學習點數', course.learningCredit ? `${course.learningCredit}` : null],
      ['可重聽', course.canRepeat ? '是' : '否']
    ]);
  });

  /**
   * The selling content, ordered the way a proposal reads: what you get out of it, then who it is
   * for, then the detail.
   *
   * `note` (備註) is deliberately absent. A free-text "note" on an admin record is exactly where
   * staff park internal remarks, and there is no way to tell a customer-safe note from an unsafe
   * one at render time. If 業務 confirm 備註 is always customer-safe, add it here.
   */
  protected readonly contentRows = computed<PrintRow[]>(() => {
    const course = this.view()?.course;
    if (!course) return [];

    return present([
      ['課程目標', course.objective],
      ['適合對象', course.target],
      ['先備知識', course.prerequisites],
      ['課程大綱', course.outline],
      ['教材', course.material],
      ['考試／認證說明', course.towardCertOrExam],
      ['其他資訊', course.otherInfo]
    ]);
  });

  /** Public course page the QR code points at: /Course/Show/{pkid}/{CourseId}. */
  protected qrUrl(course: Course): string {
    return `https://www.uuu.com.tw/Course/Show/${course.pkid}/${course.courseId}`;
  }

  protected print(): void {
    window.print();
  }
}

/**
 * Keeps only the rows the record actually has something for.
 *
 * 檢視課程 renders a missing value as "—" because an admin needs to see the field exists and is
 * blank. On a customer draft that column of dashes is the noise this view exists to remove, so an
 * empty value drops its whole row.
 */
function present(rows: readonly (readonly [string, string | number | null | undefined])[]): PrintRow[] {
  return rows
    .filter(([, value]) => value !== null && value !== undefined && `${value}`.trim() !== '')
    .map(([label, value]) => ({ label, value: `${value}` }));
}
