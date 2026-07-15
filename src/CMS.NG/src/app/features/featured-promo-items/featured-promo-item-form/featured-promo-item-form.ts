import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';

import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';

import { LookupService } from '@core/services/lookup.service';
import { FeaturedPromoItemRequest } from '@core/models/featured-promo-item.model';

/** Where the edited row lives — carried through unchanged into the emitted request. */
export interface FeaturedPromoItemFormContext {
  /** 0 for a new/paste row; the DB pkid in edit mode. */
  pkid: number;
  /** ISO yyyy-MM-dd for the day this cell belongs to. */
  scheduleOn: string;
  trainingCenterPkid: number;
  slot: number;
}

/** Seed values for the three editable fields (edit = existing row, paste = copied row). */
export interface FeaturedPromoItemFormInitial {
  promoCode: string;
  topic: string;
  description: string;
  /** Known for edit/paste (already resolved); null for a blank new row. */
  promotionPkid: number | null;
}

/**
 * The inline PromoCode / Topic / Description editor the board opens for Edit, New and Paste.
 *
 * The PromoCode is resolved to a Promotion_pkid via the lookup endpoint (spec: "Enter PromoCode,
 * lookup, then set Promotion_pkid") before a save is allowed. A resolved pkid is only trusted while
 * the PromoCode field still matches the code it was resolved for — editing the code invalidates it and
 * forces a fresh lookup.
 */
@Component({
  selector: 'app-featured-promo-item-form',
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule],
  templateUrl: './featured-promo-item-form.html',
  styleUrl: './featured-promo-item-form.scss'
})
export class FeaturedPromoItemForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly lookupService = inject(LookupService);

  readonly context = input.required<FeaturedPromoItemFormContext>();
  readonly initial = input<FeaturedPromoItemFormInitial | null>(null);

  readonly save = output<FeaturedPromoItemRequest>();
  readonly cancel = output<void>();

  protected readonly lookingUp = signal(false);
  protected readonly lookupError = signal<string | null>(null);

  /** The pkid resolved from the PromoCode, and the exact code it was resolved for. */
  private resolvedPromotionPkid: number | null = null;
  private resolvedCode: string | null = null;

  protected readonly form = this.fb.group({
    promoCode: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(30)]),
    topic: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(100)]),
    description: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(300)])
  });

  ngOnInit(): void {
    const initial = this.initial();
    if (initial) {
      this.form.patchValue({
        promoCode: initial.promoCode,
        topic: initial.topic,
        description: initial.description
      });
      // Trust the seeded pkid only while the code stays put (see promoCodeResolved()).
      this.resolvedPromotionPkid = initial.promotionPkid;
      this.resolvedCode = initial.promotionPkid !== null ? initial.promoCode.trim() : null;
    }
  }

  /** True while the resolved pkid still matches the code currently in the field. */
  protected promoCodeResolved(): boolean {
    return this.resolvedPromotionPkid !== null
      && this.resolvedCode === this.form.controls.promoCode.value.trim();
  }

  /** Resolves the current PromoCode to a Promotion_pkid, prefilling empty Topic/Description. */
  protected lookup(): void {
    const code = this.form.controls.promoCode.value.trim();
    if (!code) {
      this.resolvedPromotionPkid = null;
      this.resolvedCode = null;
      this.lookupError.set('請輸入促銷代碼');
      return;
    }
    if (this.promoCodeResolved()) return;   // already resolved for this exact code

    this.lookingUp.set(true);
    this.lookupError.set(null);
    this.lookupService.promotionByCode(code).subscribe({
      next: promotion => {
        this.resolvedPromotionPkid = promotion.pkid;
        this.resolvedCode = code;
        // Prefill only blanks, so a lookup never clobbers text the user already typed/pasted.
        if (!this.form.controls.topic.value.trim()) {
          this.form.controls.topic.setValue(promotion.topic);
        }
        if (!this.form.controls.description.value.trim()) {
          this.form.controls.description.setValue(promotion.description);
        }
        this.lookingUp.set(false);
      },
      error: (error: HttpErrorResponse) => {
        this.resolvedPromotionPkid = null;
        this.resolvedCode = null;
        this.lookingUp.set(false);
        this.lookupError.set(error.status === 404 ? '找不到促銷代碼' : '查詢失敗，請稍後再試');
      }
    });
  }

  protected onSave(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    if (!this.promoCodeResolved()) {
      this.lookupError.set('請先查詢並確認促銷代碼');
      return;
    }

    const raw = this.form.getRawValue();
    const context = this.context();
    this.save.emit({
      pkid: context.pkid,
      scheduleOn: context.scheduleOn,
      trainingCenterPkid: context.trainingCenterPkid,
      slot: context.slot,
      promotionPkid: this.resolvedPromotionPkid!,
      topic: raw.topic.trim(),
      description: raw.description.trim()
    });
  }

  protected onCancel(): void {
    this.cancel.emit();
  }

  protected invalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.touched || control.dirty);
  }
}
