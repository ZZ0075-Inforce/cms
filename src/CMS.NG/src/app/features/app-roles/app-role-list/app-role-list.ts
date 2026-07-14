import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { TooltipModule } from 'primeng/tooltip';
import { TagModule } from 'primeng/tag';

import { AppRoleService } from '@core/services/app-role.service';
import { AppRole, AppRoleQuery, EMPTY_APP_ROLE_QUERY } from '@core/models/app-role.model';

const FILTERS_KEY = 'app-role-list-filters';
const SORT_KEY = 'app-role-list-sort';
const PAGE_KEY = 'app-role-list-page';

interface SortState {
  sortField: string;
  sortOrder: number;
}

interface PageState {
  first: number;
  rows: number;
}

@Component({
  selector: 'app-role-list',
  imports: [
    FormsModule,
    RouterLink,
    TableModule,
    ButtonModule,
    DrawerModule,
    InputTextModule,
    InputNumberModule,
    TooltipModule,
    TagModule
  ],
  templateUrl: './app-role-list.html',
  styleUrl: './app-role-list.scss'
})
export class AppRoleList implements OnInit {
  private readonly service = inject(AppRoleService);
  private readonly router = inject(Router);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);

  protected readonly roles = signal<AppRole[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterDrawerVisible = signal(false);

  /** Bound to the drawer inputs. Only copied into `appliedFilters` when 搜尋 is pressed. */
  protected draftFilters: AppRoleQuery = { ...EMPTY_APP_ROLE_QUERY };
  protected appliedFilters: AppRoleQuery = { ...EMPTY_APP_ROLE_QUERY };

  protected sort: SortState = { sortField: 'roleId', sortOrder: 1 };
  protected page: PageState = { first: 0, rows: 20 };

  ngOnInit(): void {
    this.restoreState();
    this.load();
  }

  protected get hasActiveFilters(): boolean {
    return this.appliedFilters.keyword !== null || this.appliedFilters.permissionLevel !== null;
  }

  protected load(): void {
    this.loading.set(true);
    this.service.query(this.appliedFilters).subscribe({
      next: roles => {
        this.roles.set(roles);
        this.loading.set(false);
      },
      error: () => {
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法載入角色清單，請稍後再試。'
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
    this.draftFilters = { ...EMPTY_APP_ROLE_QUERY };
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

  protected confirmDelete(role: AppRole): void {
    this.confirmationService.confirm({
      header: '刪除角色',
      message: `確定要刪除主代碼 <b>${role.pkid}</b>「${role.roleId}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.remove(role)
    });
  }

  private remove(role: AppRole): void {
    this.service.remove(role.roleId).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: '刪除成功',
          detail: `角色「${role.roleId}」已刪除。`
        });
        this.load();
      },
      error: () =>
        this.messageService.add({
          severity: 'error',
          summary: '刪除失敗',
          detail: `無法刪除角色「${role.roleId}」。`
        })
    });
  }

  protected view(role: AppRole): void {
    this.router.navigate(['/app-roles', role.roleId]);
  }

  protected edit(role: AppRole): void {
    this.router.navigate(['/app-roles', role.roleId, 'edit']);
  }

  // ---------- session state ----------

  private restoreState(): void {
    const filters = readJson<AppRoleQuery>(FILTERS_KEY);
    if (filters) {
      this.appliedFilters = normalizeQuery({ ...EMPTY_APP_ROLE_QUERY, ...filters });
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
function normalizeQuery(query: AppRoleQuery): AppRoleQuery {
  const keyword = query.keyword?.trim();
  return {
    keyword: keyword ? keyword : null,
    permissionLevel: query.permissionLevel ?? null
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
