/**
 * 首頁上稿作業 FeaturedPromoItem.
 *
 * `pkid` IS the primary key (PK_FeaturedPromoItem over an int IDENTITY), so it addresses the record and
 * needs no URL encoding. `scheduleOn` travels as an ISO `yyyy-MM-dd` string; the board converts to/from
 * `Date` via `core/utils/date.util.ts`. `promoCode` / `trainingCenterName` are JOIN-resolved display
 * labels. A UNIQUE (scheduleOn, trainingCenterPkid, slot) index means one row per slot per day/centre.
 */
export interface FeaturedPromoItem {
  pkid: number;
  scheduleOn: string;
  trainingCenterPkid: number;
  slot: number;
  promotionPkid: number;
  topic: string;
  description: string;

  /** JOIN-resolved display labels. */
  promoCode: string;
  trainingCenterName: string;
}

/** Write DTO. pkid travels in the body on update; on create the DB generates it. */
export interface FeaturedPromoItemRequest {
  pkid: number;
  scheduleOn: string;
  trainingCenterPkid: number;
  slot: number;
  promotionPkid: number;
  topic: string;
  description: string;
}

/** Search DTO for POST /api/featured-promo-items/query. Omitted/null fields mean "no filter". */
export interface FeaturedPromoItemQuery {
  trainingCenterPkid: number | null;
  scheduleOnFrom: string | null;
  scheduleOnTo: string | null;
}

/** + / − on the board. "down" pushes Slot 1 → 2; "up" pulls Slot 2 → 1. */
export type SlotMoveDirection = 'up' | 'down';

/** Slim TrainingCenter row for the board's centre tabs (label = name, value = pkid). */
export interface TrainingCenterLookup {
  pkid: number;
  name: string;
}

/** Slim Promotion2 row resolved from a PromoCode in the edit form. */
export interface PromotionLookup {
  pkid: number;
  promoCode: string;
  topic: string;
  description: string;
}
