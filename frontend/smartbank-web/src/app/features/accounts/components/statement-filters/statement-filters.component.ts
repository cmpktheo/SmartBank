import { Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ComboBoxComponent, type ComboOption } from '../../../../shared/ui/combo-box.component';

export interface StatementFilterValue {
  from: string;
  to: string;
  type: string;
  kind: string;
}

@Component({
  selector: 'sb-statement-filters',
  standalone: true,
  imports: [FormsModule, ComboBoxComponent],
  templateUrl: './statement-filters.component.html',
  styleUrl: './statement-filters.component.scss',
})
export class StatementFiltersComponent {
  from = input('');
  to = input('');
  type = input('All');
  kind = input('All');
  typeOptions = input<ComboOption[]>([]);
  kindOptions = input<ComboOption[]>([]);

  fromChange = output<string>();
  toChange = output<string>();
  typeChange = output<string>();
  kindChange = output<string>();
  apply = output<void>();
}
