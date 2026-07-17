import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { RowAuditService } from '@core/services/row-audit.service';
import { HttpErrorResponse } from '@angular/common/http';
import { Subject, of, throwError } from 'rxjs';

import { FeaturedPromoItemForm, FeaturedPromoItemFormContext, FeaturedPromoItemFormInitial } from './featured-promo-item-form';
import { LookupService } from '@core/services/lookup.service';
import { FeaturedPromoItemRequest, PromotionLookup } from '@core/models/featured-promo-item.model';

describe('FeaturedPromoItemForm', () => {
  let fixture: ComponentFixture<FeaturedPromoItemForm>;
  let component: FeaturedPromoItemForm;
  let lookups: jasmine.SpyObj<LookupService>;

  const context: FeaturedPromoItemFormContext = {
    pkid: 0, scheduleOn: '2026-03-16', trainingCenterPkid: 1, slot: 2
  };

  const promotion: PromotionLookup = {
    pkid: 10, promoCode: '20251204_SkillTrainAI', topic: '成為能AI協作的程式設計師', description: '轉職就業養成班'
  };

  /** Reach past the protected members the template binds to. */
  interface Probe {
    form: FeaturedPromoItemForm['form'];
    lookup: () => void;
    onSave: () => void;
    onCancel: () => void;
    promoCodeResolved: () => boolean;
    lookupError: () => string | null;
    save: { subscribe: (fn: (r: FeaturedPromoItemRequest) => void) => void };
    cancel: { subscribe: (fn: () => void) => void };
  }
  const probe = () => component as unknown as Probe;

  async function setup(initial: FeaturedPromoItemFormInitial | null): Promise<void> {
    lookups = jasmine.createSpyObj<LookupService>('LookupService', ['promotionByCode']);
    lookups.promotionByCode.and.returnValue(of(promotion));

    await TestBed.configureTestingModule({
      imports: [FeaturedPromoItemForm],
      providers: [
        provideNoopAnimations(),
        { provide: LookupService, useValue: lookups },
        { provide: RowAuditService, useValue: { history: () => of([]) } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FeaturedPromoItemForm);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('context', context);
    fixture.componentRef.setInput('initial', initial);
    fixture.detectChanges();
  }

  describe('new mode', () => {
    beforeEach(async () => await setup(null));

    it('starts invalid and unresolved', () => {
      expect(probe().form.invalid).toBeTrue();
      expect(probe().promoCodeResolved()).toBeFalse();
    });

    it('lookup resolves the PromoCode and prefills empty Topic / Description', () => {
      probe().form.controls.promoCode.setValue('20251204_SkillTrainAI');

      probe().lookup();

      expect(lookups.promotionByCode).toHaveBeenCalledWith('20251204_SkillTrainAI');
      expect(probe().promoCodeResolved()).toBeTrue();
      expect(probe().form.controls.topic.value).toBe('成為能AI協作的程式設計師');
      expect(probe().form.controls.description.value).toBe('轉職就業養成班');
    });

    it('does NOT emit save until the PromoCode is resolved, even when the fields are filled', () => {
      const saved = jasmine.createSpy('saved');
      probe().save.subscribe(saved);
      probe().form.setValue({ promoCode: 'unresolved', topic: 'T', description: 'D' });

      probe().onSave();

      expect(saved).not.toHaveBeenCalled();
      expect(probe().lookupError()).toBe('請先查詢並確認促銷代碼');
    });

    it('emits a request carrying the resolved promotionPkid and the context', () => {
      let emitted: FeaturedPromoItemRequest | undefined;
      probe().save.subscribe(r => (emitted = r));
      probe().form.controls.promoCode.setValue('20251204_SkillTrainAI');
      probe().lookup();

      probe().onSave();

      expect(emitted).toEqual({
        pkid: 0,
        scheduleOn: '2026-03-16',
        trainingCenterPkid: 1,
        slot: 2,
        promotionPkid: 10,
        topic: '成為能AI協作的程式設計師',
        description: '轉職就業養成班'
      });
    });

    it('invalidates the resolved pkid once the PromoCode is edited', () => {
      probe().form.controls.promoCode.setValue('20251204_SkillTrainAI');
      probe().lookup();
      expect(probe().promoCodeResolved()).toBeTrue();

      probe().form.controls.promoCode.setValue('changed');

      expect(probe().promoCodeResolved()).toBeFalse();
    });

    it('surfaces a 404 as an error and blocks the save', () => {
      lookups.promotionByCode.and.returnValue(throwError(() => new HttpErrorResponse({ status: 404 })));
      probe().form.controls.promoCode.setValue('nope');

      probe().lookup();

      expect(probe().lookupError()).toBe('找不到促銷代碼');
      expect(probe().promoCodeResolved()).toBeFalse();
    });

    // Regression: QA 2026-07-17 — the field's (blur) and the 查詢 button's (onClick) both call
    // lookup(), and clicking the button blurs the field, so one press fired two identical
    // requests. A pending Subject is what reproduces it: with of()/throwError() the first call
    // answers before the second starts, which is exactly the race the browser does not have.
    it('does not fire a second lookup while one is still in flight', () => {
      const pending = new Subject<PromotionLookup>();
      lookups.promotionByCode.and.returnValue(pending.asObservable());
      probe().form.controls.promoCode.setValue('20251204_SkillTrainAI');

      probe().lookup();   // (blur)
      probe().lookup();   // (onClick), before the first answers

      expect(lookups.promotionByCode).toHaveBeenCalledTimes(1);

      pending.next(promotion);
      pending.complete();
      expect(probe().promoCodeResolved()).toBeTrue();
    });

    it('still allows a fresh lookup once an in-flight one has failed', () => {
      lookups.promotionByCode.and.returnValue(throwError(() => new HttpErrorResponse({ status: 404 })));
      probe().form.controls.promoCode.setValue('nope');

      probe().lookup();
      probe().lookup();

      // The error path clears lookingUp, so the guard must not strand the user on a dead code.
      expect(lookups.promotionByCode).toHaveBeenCalledTimes(2);
    });
  });

  describe('edit / paste mode', () => {
    const initial: FeaturedPromoItemFormInitial = {
      promoCode: '20251215_n8n', topic: 'n8n自動化三部曲', description: '從自動化新手到企業級', promotionPkid: 99
    };

    beforeEach(async () => await setup(initial));

    it('is resolved immediately from the seeded promotionPkid, without a lookup', () => {
      expect(probe().promoCodeResolved()).toBeTrue();
      expect(lookups.promotionByCode).not.toHaveBeenCalled();
    });

    it('emits the seeded promotionPkid unchanged when nothing is edited', () => {
      let emitted: FeaturedPromoItemRequest | undefined;
      probe().save.subscribe(r => (emitted = r));

      probe().onSave();

      expect(emitted?.promotionPkid).toBe(99);
      expect(emitted?.topic).toBe('n8n自動化三部曲');
      expect(lookups.promotionByCode).not.toHaveBeenCalled();
    });

    it('emits cancel', () => {
      const cancelled = jasmine.createSpy('cancelled');
      probe().cancel.subscribe(cancelled);

      probe().onCancel();

      expect(cancelled).toHaveBeenCalled();
    });
  });
});
