import { DecimalPipe } from '@angular/common';
import { Component, Input } from '@angular/core';
import { LapSummary } from '../sessions/session.service';

@Component({
    selector: 'kman-stint-summary',
    standalone: true,
    imports: [DecimalPipe],
    templateUrl: './stint-summary.component.html',
    styleUrl: './stint-summary.component.scss'
})
export class StintSummaryComponent {
    @Input({ required: true }) summary!: LapSummary;
    @Input() compact = false;
}
