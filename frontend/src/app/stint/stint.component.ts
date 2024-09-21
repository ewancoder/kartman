import { NgClass } from '@angular/common';
import { ChangeDetectorRef, Component, Input } from '@angular/core';
import { KartInfoComponent } from '../kart-info/kart-info.component';
import { KartInfo } from '../kart-info/kart-info.model';
import { KartDriveData, SessionService } from '../session.service';
import { StintLapsComponent } from '../stint-laps/stint-laps.component';
import { StintSummaryComponent } from '../stint-summary/stint-summary.component';

@Component({
    selector: 'kman-stint',
    standalone: true,
    imports: [KartInfoComponent, StintLapsComponent, StintSummaryComponent, NgClass],
    templateUrl: './stint.component.html',
    styleUrl: './stint.component.scss'
})
export class StintComponent {
    @Input({ required: true }) data!: KartDriveData;
    @Input() hideControls = false;

    constructor(
        private sessionService: SessionService,
        private cdr: ChangeDetectorRef
    ) {}

    getKartInfo(data: KartDriveData): KartInfo {
        return {
            kartId: data.kartId,
            name: data.kartName
        };
    }

    invalidateLap() {
        const lapNumber = prompt('Enter lap number');

        const index = this.data.laps.findIndex(lap => lap.lapNumber === +(lapNumber ?? -1));

        if (index >= 0) {
            const lap = this.data.laps.find(lap => lap.lapNumber === +(lapNumber ?? -1));
            const newLap = Object.assign({}, lap);

            if (newLap.isInvalidLap) {
                console.log('validating');
                this.sessionService.validateLap(newLap.lapId).subscribe(() => {
                    newLap.isInvalidLap = false;
                    this.data.laps[index] = newLap;
                    this.data.laps = [...this.data.laps];
                    this.cdr.detectChanges(); // TODO: Figure out how not to call this.
                });
            } else {
                console.log('invalidating');
                this.sessionService.invalidateLap(newLap.lapId).subscribe(() => {
                    newLap.isInvalidLap = true;
                    this.data.laps[index] = newLap;
                    this.data.laps = [...this.data.laps];
                    this.cdr.detectChanges();
                });
            }
        }
    }
}
