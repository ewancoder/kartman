import { Component, Input } from '@angular/core';
import { KartInfo, KartInfoComponent } from '../kart-info/kart-info.component';
import { KartDriveData, LapSummary, SessionService } from '../sessions/session.service';
import { StintLapsComponent } from '../stint-laps/stint-laps.component';
import { StintSummaryComponent } from '../stint-summary/stint-summary.component';

@Component({
    selector: 'kman-stint',
    standalone: true,
    imports: [KartInfoComponent, StintLapsComponent, StintSummaryComponent],
    templateUrl: './stint.component.html',
    styleUrl: './stint.component.scss'
})
export class StintComponent {
    @Input({ required: true }) data!: KartDriveData;

    constructor(private sessionService: SessionService) {}

    getKartInfo(data: KartDriveData): KartInfo {
        return {
            kartId: data.kartId,
            name: data.kartName
        };
    }

    // TODO: Consider moving this method inside drive-summary component.
    getSummary(entry: KartDriveData): LapSummary {
        const validLaps = entry.laps.filter(lap => !lap.isInvalidLap);

        // TODO: Consider getting this from backend to avoid calculations on frontend.
        const totalAllLaps = validLaps.length;
        const totalTrueLaps = validLaps.length - 4;

        const allTimes = validLaps.map(lap => lap.lapTime);
        const trueTimes = validLaps.slice(2, -2).map(lap => lap.lapTime);

        const fastestLapTime = Math.min(...allTimes);
        let slowestLapTime = Math.max(...allTimes);
        let averageLapTime = allTimes.reduce((a, b) => a + b) / totalAllLaps;

        const fastestLap: number = validLaps.find(lap => lap.lapTime === fastestLapTime)!.lapNumber;

        if (totalTrueLaps > 0) {
            //fastestLapTime = Math.min(...trueTimes);
            slowestLapTime = Math.max(...trueTimes);
            averageLapTime = trueTimes.reduce((a, b) => a + b) / totalTrueLaps;
        }

        const consistency = slowestLapTime - fastestLapTime;

        return {
            fastestLap: fastestLap,
            fastestLapTime: fastestLapTime,
            totalLaps: totalAllLaps,
            averageLapTime: averageLapTime,
            slowestLapTime: slowestLapTime,
            consistency: consistency
        };
    }

    invalidateLap() {
        const lapNumber = prompt('Enter lap number');
        const lap = this.data.laps.find(lap => lap.lapNumber === +(lapNumber ?? -1));

        if (lap) {
            if (lap.isInvalidLap) {
                this.sessionService.validateLap(lap.lapId).subscribe();
            } else {
                this.sessionService.invalidateLap(lap.lapId).subscribe();
            }
        }
    }
}
