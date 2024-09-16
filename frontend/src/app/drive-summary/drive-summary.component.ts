import { DecimalPipe } from '@angular/common';
import { Component, Input } from '@angular/core';
import { LapSummary } from '../sessions/session.service';

@Component({
    selector: 'kman-drive-summary',
    standalone: true,
    imports: [DecimalPipe],
    templateUrl: './drive-summary.component.html',
    styleUrl: './drive-summary.component.scss'
})
export class DriveSummaryComponent {
    @Input({ required: true }) summary!: LapSummary;
    @Input() compact = false;
}
