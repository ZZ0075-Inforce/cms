import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';

import { FeaturedPromoItemService } from '@core/services/featured-promo-item.service';
import { LookupService } from '@core/services/lookup.service';
import {
  FeaturedPromoItem,
  FeaturedPromoItemRequest,
  SlotMoveDirection,
  TrainingCenterLookup
} from '@core/models/featured-promo-item.model';
import { toIso } from '@core/utils/date.util';
import {
  FeaturedPromoItemForm,
  FeaturedPromoItemFormContext,
  FeaturedPromoItemFormInitial
} from '../featured-promo-item-form/featured-promo-item-form';

/** The three fixed slots the board lays out per day. */
const SLOTS = [1, 2, 3] as const;

/** Weekday glyphs for Monday..Sunday (the week always starts on Monday). */
const WEEKDAY_LABELS = ['一', '二', '三', '四', '五', '六', '日'] as const;

interface DayColumn {
  date: Date;
  iso: string;
  header: string;
}

/**
 * 首頁上稿作業 FeaturedPromoItem board.
 *
 * TrainingCenter tabs across the top (label = Name, value = pkid) filter TrainingCenter_pkid; a
 * Monday–Sunday week navigator filters ScheduleOn. Each day shows three slots; a slot is either a
 * filled row (Edit / Copy / Delete + the PromoCode/Topic/Description columns) or an empty one
 * (Edit / Paste). The inline editor is <app-featured-promo-item-form>. + / − swap a row up/down a slot.
 */
@Component({
  selector: 'app-featured-promo-item-list',
  imports: [ButtonModule, TooltipModule, FeaturedPromoItemForm],
  templateUrl: './featured-promo-item-list.html',
  styleUrl: './featured-promo-item-list.scss'
})
export class FeaturedPromoItemList implements OnInit {
  private readonly service = inject(FeaturedPromoItemService);
  private readonly lookupService = inject(LookupService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly messageService = inject(MessageService);

  protected readonly slots = SLOTS;

  protected readonly trainingCenters = signal<TrainingCenterLookup[]>([]);
  protected readonly activeCenterPkid = signal<number | null>(null);
  protected readonly items = signal<FeaturedPromoItem[]>([]);
  protected readonly loading = signal(false);

  /** Monday of the displayed week, at local midnight. */
  protected readonly weekStart = signal<Date>(mondayOf(new Date()));

  /** The copy buffer for Copy → Paste. Null until something is copied. */
  protected readonly copied = signal<FeaturedPromoItemFormInitial | null>(null);

  /** The single open inline editor, keyed `${iso}#${slot}`; null when none is open. */
  protected readonly editingKey = signal<string | null>(null);
  protected readonly editingContext = signal<FeaturedPromoItemFormContext | null>(null);
  protected readonly editingInitial = signal<FeaturedPromoItemFormInitial | null>(null);

  /** The seven Monday..Sunday day columns of the displayed week. */
  protected readonly weekDays = computed<DayColumn[]>(() => {
    const start = this.weekStart();
    return Array.from({ length: 7 }, (_, i) => {
      const date = addDays(start, i);
      return { date, iso: toIso(date)!, header: `${date.getMonth() + 1}/${date.getDate()} (${WEEKDAY_LABELS[i]})` };
    });
  });

  /** Fast lookup of the row filling a given day+slot. */
  private readonly itemMap = computed(() => {
    const map = new Map<string, FeaturedPromoItem>();
    for (const item of this.items()) map.set(keyFor(item.scheduleOn, item.slot), item);
    return map;
  });

  protected readonly weekLabel = computed(() => {
    const start = this.weekStart();
    const end = addDays(start, 6);
    return `${start.getMonth() + 1}/${start.getDate()} -- ${end.getMonth() + 1}/${end.getDate()}`;
  });

  ngOnInit(): void {
    forkJoin({
      centers: this.lookupService.trainingCenters().pipe(catchError(() => of([] as TrainingCenterLookup[])))
    }).subscribe(({ centers }) => {
      this.trainingCenters.set(centers);
      if (centers.length > 0) this.activeCenterPkid.set(centers[0].pkid);
      this.load();
    });
  }

  protected load(): void {
    const centerPkid = this.activeCenterPkid();
    if (centerPkid === null) {
      this.items.set([]);
      return;
    }

    const start = this.weekStart();
    this.loading.set(true);
    this.service.query({
      trainingCenterPkid: centerPkid,
      scheduleOnFrom: toIso(start),
      scheduleOnTo: toIso(addDays(start, 6))
    }).subscribe({
      next: items => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.messageService.add({
          severity: 'error',
          summary: '載入失敗',
          detail: '無法載入上稿清單，請稍後再試。'
        });
        this.loading.set(false);
      }
    });
  }

  // ---------- tabs & week navigation ----------

  protected selectCenter(pkid: number): void {
    if (this.activeCenterPkid() === pkid) return;
    this.activeCenterPkid.set(pkid);
    this.closeEditor();
    this.load();
  }

  protected prevWeek(): void {
    this.weekStart.set(addDays(this.weekStart(), -7));
    this.closeEditor();
    this.load();
  }

