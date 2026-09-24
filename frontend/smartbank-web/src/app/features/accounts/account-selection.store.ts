import { Injectable, signal } from '@angular/core';

const STORAGE_KEY = 'sb.selectedAccountId';

/** Holds the currently selected account id in memory (+ sessionStorage for reloads). Keeps GUIDs out of the URL. */
@Injectable({ providedIn: 'root' })
export class AccountSelectionStore {
  private readonly _selectedId = signal<string | null>(this.readStored());

  readonly selectedId = this._selectedId.asReadonly();

  select(id: string): void {
    this._selectedId.set(id);
    try {
      sessionStorage.setItem(STORAGE_KEY, id);
    } catch {
      /* storage unavailable (private mode) — in-memory still works */
    }
  }

  clear(): void {
    this._selectedId.set(null);
    try {
      sessionStorage.removeItem(STORAGE_KEY);
    } catch {
      /* ignore */
    }
  }

  private readStored(): string | null {
    try {
      return sessionStorage.getItem(STORAGE_KEY);
    } catch {
      return null;
    }
  }
}
