import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';

import { CourseGroupService } from '@core/services/course-group.service';
import { CourseGroup, CourseGroupQuery, EMPTY_COURSE_GROUP_QUERY } from '@core/models/course-group.model';

const FILTERS_KEY = 'course-group-list-filters';
const SORT_KEY = 'course-group-list-sort';
const PAGE_KEY = 'course-group-list-page';

interface SortState {
  sortField: string;
  sortOrder: number;
}

interface PageState {
  first: number;
  rows: number;
}

@Component({
  selector: 'app-course-group-list',
  imports: [
    FormsModule,
    RouterLink,
    TableModule,
    ButtonModule,
    DrawerModule,
    InputTextModule,
    TooltipModule
  ],
  templateUrl: './course-group-list.html',
  styleUrl: './course-group-list.scss'
})
export class CourseGroupList implements OnInit {
  private readonly service = inject(CourseGroupService);
  private readonly router = inject(Router);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);

  protected readonly groups = signal<CourseGroup[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterDrawerVisible = signal(false);

  /** Bound to the drawer inputs. Only copied into `appliedFilters` when 搜尋 is pressed. */
  protected draftFilters: CourseGroupQuery = { ...EMPTY_COURSE_GROUP_QUERY };
  protected appliedFilters: CourseGroupQuery = { ...EMPTY_COURSE_GROUP_QUERY };

  // No DisplayOrder column; pkid DESC is the list default (newest first).
  protected sort: SortState = { sortField: 'pkid', sortOrder: -1 };
  protected page: PageState = { first: 0, rows: 20 };

  ngOnInit(): void {
    this.restoreState();
    this.load();
  }

  protected get hasActiveFilters(): boolean {
    return this.appliedFilters.keyword !== null;
  }

  protected load(): void {
    this.loading.set(true);
    this.service.query(this.appliedFilters).subscribe({
      next: groups => {
        this.groups.set(groups);
        this.loading.set(false);
      },
      error: () => {
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法載入課程群組清單，請稍後再試。'
        });
        this.loading.set(false);
      }
    });
  }

  protected applyFilters(): void {
    this.appliedFilters = normalizeQuery(this.draftFilters);
    this.page = { ...this.page, first: 0 };   // a new search starts at page 1
    this.persistFilters();
    this.persistPage();
    this.filterDrawerVisible.set(false);
    this.load();
  }

  protected clearFilters(): void {
    this.draftFilters = { ...EMPTY_COURSE_GROUP_QUERY };
    this.applyFilters();
  }

  protected onSort(event: { field?: string | string[] | null; order?: number | null }): void {
    const field = Array.isArray(event.field) ? event.field[0] : event.field;
    if (!field) return;
    this.sort = { sortField: field, sortOrder: event.order ?? 1 };
    sessionStorage.setItem(SORT_KEY, JSON.stringify(this.sort));
  }

  protected onPage(event: TableLazyLoadEvent | { first?: number; rows?: number }): void {
    this.page = {
      first: event.first ?? 0,
      rows: event.rows ?? this.page.rows
    };
    this.persistPage();
  }

  protected confirmDelete(group: CourseGroup): void {
    // Deleting the group cascade-deletes its courses (FK_Course_CourseGroup ON DELETE CASCADE).
    // That is irreversible and the DB will not stop it, so the count is spelled out here — this
    // dialog is the only guard the courses get.
    // Inline style, not a scoped class: the dialog renders this via innerHTML outside the
    // component's style encapsulation, so a component-scoped selector would not reach it.
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
      accept: () => this.remove(group)
    });
  }

  private remove(group: CourseGroup): void {
    this.service.remove(group.pkid).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: '刪除成功',
          detail: `課程群組「${group.description}」已刪除。`
        });
        this.load();
      },
      error: (error: HttpErrorResponse) =>
        this.messageService.add({
          severity: 'error',
          // A 409 means a PartnerCourseGroup row still points at this group. Saying so is
          // actionable; a bare "刪除失敗" is not.
          summary: error.status === 409 ? '群組使用中' : '刪除失敗',
          detail: error.status === 409
            ? (error.error?.detail ?? `課程群組「${group.description}」仍被廠商群組使用，無法刪除。`)
            : `無法刪除課程群組「${group.description}」。`
        })
    });
  }

  protected view(group: CourseGroup): void {
    this.router.navigate(['/course-groups', group.pkid]);
  }

  protected edit(group: CourseGroup): void {
    this.router.navigate(['/course-groups', group.pkid, 'edit']);
  }

  // ---------- session state ----------

  private restoreState(): void {
    const filters = readJson<CourseGroupQuery>(FILTERS_KEY);
    if (filters) {
      this.appliedFilters = normalizeQuery({ ...EMPTY_COURSE_GROUP_QUERY, ...filters });
      this.draftFilters = { ...this.appliedFilters };
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

/** Blank strings from the inputs must become null, or the API filters on an empty keyword. */
function normalizeQuery(query: CourseGroupQuery): CourseGroupQuery {
  const keyword = query.keyword?.trim();
  return { keyword: keyword ? keyword : null };
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
