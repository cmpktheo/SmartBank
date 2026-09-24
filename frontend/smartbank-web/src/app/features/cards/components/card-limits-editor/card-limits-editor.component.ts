import { Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import type { CardDto } from '../../../../core/models/models';

@Component({
  selector: 'sb-card-limits-editor',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './card-limits-editor.component.html',
  styleUrl: './card-limits-editor.component.scss',
})
export class CardLimitsEditorComponent {
  card = input.required<CardDto>();
  ecom = input.required<number>();
  atm = input.required<number>();
  saving = input(false);
  dirty = input(false);
  saveError = input<string | null>(null);

  ecomChange = output<number>();
  atmChange = output<number>();
  save = output<void>();
}
