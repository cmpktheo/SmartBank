export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  expiresAt: number;
}

export interface LoginResponse {
  mfaRequired: boolean;
  challengeId?: string;
  expiresInSeconds?: number;
  accessToken?: string;
  refreshToken?: string;
  expiresIn?: number;
  tokenType?: string;
  customerId?: string;
}

export interface AccountSummary {
  id: string;
  alias: string;
  iban: string;
  ibanFormatted: string;
  type: 'Current' | 'Savings';
  status: 'Active' | 'Frozen' | 'Closed';
  currency: 'EUR' | 'USD' | 'GBP';
  availableBalance: string;
  postedBalance: string;
}

export interface TransferRequest {
  sourceAccountId: string;
  destinationIban: string;
  amount: string;
  currency: string;
  transferType: string;
  narrative?: string;
}

export interface CardDto {
  id: string;
  accountId: string;
  type?: string;
  brand?: string;
  maskedPan: string;
  lastFour: string;
  expiryMonth: number;
  expiryYear: number;
  status: string;
  dailyEcommerceLimit: string;
  dailyAtmLimit: string;
  currency: string;
}
