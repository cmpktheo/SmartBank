import { Component, input, output } from '@angular/core';
import { LucideChevronRight } from '@lucide/angular';
import type { CardDto } from '../../../../core/models/models';

@Component({
  selector: 'sb-card-list',
  standalone: true,
  imports: [LucideChevronRight],
  templateUrl: './card-list.component.html',
  styleUrl: './card-list.component.scss',
})
export class CardListComponent {
  cards = input<CardDto[]>([]);
  select = output<string>();

  isVisa(card: CardDto): boolean {
    const brand = card.brand?.toLowerCase();
    if (brand) return brand === 'visa';
    return true;
  }

  isMastercard(card: CardDto): boolean {
    return card.brand?.toLowerCase() === 'mastercard';
  }
}
