export function compactIban(raw: string): string {
  return raw.replace(/[^A-Za-z0-9]/g, '').toUpperCase();
}

export function isValidIban(raw: string): boolean {
  const iban = compactIban(raw);
  if (iban.length < 15 || iban.length > 34) return false;
  if (!/^[A-Z]{2}[0-9]{2}[A-Z0-9]+$/.test(iban)) return false;
  return mod97(iban.slice(4) + iban.slice(0, 4)) === 1;
}

export function formatIban(raw: string): string {
  const iban = compactIban(raw);
  return iban.replace(/(.{4})/g, '$1 ').trim();
}

function mod97(rearranged: string): number {
  const numeric = rearranged.replace(/[A-Z]/g, (c) => (c.charCodeAt(0) - 55).toString());
  let remainder = 0;
  for (let i = 0; i < numeric.length; i += 7) {
    const chunk = remainder.toString() + numeric.substring(i, i + 7);
    remainder = Number(chunk) % 97;
  }
  return remainder;
}
