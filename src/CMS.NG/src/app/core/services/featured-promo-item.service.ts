import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env';
import {
  FeaturedPromoItem,
  FeaturedPromoItemQuery,
  FeaturedPromoItemRequest,
  SlotMoveDirection
} from '@core/models/featured-promo-item.model';

@Injectable({ providedIn: 'root' })
export class FeaturedPromoItemService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/featured-promo-items`;

  getAll(): Observable<FeaturedPromoItem[]> {
    return this.http.get<FeaturedPromoItem[]>(this.base);
  }

  /** The board posts the active centre tab + the selected Monday–Sunday week. */
  query(query: FeaturedPromoItemQuery): Observable<FeaturedPromoItem[]> {
    return this.http.post<FeaturedPromoItem[]>(`${this.base}/query`, query);
  }

  // pkid is a number, so no encodeURIComponent here.
  getById(pkid: number): Observable<FeaturedPromoItem> {
    return this.http.get<FeaturedPromoItem>(`${this.base}/${pkid}`);
  }

  create(request: FeaturedPromoItemRequest): Observable<FeaturedPromoItem> {
    return this.http.post<FeaturedPromoItem>(this.base, request);
  }

  /** Update takes no id segment — the key rides in the body, per the API convention. */
  update(request: FeaturedPromoItemRequest): Observable<void> {
    return this.http.put<void>(this.base, request);
  }

  remove(pkid: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${pkid}`);
  }

  /** Moves a row one slot up/down, swapping with the target slot's occupant if any. */
  move(pkid: number, direction: SlotMoveDirection): Observable<void> {
    return this.http.post<void>(`${this.base}/${pkid}/move`, { direction });
  }
}
