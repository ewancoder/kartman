import { Component, Input } from '@angular/core';
import { LapSummary } from '../sessions/session.service';
import { DecimalPipe } from '@angular/common';

@Component({
  selector: 'kman-drive-summary',
  standalone: true,
  imports: [DecimalPipe],
  templateUrl: './drive-summary.component.html',
  styleUrl: './drive-summary.component.scss'
})
export class DriveSummaryComponent {
  @Input({required: true}) summary!: LapSummary;
  @Input() compact: boolean = false;
}
