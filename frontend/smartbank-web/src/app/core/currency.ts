const FALLBACK_SYMBOLS: Record<string, string> = { EUR: '€', USD: '$', GBP: '£' };

export function getCurrencySymbol(code: string, locale?: string): string {
  const normalized = (code ?? '').trim().toUpperCase();
  if (!normalized) return '';
  try {
    const parts = new Intl.NumberFormat(locale ?? undefined, {
      style: 'currency',
      currency: normalized,
      currencyDisplay: 'narrowSymbol',
    }).formatToParts(0);
    const currencyPart = parts.find((p) => p.type === 'currency')?.value;
    if (currencyPart) return currencyPart;
  } catch {
    // Invalid/unknown currency code — fall through to map below.
  }
  return FALLBACK_SYMBOLS[normalized] ?? normalized;
}

export function getAmountPlaceholder(code: string, locale?: string): string {
  return `1,000.00 ${getCurrencySymbol(code, locale)}`.trim();
}
