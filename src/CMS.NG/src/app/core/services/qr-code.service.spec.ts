import { TestBed } from '@angular/core/testing';

import { QrCodeService } from './qr-code.service';

describe('QrCodeService', () => {
  let service: QrCodeService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [QrCodeService] });
    service = TestBed.inject(QrCodeService);
  });

  it('encodes text into a real PNG image data URL', async () => {
    const url = await service.toDataUrl('https://www.uuu.com.tw/Course/Show/1/AZ-900');

    expect(url.startsWith('data:image/png')).toBeTrue();
    expect(url.length).toBeGreaterThan(100);   // a non-trivial image, not an empty stub
  });

  it('produces distinct images for distinct inputs', async () => {
    const [a, b] = await Promise.all([service.toDataUrl('A'), service.toDataUrl('B')]);

    expect(a).not.toEqual(b);
  });
});
