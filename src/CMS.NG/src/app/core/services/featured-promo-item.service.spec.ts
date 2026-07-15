import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@env';

import { FeaturedPromoItemService } from './featured-promo-item.service';
import { FeaturedPromoItem, FeaturedPromoItemRequest } from '@core/models/featured-promo-item.model';

describe('FeaturedPromoItemService', () => {
  let service: FeaturedPromoItemService;
  let httpMock: HttpTestingController;

  const base = `${environment.apiBaseUrl}/featured-promo-items`;

  const item = { pkid: 1, promoCode: '20251215_n8n', slot: 1 } as FeaturedPromoItem;
  const request = { pkid: 0, slot: 1, promotionPkid: 10 } as FeaturedPromoItemRequest;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [FeaturedPromoItemService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(FeaturedPromoItemService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getAll issues GET /featured-promo-items', () => {
    service.getAll().subscribe(items => expect(items).toEqual([item]));

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('GET');
    req.flush([item]);
  });

  it('query POSTs the week + centre filter to /query', () => {
    const filter = { trainingCenterPkid: 1, scheduleOnFrom: '2026-03-16', scheduleOnTo: '2026-03-22' };

    service.query(filter).subscribe(items => expect(items).toEqual([item]));

    const req = httpMock.expectOne(`${base}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(filter);
    req.flush([item]);
  });

  it('getById puts the numeric pkid straight into the path', () => {
    service.getById(1).subscribe();

    const req = httpMock.expectOne(`${base}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(item);
  });

  it('create POSTs to /featured-promo-items', () => {
    service.create(request).subscribe();

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(item);
  });

  it('update PUTs to /featured-promo-items with the key in the body and NO id segment', () => {
    service.update({ ...request, pkid: 3 }).subscribe();

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('PUT');
    expect(req.request.url).toBe(base);
    expect(req.request.body.pkid).toBe(3);
    req.flush(null);
  });

  it('remove DELETEs /featured-promo-items/{pkid}', () => {
    service.remove(3).subscribe();

    const req = httpMock.expectOne(`${base}/3`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('move POSTs the direction to /{pkid}/move', () => {
    service.move(7, 'down').subscribe();

    const req = httpMock.expectOne(`${base}/7/move`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ direction: 'down' });
    req.flush(null);
  });
});
