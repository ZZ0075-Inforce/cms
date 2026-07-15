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

import { PartnerService } from '@core/services/partner.service';
import { Partner, PartnerQuery, EMPTY_PARTNER_QUERY } from '@core/models/partner.model';

const FILTERS_KEY = 'partner-list-filters';
const SORT_KEY = 'partner-list-sort';
const PAGE_KEY = 'partner-list-page';

interface SortState {
  sortField: string;
  sortOrder: number;
}

interface PageState {
  first: number;
  rows: number;
}

@Component({
  selector: 'app-partner-list',
  imports: [
    FormsModule,
    RouterLink,
    TableModule,
    ButtonModule,
    DrawerModule,
    InputTextModule,
    TooltipModule
  ],
  templateUrl: './partner-list.html',
  styleUrl: './partner-list.scss'
})
export class PartnerList implements OnInit {
  private readonly service = inject(PartnerService);
  private readonly router = inject(Router);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);

  protected readonly partners = signal<Partner[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterDrawerVisible = signal(false);

  /** Bound to the drawer inputs. Only copied into `appliedFilters` when 搜尋 is pressed. */
  protected draftFilters: PartnerQuery = { ...EMPTY_PARTNER_QUERY };
  protected appliedFilters: PartnerQuery = { ...EMPTY_PARTNER_QUERY };

  protected sort: SortState = { sortField: 'displayOrder', sortOrder: 1 };
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
      next: partners => {
        this.partners.set(partners);
        this.loading.set(false);
      },
      error: () => {
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法載入廠商清單，請稍後再試。'
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
    this.draftFilters = { ...EMPTY_PARTNER_QUERY };
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

  protected confirmDelete(partner: Partner): void {
    this.confirmationService.confirm({
      header: '刪除廠商',
      message: `確定要刪除主代碼 <b>${partner.pkid}</b>「${partner.name}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.remove(partner)
    });
  }

  private remove(partner: Partner): void {
    this.service.remove(partner.pkid).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: '刪除成功',
          detail: `廠商「${partner.name}」已刪除。`
        });
        this.load();
      },
      error: (error: HttpErrorResponse) =>
        this.messageService.add({
          severity: 'error',
          // A 409 means courses or certifications still point at this partner. Saying so is
          // actionable; a bare "刪除失敗" is not.
          summary: error.status === 409 ? '廠商使用中' : '刪除失敗',
          detail: error.status === 409
            ? (error.error?.detail ?? `廠商「${partner.name}」仍被其他資料使用，無法刪除。`)
            : `無法刪除廠商「${partner.name}」。`
        })
    });
  }

  protected view(partner: Partner): void {
    this.router.navigate(['/partners', partner.pkid]);
  }

  protected edit(partner: Partner): void {
    this.router.navigate(['/partners', partner.pkid, 'edit']);
  }

  // ---------- session state ----------

  private restoreState(): void {
    const filters = readJson<PartnerQuery>(FILTERS_KEY);
    if (filters) {
      this.appliedFilters = normalizeQuery({ ...EMPTY_PARTNER_QUERY, ...filters });
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
function normalizeQuery(query: PartnerQuery): PartnerQuery {
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
