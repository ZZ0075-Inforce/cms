import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { DialogModule } from 'primeng/dialog';

import { RowAuditService } from '@core/services/row-audit.service';
import { RowAuditEntry } from '@core/models/row-audit.model';

/**
 * Reusable 異動紀錄 History badge for a single record. Drop it in a detail/form page's action bar with
 * the page's <c>tableName</c> and the record's <c>pkid</c>:
 *
 *   &lt;app-row-audit-badge [tableName]="'Course'" [pkid]="item.pkid" /&gt;
 *
 * On load it fetches the record's audit trail and shows the most recent entry inline on the badge
 * ("Update by alice · 2026-06-04 14:30"), or a neutral "尚無異動 No history" when there is none.
 * Clicking opens a dialog with the full trail, newest first.
 */
@Component({
  selector: 'app-row-audit-badge',
  imports: [DatePipe, DialogModule],
  templateUrl: './row-audit-badge.html',
  styleUrl: './row-audit-badge.scss'
})
export class RowAuditBadge {
  private readonly service = inject(RowAuditService);

  /** The audit TableName for this page (e.g. "Course", "AppRole"). */
  readonly tableName = input.required<string>();
  /** The record's pkid — matches what the audit writer stored in PrimaryKeyValues. */
  readonly pkid = input.required<number>();

  protected readonly entries = signal<RowAuditEntry[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly dialogVisible = signal(false);

  /** Most recent entry (the list is already newest-first), or null when there is no history. */
  protected readonly latest = computed<RowAuditEntry | null>(() => this.entries()[0] ?? null);
  protected readonly hasHistory = computed(() => this.entries().length > 0);

  constructor() {
    // Re-fetch whenever the target record changes. A failed fetch degrades to the "no history" look.
    effect(onCleanup => {
      const tableName = this.tableName();
      const pkid = this.pkid();
      this.loading.set(true);
      this.loadError.set(false);

      const sub = this.service.history(tableName, pkid).subscribe({
        next: rows => {
          this.entries.set(rows ?? []);
          this.loading.set(false);
        },
        error: () => {
          this.entries.set([]);
          this.loadError.set(true);
          this.loading.set(false);
        }
      });
      onCleanup(() => sub.unsubscribe());
    });
  }

  protected open(): void {
    this.dialogVisible.set(true);
  }
}
