import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { ConfirmationService, MessageService } from 'primeng/api';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { DatePickerModule } from 'primeng/datepicker';
import { CheckboxModule } from 'primeng/checkbox';
import { TooltipModule } from 'primeng/tooltip';

import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { Course, CourseQuery, CourseRequest, EMPTY_COURSE_QUERY } from '@core/models/course.model';
import { toIso, fromIso } from '@core/utils/date.util';

/** The list columns that can be edited inline — every column except pkid / partnerName / courseGroupName. */
type EditableField =
  | 'displayOrder' | 'courseId' | 'prodCourseId' | 'title' | 'publishStatusPkid'
  | 'scheduleOn' | 'scheduleOff' | 'hour' | 'listPrice' | 'learningCredit' | 'canRepeat';

const EDITABLE_FIELDS: ReadonlySet<EditableField> = new Set<EditableField>([
  'displayOrder', 'courseId', 'prodCourseId', 'title', 'publishStatusPkid',
  'scheduleOn', 'scheduleOff', 'hour', 'listPrice', 'learningCredit', 'canRepeat'
]);

const FILTERS_KEY = 'course-list-filters';
const SORT_KEY = 'course-list-sort';
const PAGE_KEY = 'course-list-page';

interface SortState { sortField: string; sortOrder: number; }
interface PageState { first: number; rows: number; }
interface Option { pkid: number; label: string; }

/** UI-side filter shape: date controls bind `Date`, the API/query shape uses ISO strings. */
interface DraftFilters {
  keyword: string | null;
  partnerPkid: number | null;
  courseGroupPkid: number | null;
  publishStatusPkid: number | null;
  canRepeat: boolean | null;
  scheduleOnFrom: Date | null;
  scheduleOnTo: Date | null;
  scheduleOffFrom: Date | null;
  scheduleOffTo: Date | null;
}

const EMPTY_DRAFT: DraftFilters = {
  keyword: null, partnerPkid: null, courseGroupPkid: null, publishStatusPkid: null,
  canRepeat: null, scheduleOnFrom: null, scheduleOnTo: null, scheduleOffFrom: null, scheduleOffTo: null
};

