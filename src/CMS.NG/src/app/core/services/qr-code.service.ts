import { Injectable } from '@angular/core';
import { toDataURL, QRCodeToDataURLOptions } from 'qrcode';

/**
 * Thin injectable wrapper over the `qrcode` library. Kept as a service (rather than importing the
 * library straight into components) so features depend on an Angular seam that is trivial to mock in
 * unit tests, and so the encoder can be swapped without touching any component.
 */
@Injectable({ providedIn: 'root' })
export class QrCodeService {
  /**
   * Encodes text into a PNG data URL (<c>data:image/png;base64,…</c>) suitable both for an
   * <c>&lt;img src&gt;</c> and for a download. Level M gives a good size/robustness balance for URLs.
   */
  toDataUrl(text: string, options?: QRCodeToDataURLOptions): Promise<string> {
    return toDataURL(text, { errorCorrectionLevel: 'M', margin: 2, width: 240, ...options });
  }
}
