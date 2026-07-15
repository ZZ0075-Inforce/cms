import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';

import { PartnerDetail } from './partner-detail';
import { PartnerService } from '@core/services/partner.service';
import { Partner } from '@core/models/partner.model';

describe('PartnerDetail', () => {
  let fixture: ComponentFixture<PartnerDetail>;
  let service: jasmine.SpyObj<PartnerService>;

  const partner: Partner = {
    pkid: 1,
    name: 'Microsoft',
    appKey: 'MS',
    nameOnPartnerMenu: 'Microsoft 微軟',
    nameOnCourseDetailPage: '微軟',
    displayOrder: 10,
    imageFilename: 'ms.png',
    courseCount: 7
  };

  async function setup(failWith?: number): Promise<void> {
    service = jasmine.createSpyObj<PartnerService>('PartnerService', ['getById', 'remove']);

    service.getById.and.returnValue(
      failWith
        ? throwError(() => new HttpErrorResponse({ status: failWith }))
        : of(partner)
    );
    service.remove.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [PartnerDetail],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        { provide: PartnerService, useValue: service },
        MessageService,
        ConfirmationService,
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ id: '1' }) } }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PartnerDetail);
    fixture.detectChanges();
  }

  it('loads the partner named by the route param, as a number', async () => {
    await setup();

    expect(service.getById).toHaveBeenCalledWith(1);
  });

  it('renders every partner field', async () => {
    await setup();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Microsoft');
    expect(text).toContain('MS');
    expect(text).toContain('微軟');
    expect(text).toContain('ms.png');
    expect(text).toContain('7');   // 對應課程數
  });

  it('shows a not-found state when the partner is missing', async () => {
    await setup(404);

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('找不到這個廠商');
  });
});