@Component({
  selector: 'app-course-list',
  imports: [
    DatePipe, FormsModule, RouterLink,
    TableModule, ButtonModule, DrawerModule, InputTextModule, InputNumberModule, SelectModule,
    DatePickerModule, CheckboxModule, TooltipModule
  ],
  templateUrl: './course-list.html',
  styleUrl: './course-list.scss'
})
export class CourseList implements OnInit {
  private readonly service = inject(CourseService);
  private readonly lookupService = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);

  protected readonly courses = signal<Course[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterDrawerVisible = signal(false);

  protected readonly partnerOptions = signal<Option[]>([]);
  protected readonly courseGroupOptions = signal<Option[]>([]);
  protected readonly publishStatusOptions = signal<Option[]>([]);
  protected readonly canRepeatOptions = [
    { label: '全部', value: null },
    { label: '是', value: true },
    { label: '否', value: false }
  ];

  protected draftFilters: DraftFilters = { ...EMPTY_DRAFT };
  protected appliedFilters: CourseQuery = { ...EMPTY_COURSE_QUERY };

  protected sort: SortState = { sortField: 'displayOrder', sortOrder: 1 };
  protected page: PageState = { first: 0, rows: 20 };

  ngOnInit(): void {
    this.restoreState();

    // A partnerPkid in the URL (cross-entity navigation from Partner) overrides any saved filter.
    const incomingPartner = this.route.snapshot.queryParamMap.get('partnerPkid');
    if (incomingPartner) {
      const pkid = Number(incomingPartner);
      this.appliedFilters = { ...this.appliedFilters, partnerPkid: pkid };
      this.draftFilters = { ...this.draftFilters, partnerPkid: pkid };
      this.persistFilters();
    }

    // Lookups populate the drawer selects; the grid shows the JOIN labels the API already returns.
    forkJoin({
      partners: this.lookupService.partners().pipe(catchError(() => of([]))),
      courseGroups: this.lookupService.courseGroups().pipe(catchError(() => of([]))),
      publishStatuses: this.lookupService.publishStatuses().pipe(catchError(() => of([])))
    }).subscribe(({ partners, courseGroups, publishStatuses }) => {
      this.partnerOptions.set(partners.map(p => ({ pkid: p.pkid, label: p.name })));
      this.courseGroupOptions.set(courseGroups.map(g => ({ pkid: g.pkid, label: g.description })));
      this.publishStatusOptions.set(publishStatuses.map(s => ({ pkid: s.pkid, label: s.description })));
    });

    this.load();
  }

  protected get hasActiveFilters(): boolean {
    return JSON.stringify(this.appliedFilters) !== JSON.stringify(EMPTY_COURSE_QUERY);
  }

  protected load(): void {
    this.loading.set(true);
    this.service.query(this.appliedFilters).subscribe({
      next: courses => {
        this.courses.set(courses);
        this.loading.set(false);
      },
      error: () => {
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法載入課程清單，請稍後再試。'
        });
        this.loading.set(false);
      }
    });
  }

  protected applyFilters(): void {
    this.appliedFilters = toQuery(this.draftFilters);
    this.page = { ...this.page, first: 0 };
    this.persistFilters();
    this.persistPage();
    this.filterDrawerVisible.set(false);
    this.load();
  }

  protected clearFilters(): void {
    this.draftFilters = { ...EMPTY_DRAFT };
    this.applyFilters();
  }

  protected onSort(event: { field?: string | string[] | null; order?: number | null }): void {
    const field = Array.isArray(event.field) ? event.field[0] : event.field;
    if (!field) return;
    this.sort = { sortField: field, sortOrder: event.order ?? 1 };
    sessionStorage.setItem(SORT_KEY, JSON.stringify(this.sort));
  }

  protected onPage(event: TableLazyLoadEvent | { first?: number; rows?: number }): void {
    this.page = { first: event.first ?? 0, rows: event.rows ?? this.page.rows };
    this.persistPage();
  }

  protected confirmDelete(course: Course): void {
    this.confirmationService.confirm({
      header: '刪除課程',
      message: `確定要刪除主代碼 <b>${course.pkid}</b>「${course.courseId} ${course.title}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.remove(course)
    });
  }

  private remove(course: Course): void {
    this.service.remove(course.pkid).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: '刪除成功',
          detail: `課程「${course.title}」已刪除。`
        });
        this.load();
      },
      error: (error: HttpErrorResponse) =>
        this.messageService.add({
          severity: 'error',
          // 409 = FAQ / related links / hot-course still point here. Say so; a bare 失敗 is useless.
          summary: error.status === 409 ? '課程使用中' : '刪除失敗',
          detail: error.status === 409
            ? (error.error?.detail ?? `課程「${course.title}」仍被其他資料使用，無法刪除。`)
            : `無法刪除課程「${course.title}」。`
        })
    });
  }

  protected view(course: Course): void {
    this.router.navigate(['/courses', course.pkid]);
  }

  protected edit(course: Course): void {
    this.router.navigate(['/courses', course.pkid, 'edit']);
  }

  // ---------- inline cell editing ----------

  /** The cell currently in edit mode (row pkid + column), or null. Only one cell edits at a time. */
  protected readonly editing = signal<{ pkid: number; field: EditableField } | null>(null);
  /** Inline validation message for the editing cell; while set, the cell stays open. */
  protected readonly editError = signal<string | null>(null);
  /** The in-flight editor value; its type varies by column (text / number / Date / boolean). */
  protected editDraft: string | number | boolean | Date | null = null;
  /** Guards against a second blur committing while a save is already in flight. */
  private savingEdit = false;

  protected isEditing(course: Course, field: EditableField): boolean {
    const cell = this.editing();
    return cell !== null && cell.pkid === course.pkid && cell.field === field;
  }

  /** Double-click handler: opens the editor for an editable cell. Read-only columns are ignored. */
  protected startEdit(course: Course, field: EditableField): void {
    if (!EDITABLE_FIELDS.has(field) || this.savingEdit) return;
    this.editError.set(null);
    this.editDraft =
      field === 'scheduleOn' ? fromIso(course.scheduleOn)
      : field === 'scheduleOff' ? fromIso(course.scheduleOff)
      : (course[field] as string | number | boolean);
    this.editing.set({ pkid: course.pkid, field });
  }

  /**
   * Blur handler: validates, then persists via the existing update endpoint. On a validation failure
   * the cell stays open with an inline error; on a save failure the row reverts to its previous value.
   */
  protected commitEdit(course: Course, field: EditableField): void {
    const cell = this.editing();
    if (!cell || cell.pkid !== course.pkid || cell.field !== field || this.savingEdit) return;

    const value = this.editDraft;
    const error = validateEdit(course, field, value);
    if (error) {
      this.editError.set(error);   // keep the cell open — do not persist
      return;
    }

    // Fetch the full record first: list rows carry no N-N pkids, so a PUT built from the row alone
    // would wipe the course's certifications / job categories. getById restores them.
    this.savingEdit = true;
    this.service.getById(course.pkid).subscribe({
      next: full => {
        this.service.update(buildRequest(full, field, value)).subscribe({
          next: () => {
            this.applyToRow(course, field, value);
            this.finishEdit();
            this.messageService.add({
              severity: 'success', summary: '已更新', detail: `「${course.title}」已儲存。`
            });
          },
          error: (err: HttpErrorResponse) => {
            this.finishEdit();   // revert: the in-memory row was never mutated
            this.messageService.add({
              severity: 'error', summary: '儲存失敗',
              detail: err.error?.detail ?? '無法儲存變更，已還原為原值。'
            });
          }
        });
      },
      error: () => {
        this.finishEdit();
        this.messageService.add({
          severity: 'error', summary: '儲存失敗', detail: '無法載入課程資料，已還原為原值。'
        });
      }
    });
  }

  private finishEdit(): void {
    this.savingEdit = false;
    this.editing.set(null);
    this.editError.set(null);
  }

  /** Writes the saved value onto the in-memory row (plus the status label for the dropdown). */
  private applyToRow(
    course: Course, field: EditableField, value: string | number | boolean | Date | null): void {
    switch (field) {
      case 'scheduleOn': course.scheduleOn = toIso(value as Date)!; break;
      case 'scheduleOff': course.scheduleOff = toIso(value as Date)!; break;
      case 'title': course.title = (value as string).trim(); break;
      case 'courseId': course.courseId = (value as string).trim(); break;
      case 'prodCourseId': course.prodCourseId = (value as string).trim(); break;
      case 'displayOrder': course.displayOrder = value as number; break;
      case 'hour': course.hour = value as number; break;
      case 'listPrice': course.listPrice = value as number; break;
      case 'learningCredit': course.learningCredit = value as number; break;
      case 'canRepeat': course.canRepeat = value as boolean; break;
      case 'publishStatusPkid':
        course.publishStatusPkid = value as number;
        course.publishStatusName =
          this.publishStatusOptions().find(o => o.pkid === value)?.label ?? course.publishStatusName;
        break;
    }
    // New array reference so the p-table re-renders the mutated row.
    this.courses.update(list => [...list]);
  }

  // ---------- session state ----------

  private restoreState(): void {
    const filters = readJson<CourseQuery>(FILTERS_KEY);
    if (filters) {
      this.appliedFilters = { ...EMPTY_COURSE_QUERY, ...filters };
      this.draftFilters = toDraft(this.appliedFilters);
    }

    const sort = readJson<SortState>(SORT_KEY);
    if (sort?.sortField) this.sort = sort;

    const page = readJson<PageState>(PAGE_KEY);
    if (page) this.page = { first: page.first ?? 0, rows: page.rows || 20 };
  }

  private persistFilters(): void {
    sessionStorage.setItem(FILTERS_KEY, JSON.stringify(this.appliedFilters));
  }

  private persistPage(): void {
    sessionStorage.setItem(PAGE_KEY, JSON.stringify(this.page));
  }
}

/** Draft (Date pickers) → query DTO (ISO strings + trimmed keyword, blanks to null). */
function toQuery(draft: DraftFilters): CourseQuery {
  const keyword = draft.keyword?.trim();
  return {
    keyword: keyword ? keyword : null,
    partnerPkid: draft.partnerPkid,
    courseGroupPkid: draft.courseGroupPkid,
    publishStatusPkid: draft.publishStatusPkid,
    canRepeat: draft.canRepeat,
    scheduleOnFrom: toIso(draft.scheduleOnFrom),
    scheduleOnTo: toIso(draft.scheduleOnTo),
    scheduleOffFrom: toIso(draft.scheduleOffFrom),
    scheduleOffTo: toIso(draft.scheduleOffTo)
  };
}

/** Query DTO (ISO strings) → draft (Date pickers), for restoring saved filters into the drawer. */
function toDraft(query: CourseQuery): DraftFilters {
  return {
    keyword: query.keyword,
    partnerPkid: query.partnerPkid,
    courseGroupPkid: query.courseGroupPkid,
    publishStatusPkid: query.publishStatusPkid,
    canRepeat: query.canRepeat,
    scheduleOnFrom: fromIso(query.scheduleOnFrom),
    scheduleOnTo: fromIso(query.scheduleOnTo),
    scheduleOffFrom: fromIso(query.scheduleOffFrom),
    scheduleOffTo: fromIso(query.scheduleOffTo)
  };
}

/** Validates an inline edit before it is persisted. Returns an error message, or null when valid. */
function validateEdit(
  course: Course, field: EditableField, value: string | number | boolean | Date | null): string | null {
  switch (field) {
    case 'title':
    case 'courseId':
    case 'prodCourseId':
      return typeof value === 'string' && value.trim().length > 0 ? null : '此欄位為必填';
    case 'displayOrder':
    case 'hour':
    case 'listPrice':
    case 'learningCredit':
      return typeof value === 'number' && Number.isFinite(value) && value >= 0
        ? null : '請輸入有效的非負數字';
    case 'scheduleOn': {
      if (!(value instanceof Date) || Number.isNaN(value.getTime())) return '請輸入有效的日期';
      const off = fromIso(course.scheduleOff);
      return off && value > off ? '上架日期不可晚於下架日期' : null;
    }
    case 'scheduleOff': {
      if (!(value instanceof Date) || Number.isNaN(value.getTime())) return '請輸入有效的日期';
      const on = fromIso(course.scheduleOn);
      return on && value < on ? '上架日期不可晚於下架日期' : null;
    }
    case 'publishStatusPkid':
      return typeof value === 'number' ? null : '請選擇上架狀態';
    case 'canRepeat':
      return null;   // a checkbox is always valid
  }
}

/** Builds a full CourseRequest from the fetched record, overriding only the edited field. */
function buildRequest(
  c: Course, field: EditableField, value: string | number | boolean | Date | null): CourseRequest {
  const request: CourseRequest = {
    pkid: c.pkid,
    title: c.title,
    officialTitle: c.officialTitle,
    courseId: c.courseId,
    prodCourseId: c.prodCourseId,
    friendlyUrl: c.friendlyUrl,
    displayOrder: c.displayOrder,
    partnerPkid: c.partnerPkid,
    courseGroupPkid: c.courseGroupPkid,
    publishStatusPkid: c.publishStatusPkid,
    scheduleOn: c.scheduleOn,
    scheduleOff: c.scheduleOff,
    hour: c.hour,
    listPrice: c.listPrice,
    learningCredit: c.learningCredit,
    material: c.material,
    objective: c.objective,
    target: c.target,
    prerequisites: c.prerequisites,
    outline: c.outline,
    towardCertOrExam: c.towardCertOrExam,
    note: c.note,
    otherInfo: c.otherInfo,
    canRepeat: c.canRepeat,
    certificationPkids: c.certificationPkids ?? [],   // preserved from the fetched record
    jobCategoryPkids: c.jobCategoryPkids ?? []
  };
  switch (field) {
    case 'scheduleOn': request.scheduleOn = toIso(value as Date)!; break;
    case 'scheduleOff': request.scheduleOff = toIso(value as Date)!; break;
    case 'title': request.title = (value as string).trim(); break;
    case 'courseId': request.courseId = (value as string).trim(); break;
    case 'prodCourseId': request.prodCourseId = (value as string).trim(); break;
    case 'displayOrder': request.displayOrder = value as number; break;
    case 'publishStatusPkid': request.publishStatusPkid = value as number; break;
    case 'hour': request.hour = value as number; break;
    case 'listPrice': request.listPrice = value as number; break;
    case 'learningCredit': request.learningCredit = value as number; break;
    case 'canRepeat': request.canRepeat = value as boolean; break;
  }
  return request;
}

function readJson<T>(key: string): T | null {
  const raw = sessionStorage.getItem(key);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as T;
  } catch {
    return null;   // a corrupt entry must not brick the page
  }
}
