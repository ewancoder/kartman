import { DecimalPipe } from '@angular/common';
import { Component, Input } from '@angular/core';
import { LapEntry, LapSummary } from '../session.service';

@Component({
    selector: 'kman-stint-summary',
    standalone: true,
    imports: [DecimalPipe],
    templateUrl: './stint-summary.component.html',
    styleUrl: './stint-summary.component.scss'
})
export class StintSummaryComponent {
    summary!: LapSummary;
    @Input({ required: true })
    set laps(laps: LapEntry[]) {
        this.summary = this.getSummary(laps);
    }

    private getSummary(laps: LapEntry[]): LapSummary {
        const validLaps = laps.filter(lap => !lap.isInvalidLap);

        // TODO: Consider getting this from backend to avoid calculations on frontend.
        const totalAllLaps = validLaps.length;
        const totalTrueLaps = validLaps.length >= 8 ? validLaps.length - 4 : validLaps.length;

        const allTimes = validLaps.map(lap => lap.lapTime);
        const trueTimes =
            validLaps.length >= 8 ? validLaps.slice(2, -2).map(lap => lap.lapTime) : validLaps.map(lap => lap.lapTime);

        const fastestLapTime = allTimes.length === 0 ? 0 : Math.min(...allTimes);
        let slowestLapTime = allTimes.length === 0 ? 0 : Math.max(...allTimes);
        let averageLapTime = allTimes.length === 0 ? 0 : allTimes[0];
        if (allTimes.length > 1) {
            averageLapTime = allTimes.reduce((a, b) => a + b) / totalAllLaps;
        }

        const fastestLap: number = validLaps.find(lap => lap.lapTime === fastestLapTime)?.lapNumber ?? 0;

        if (totalTrueLaps > 0) {
            //fastestLapTime = Math.min(...trueTimes);
            slowestLapTime = Math.max(...trueTimes);
            averageLapTime = trueTimes.reduce((a, b) => a + b) / totalTrueLaps;
        }

        const consistency = slowestLapTime - fastestLapTime;

        return {
            fastestLap: fastestLap,
            fastestLapTime: fastestLapTime,
            totalLaps: laps.length,
            validLaps: totalAllLaps,
            averageLapTime: averageLapTime,
            slowestLapTime: slowestLapTime,
            consistency: consistency
        };
    }
}
