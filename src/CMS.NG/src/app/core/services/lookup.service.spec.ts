import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@env';

import { LookupService } from './lookup.service';
import { AppUserLookup, appUserLabel } from '@core/models/app-user.model';

describe('LookupService', () => {
  let service: LookupService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [LookupService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(LookupService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('appUsers issues GET /lookups/app-users', () => {
    const users: AppUserLookup[] = [
      { userId: 'miles@uuu.com.tw', userName: 'Miles Sun', isActive: true }
    ];

    service.appUsers().subscribe(result => expect(result).toEqual(users));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/lookups/app-users`);
    expect(req.request.method).toBe('GET');
    req.flush(users);
  });
});

describe('appUserLabel', () => {
  it('renders "UserName (UserId)"', () => {
    expect(appUserLabel({ userId: 'helen', userName: 'helen', isActive: true }))
      .toBe('helen (helen)');
    expect(appUserLabel({ userId: 'miles@uuu.com.tw', userName: 'Miles Sun', isActive: true }))
      .toBe('Miles Sun (miles@uuu.com.tw)');
  });
});
