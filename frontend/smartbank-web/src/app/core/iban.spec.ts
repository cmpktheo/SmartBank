import { describe, it, expect } from 'vitest';
import { isValidIban, compactIban } from './iban';

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
});
