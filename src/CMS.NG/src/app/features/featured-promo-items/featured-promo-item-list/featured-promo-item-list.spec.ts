import { ComponentFixture, TestBed } from '@angular/core/testing';
import { WritableSignal, Signal } from '@angular/core';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of } from 'rxjs';

import { FeaturedPromoItemList } from './featured-promo-item-list';
import { FeaturedPromoItemService } from '@core/services/featured-promo-item.service';
import { LookupService } from '@core/services/lookup.service';
import { FeaturedPromoItem, FeaturedPromoItemQuery, FeaturedPromoItemRequest, TrainingCenterLookup } from '@core/models/featured-promo-item.model';
import { FeaturedPromoItemFormContext, FeaturedPromoItemFormInitial } from '../featured-promo-item-form/featured-promo-item-form';

describe('FeaturedPromoItemList', () => {
  let fixture: ComponentFixture<FeaturedPromoItemList>;
  let component: FeaturedPromoItemList;
  let service: jasmine.SpyObj<FeaturedPromoItemService>;
  let lookups: jasmine.SpyObj<LookupService>;

  const centers: TrainingCenterLookup[] = [{ pkid: 1, name: '台北' }, { pkid: 2, name: '新竹' }];

  const item: FeaturedPromoItem = {
    pkid: 5, scheduleOn: '2026-03-16', trainingCenterPkid: 1, slot: 1, promotionPkid: 10,
    topic: 'n8n自動化三部曲', description: '從自動化新手到企業級', promoCode: '20251215_n8n', trainingCenterName: '台北'
  };

  interface DayColumn { date: Date; iso: string; header: string; }
  interface Probe {
    weekStart: WritableSignal<Date>;
    activeCenterPkid: Signal<number | null>;
    items: Signal<FeaturedPromoItem[]>;
    copied: Signal<FeaturedPromoItemFormInitial | null>;
    editingKey: Signal<string | null>;
    editingContext: Signal<FeaturedPromoItemFormContext | null>;
    editingInitial: Signal<FeaturedPromoItemFormInitial | null>;
    weekDays: Signal<DayColumn[]>;
    weekLabel: Signal<string>;
    load: () => void;
    selectCenter: (pkid: number) => void;
    prevWeek: () => void;
    nextWeek: () => void;
    itemAt: (iso: string, slot: number) => FeaturedPromoItem | undefined;
    edit: (day: DayColumn, slot: number, item: FeaturedPromoItem) => void;
    create: (day: DayColumn, slot: number) => void;
    paste: (day: DayColumn, slot: number) => void;
    copy: (item: FeaturedPromoItem) => void;
    confirmDelete: (item: FeaturedPromoItem) => void;
    move: (item: FeaturedPromoItem, direction: 'up' | 'down') => void;
    onSave: (request: FeaturedPromoItemRequest) => void;
  }
  const probe = () => component as unknown as Probe;

  async function setup(): Promise<void> {
    service = jasmine.createSpyObj<FeaturedPromoItemService>('FeaturedPromoItemService',
      ['query', 'create', 'update', 'remove', 'move']);
    lookups = jasmine.createSpyObj<LookupService>('LookupService', ['trainingCenters']);

    service.query.and.returnValue(of([item]));
    service.create.and.returnValue(of(item));
    service.update.and.returnValue(of(void 0));
    service.remove.and.returnValue(of(void 0));
    service.move.and.returnValue(of(void 0));
    lookups.trainingCenters.and.returnValue(of(centers));

    await TestBed.configureTestingModule({
      imports: [FeaturedPromoItemList],
      providers: [
        provideNoopAnimations(),
        { provide: FeaturedPromoItemService, useValue: service },
        { provide: LookupService, useValue: lookups },
        MessageService,
        // Auto-accept the delete confirmation so remove() runs synchronously.
        { provide: ConfirmationService, useValue: { confirm: (c: { accept: () => void }) => c.accept() } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FeaturedPromoItemList);
    component = fixture.componentInstance;
    // Pin the week so the ScheduleOn range is deterministic (16–22 Mar 2026).
    probe().weekStart.set(new Date(2026, 2, 16));
    fixture.detectChanges();   // runs ngOnInit → loads centres + first query
  }

  beforeEach(async () => await setup());

  function lastQuery(): FeaturedPromoItemQuery {
    return service.query.calls.mostRecent().args[0];
  }

  it('loads centres, activates the first, and queries the week for that centre', () => {
    expect(lookups.trainingCenters).toHaveBeenCalled();
    expect(probe().activeCenterPkid()).toBe(1);
    expect(lastQuery()).toEqual({
      trainingCenterPkid: 1, scheduleOnFrom: '2026-03-16', scheduleOnTo: '2026-03-22'
    });
    expect(probe().items()).toEqual([item]);
  });

  it('lays out seven Monday–Sunday columns', () => {
    const days = probe().weekDays();
    expect(days.length).toBe(7);
    expect(days[0].iso).toBe('2026-03-16');
    expect(days[6].iso).toBe('2026-03-22');
    expect(probe().weekLabel()).toBe('3/16 -- 3/22');
  });

  it('maps a loaded row to its day/slot cell', () => {
    expect(probe().itemAt('2026-03-16', 1)).toEqual(item);
    expect(probe().itemAt('2026-03-16', 2)).toBeUndefined();
  });

  it('nextWeek advances by seven days and reloads', () => {
    probe().nextWeek();

    expect(probe().weekLabel()).toBe('3/23 -- 3/29');
    expect(lastQuery().scheduleOnFrom).toBe('2026-03-23');
    expect(lastQuery().scheduleOnTo).toBe('2026-03-29');
  });

  it('selectCenter switches the active tab and reloads for that centre', () => {
    probe().selectCenter(2);

    expect(probe().activeCenterPkid()).toBe(2);
    expect(lastQuery().trainingCenterPkid).toBe(2);
  });

  it('create opens an empty editor with pkid 0', () => {
    probe().create(probe().weekDays()[0], 2);

    expect(probe().editingKey()).toBe('2026-03-16#2');
    expect(probe().editingContext()).toEqual({ pkid: 0, scheduleOn: '2026-03-16', trainingCenterPkid: 1, slot: 2 });
    expect(probe().editingInitial()).toBeNull();
  });

  it('edit opens the editor seeded from the row', () => {
    probe().edit(probe().weekDays()[0], 1, item);

    expect(probe().editingContext()?.pkid).toBe(5);
    expect(probe().editingInitial()).toEqual({
      promoCode: '20251215_n8n', topic: 'n8n自動化三部曲', description: '從自動化新手到企業級', promotionPkid: 10
    });
  });

  it('copy then paste seeds a new editor from the copy buffer', () => {
    probe().copy(item);
    expect(probe().copied()?.promoCode).toBe('20251215_n8n');

    probe().paste(probe().weekDays()[2], 3);

    expect(probe().editingContext()).toEqual({ pkid: 0, scheduleOn: '2026-03-18', trainingCenterPkid: 1, slot: 3 });
    expect(probe().editingInitial()?.promoCode).toBe('20251215_n8n');
    expect(probe().editingInitial()?.promotionPkid).toBe(10);
  });

  it('onSave creates when pkid is 0, then closes the editor and reloads', () => {
    probe().create(probe().weekDays()[0], 2);
    service.query.calls.reset();

    probe().onSave({
      pkid: 0, scheduleOn: '2026-03-16', trainingCenterPkid: 1, slot: 2,
      promotionPkid: 10, topic: 'T', description: 'D'
    });

    expect(service.create).toHaveBeenCalled();
    expect(service.update).not.toHaveBeenCalled();
    expect(probe().editingKey()).toBeNull();
    expect(service.query).toHaveBeenCalled();
  });

  it('onSave updates when pkid is set', () => {
    probe().onSave({
      pkid: 5, scheduleOn: '2026-03-16', trainingCenterPkid: 1, slot: 1,
      promotionPkid: 10, topic: 'T', description: 'D'
    });

    expect(service.update).toHaveBeenCalled();
    expect(service.create).not.toHaveBeenCalled();
  });

  it('move calls the service with the direction and reloads', () => {
    service.query.calls.reset();

    probe().move(item, 'down');

    expect(service.move).toHaveBeenCalledWith(5, 'down');
    expect(service.query).toHaveBeenCalled();
  });

  it('confirmDelete removes the row after the confirmation is accepted', () => {
    probe().confirmDelete(item);

    expect(service.remove).toHaveBeenCalledWith(5);
  });
});
