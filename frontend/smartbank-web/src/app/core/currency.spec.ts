import { describe, it, expect } from 'vitest';
import { getCurrencySymbol, getAmountPlaceholder } from './currency';

describe('currency', () => {
  it('maps known codes to symbols', () => {
    expect(getCurrencySymbol('EUR')).toBe('€');
    expect(getCurrencySymbol('USD')).toBe('$');
    expect(getCurrencySymbol('GBP')).toBe('£');
  });

  it('normalizes casing and whitespace', () => {
    expect(getCurrencySymbol(' eur ')).toBe('€');
  });

  it('falls back to code for unknown currencies', () => {
    expect(getCurrencySymbol('CHF')).toContain('CHF');
  });

  it('returns empty string for empty input', () => {
    expect(getCurrencySymbol('')).toBe('');
  });

  it('builds amount placeholder with symbol', () => {
    expect(getAmountPlaceholder('EUR')).toBe('1,000.00 €');
    expect(getAmountPlaceholder('USD')).toBe('1,000.00 $');
    expect(getAmountPlaceholder('GBP')).toBe('1,000.00 £');
  });
});
