import { NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { LapEntry, SessionService } from '../sessions/session.service';

@Component({
    selector: 'kman-stint-lap',
    standalone: true,
    imports: [NgClass],
    templateUrl: './stint-lap.component.html',
    styleUrl: './stint-lap.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class StintLapComponent {
    @Input({ required: true }) lap!: LapEntry;
    @Input() fastest = false;

    constructor(private service: SessionService) {}

    toggleLapValid(lap: LapEntry) {
        if (lap.isInvalidLap) {
            this.validateLap(lap);
        } else {
            this.invalidateLap(lap);
        }
    }

    invalidateLap(lap: LapEntry) {
        // Updating doesn't work without change detection.
        this.service.invalidateLap(lap.lapId).subscribe(() => (lap.isInvalidLap = true));
    }

    validateLap(lap: LapEntry) {
        // Updating doesn't work without change detection.
        this.service.validateLap(lap.lapId).subscribe(() => (lap.isInvalidLap = false));
    }
}
