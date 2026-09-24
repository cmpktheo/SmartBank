import { Component, input, output } from '@angular/core';
import { LucideArrowLeft, LucideSnowflake } from '@lucide/angular';
import type { CardDto } from '../../../../core/models/models';
import { CardLimitsEditorComponent } from '../card-limits-editor/card-limits-editor.component';

@Component({
  selector: 'sb-card-detail',
  standalone: true,
  imports: [LucideArrowLeft, LucideSnowflake, CardLimitsEditorComponent],
  templateUrl: './card-detail.component.html',
  styleUrl: './card-detail.component.scss',
})
export class CardDetailComponent {
  card = input.required<CardDto>();
  cvv = input<string | null>(null);
  ecom = input.required<number>();
  atm = input.required<number>();
  saving = input(false);
  dirty = input(false);
  saveError = input<string | null>(null);

  back = output<void>();
  reveal = output<string>();
  toggleFreeze = output<CardDto>();
  ecomChange = output<number>();
  atmChange = output<number>();
  saveLimits = output<void>();

  isVisa(card: CardDto): boolean {
    const brand = card.brand?.toLowerCase();
    if (brand) return brand === 'visa';
    return true;
  }

  isMastercard(card: CardDto): boolean {
    return card.brand?.toLowerCase() === 'mastercard';
  }
}
