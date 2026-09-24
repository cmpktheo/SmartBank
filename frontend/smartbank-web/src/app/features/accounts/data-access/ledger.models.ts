export interface RecentTx {
  transactionId: string;
  reference: string;
  bookedAt: string;
  direction: string;
  kind: string;
  amount: string;
  currency: string;
  balanceAfter: string;
  counterpartyIban?: string | null;
}

export interface CurrencyTotal {
  currency: string;
  total: string;
}
