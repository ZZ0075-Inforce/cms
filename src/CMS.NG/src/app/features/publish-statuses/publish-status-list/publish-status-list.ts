import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TooltipModule } from 'primeng/tooltip';

import { PublishStatusService } from '@core/services/publish-status.service';
import {
  PublishStatus,
  PublishStatusQuery,
  EMPTY_PUBLISH_STATUS_QUERY
} from '@core/models/publish-status.model';

const FILTERS_KEY = 'publish-status-list-filters';
const SORT_KEY = 'publish-status-list-sort';
const PAGE_KEY = 'publish-status-list-page';

interface SortState {
  sortField: string;
  sortOrder: number;
}

interface PageState {
  first: number;
  rows: number;
}

/** null = 不篩選, true = 只顯示勾選, false = 只顯示未勾選. */
interface TriStateOption {
  label: string;
  value: boolean | null;
}

@Component({
  selector: 'app-publish-status-list',
  imports: [
    FormsModule,
    RouterLink,
    TableModule,
    ButtonModule,
    DrawerModule,
    InputTextModule,
    SelectModule,
    TooltipModule
  ],
  templateUrl: './publish-status-list.html',
  styleUrl: './publish-status-list.scss'
})
export class PublishStatusList implements OnInit {
  private readonly service = inject(PublishStatusService);
  private readonly router = inject(Router);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);

  protected readonly statuses = signal<PublishStatus[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterDrawerVisible = signal(false);

  protected readonly triStateOptions: TriStateOption[] = [
    { label: '全部', value: null },
    { label: '是', value: true },
    { label: '否', value: false }
  ];

  /** Bound to the drawer inputs. Only copied into `appliedFilters` when 搜尋 is pressed. */
  protected draftFilters: PublishStatusQuery = { ...EMPTY_PUBLISH_STATUS_QUERY };
  protected appliedFilters: PublishStatusQuery = { ...EMPTY_PUBLISH_STATUS_QUERY };

  protected sort: SortState = { sortField: 'pkid', sortOrder: 1 };
  protected page: PageState = { first: 0, rows: 20 };

  ngOnInit(): void {
    this.restoreState();
    this.load();
  }

  protected get hasActiveFilters(): boolean {
    return this.appliedFilters.keyword !== null
      || this.appliedFilters.isDraft !== null
      || this.appliedFilters.isPublished !== null
      || this.appliedFilters.isDiscontinued !== null;
  }

  protected load(): void {
    this.loading.set(true);
    this.service.query(this.appliedFilters).subscribe({
      next: statuses => {
        this.statuses.set(statuses);
        this.loading.set(false);
      },
      error: () => {
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法載入上架狀態清單，請稍後再試。'
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
    this.draftFilters = { ...EMPTY_PUBLISH_STATUS_QUERY };
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

  protected confirmDelete(status: PublishStatus): void {
    this.confirmationService.confirm({
      header: '刪除上架狀態',
      message: `確定要刪除主代碼 <b>${status.pkid}</b>「${status.description}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.remove(status)
    });
  }

  private remove(status: PublishStatus): void {
    this.service.remove(status.pkid).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: '刪除成功',
          detail: `上架狀態「${status.description}」已刪除。`
        });
        this.load();
      },
      error: (error: HttpErrorResponse) =>
        this.messageService.add({
          severity: 'error',
          // A 409 means courses still point at this status. Saying so is actionable; a bare
          // "刪除失敗" is not.
          summary: error.status === 409 ? '上架狀態使用中' : '刪除失敗',
          detail: error.status === 409
            ? (error.error?.detail ?? `上架狀態「${status.description}」仍被課程使用，無法刪除。`)
            : `無法刪除上架狀態「${status.description}」。`
        })
    });
  }

  protected view(status: PublishStatus): void {
    this.router.navigate(['/publish-statuses', status.pkid]);
  }

  protected edit(status: PublishStatus): void {
    this.router.navigate(['/publish-statuses', status.pkid, 'edit']);
  }

  // ---------- session state ----------

  private restoreState(): void {
    const filters = readJson<PublishStatusQuery>(FILTERS_KEY);
    if (filters) {
      this.appliedFilters = normalizeQuery({ ...EMPTY_PUBLISH_STATUS_QUERY, ...filters });
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
function normalizeQuery(query: PublishStatusQuery): PublishStatusQuery {
  const keyword = query.keyword?.trim();
  return {
    keyword: keyword ? keyword : null,
    isDraft: query.isDraft ?? null,
    isPublished: query.isPublished ?? null,
    isDiscontinued: query.isDiscontinued ?? null
  };
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