  protected nextWeek(): void {
    this.weekStart.set(addDays(this.weekStart(), 7));
    this.closeEditor();
    this.load();
  }

  // ---------- grid helpers ----------

  protected itemAt(iso: string, slot: number): FeaturedPromoItem | undefined {
    return this.itemMap().get(keyFor(iso, slot));
  }

  protected isEditing(iso: string, slot: number): boolean {
    return this.editingKey() === keyFor(iso, slot);
  }

  // ---------- edit / new / paste ----------

  /** Opens the inline editor for a filled row. */
  protected edit(day: DayColumn, slot: number, item: FeaturedPromoItem): void {
    this.openEditor(day, slot, item.pkid, {
      promoCode: item.promoCode,
      topic: item.topic,
      description: item.description,
      promotionPkid: item.promotionPkid
    });
  }

  /** Opens the inline editor for an empty slot with no seed values. */
  protected create(day: DayColumn, slot: number): void {
    this.openEditor(day, slot, 0, null);
  }

  /** Opens the inline editor for an empty slot seeded from the copy buffer. */
  protected paste(day: DayColumn, slot: number): void {
    const buffer = this.copied();
    if (!buffer) {
      this.messageService.add({ severity: 'info', summary: '尚未複製', detail: '請先按 Copy 複製一筆資料。' });
      return;
    }
    this.openEditor(day, slot, 0, { ...buffer });
  }

  private openEditor(
    day: DayColumn, slot: number, pkid: number, initial: FeaturedPromoItemFormInitial | null): void {
    const centerPkid = this.activeCenterPkid();
    if (centerPkid === null) return;
    this.editingContext.set({ pkid, scheduleOn: day.iso, trainingCenterPkid: centerPkid, slot });
    this.editingInitial.set(initial);
    this.editingKey.set(keyFor(day.iso, slot));
  }

  protected closeEditor(): void {
    this.editingKey.set(null);
    this.editingContext.set(null);
    this.editingInitial.set(null);
  }

  protected onSave(request: FeaturedPromoItemRequest): void {
    const isEdit = request.pkid > 0;
    const save$: Observable<unknown> = isEdit
      ? this.service.update(request)
      : this.service.create(request);
    save$.subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: '儲存成功',
          detail: `上稿資料已${isEdit ? '更新' : '新增'}。`
        });
        this.closeEditor();
        this.load();
      },
      error: (error: HttpErrorResponse) => {
        this.messageService.add({
          severity: 'error',
          summary: error.status === 409 ? '版位已被占用' : '儲存失敗',
          detail: error.error?.detail ?? '請稍後再試。'
        });
      }
    });
  }

  // ---------- copy / delete / move ----------

  protected copy(item: FeaturedPromoItem): void {
    this.copied.set({
      promoCode: item.promoCode,
      topic: item.topic,
      description: item.description,
      promotionPkid: item.promotionPkid
    });
    this.messageService.add({ severity: 'info', summary: '已複製', detail: `已複製「${item.promoCode}」，可貼到空白版位。` });
  }

  protected confirmDelete(item: FeaturedPromoItem): void {
    this.confirmationService.confirm({
      header: '刪除上稿資料',
      message: `確定要刪除「${item.promoCode}」（版位 ${item.slot}）？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.remove(item)
    });
  }

  private remove(item: FeaturedPromoItem): void {
    this.service.remove(item.pkid).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: '刪除成功', detail: `「${item.promoCode}」已刪除。` });
        if (this.isEditing(item.scheduleOn, item.slot)) this.closeEditor();
        this.load();
      },
      error: () =>
        this.messageService.add({ severity: 'error', summary: '刪除失敗', detail: `無法刪除「${item.promoCode}」。` })
    });
  }

  /** + = down (Slot + 1), − = up (Slot − 1). Swaps with the target slot's occupant if any. */
  protected move(item: FeaturedPromoItem, direction: SlotMoveDirection): void {
    this.closeEditor();
    this.service.move(item.pkid, direction).subscribe({
      next: () => this.load(),
      error: (error: HttpErrorResponse) => {
        // 400 at a slot boundary is expected — say so quietly rather than screaming 失敗.
        this.messageService.add({
          severity: error.status === 400 ? 'info' : 'error',
          summary: error.status === 400 ? '無法移動' : '移動失敗',
          detail: error.error?.detail ?? '請稍後再試。'
        });
      }
    });
  }
}

function keyFor(iso: string, slot: number): string {
  // Dates come back as full ISO from the API but as yyyy-MM-dd locally; normalise to the date part.
  return `${iso.slice(0, 10)}#${slot}`;
}

/** Monday (local midnight) of the week containing `date`. */
function mondayOf(date: Date): Date {
  const day = date.getDay();                 // 0 = Sunday … 6 = Saturday
  const diff = day === 0 ? -6 : 1 - day;     // Sunday rolls back to the previous Monday
  return new Date(date.getFullYear(), date.getMonth(), date.getDate() + diff);
}

function addDays(date: Date, days: number): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate() + days);
}
