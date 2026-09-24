import { describe, it, expect } from 'vitest';
import { isValidIban, compactIban, formatIban } from './iban';

describe('iban', () => {
  it('valid DE89370400440532013000', () => {
    expect(isValidIban('DE89370400440532013000')).toBe(true);
  });
  it('invalid checksum', () => {
    expect(isValidIban('DE89370400440532013001')).toBe(false);
  });
  it('lowercase and spaces normalize', () => {
    expect(isValidIban('de89 3704 0044 0532 0130 00')).toBe(true);
    expect(compactIban('de89 3704 0044 0532 0130 00')).toBe('DE89370400440532013000');
  });

  it('rejects empty and too-short input', () => {
    expect(isValidIban('')).toBe(false);
    expect(isValidIban('DE89')).toBe(false);
    expect(isValidIban('DE8937040044')).toBe(false); // < 15 chars
  });

  it('rejects too-long input (> 34 chars)', () => {
    expect(isValidIban('DE89370400440532013000123456789012')).toBe(false);
  });

  it('rejects bad country/checksum pattern', () => {
    expect(isValidIban('1289370400440532013000')).toBe(false); // no country letters
    expect(isValidIban('DEXX370400440532013000')).toBe(false); // non-digit check
    expect(isValidIban('DE8937040044053201300!')).toBe(false); // stripped -> bad checksum/length
  });

  it('accepts other countries and validates checksum', () => {
    expect(isValidIban('GB29NWBK60161331926819')).toBe(true);
    expect(isValidIban('GB29NWBK60161331926818')).toBe(false);
    expect(isValidIban('FR1420041010050500013M02606')).toBe(true);
  });

  it('compactIban strips separators and uppercases', () => {
    expect(compactIban('de89-3704.0044/0532 0130 00')).toBe('DE89370400440532013000');
    expect(compactIban('')).toBe('');
  });

  it('formatIban groups in fours', () => {
    expect(formatIban('DE89370400440532013000')).toBe('DE89 3704 0044 0532 0130 00');
    expect(formatIban('de89 3704 0044 0532 0130 00')).toBe('DE89 3704 0044 0532 0130 00');
    expect(formatIban('')).toBe('');
  });
});
