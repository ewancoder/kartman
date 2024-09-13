import { NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { LapEntry } from '../sessions/session.service';

export interface LapRow extends LapEntry {
  fastest: boolean
}

@Component({
  selector: 'kman-lap',
  standalone: true,
  imports: [NgClass],
  templateUrl: './lap.component.html',
  styleUrl: './lap.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LapComponent {
  @Input({required: true}) lap!: LapRow;
}
