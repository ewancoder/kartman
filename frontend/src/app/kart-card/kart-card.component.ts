import { Component, Input } from '@angular/core';
import { StintSummaryComponent } from '../stint-summary/stint-summary.component';
import { KartInfo, KartInfoComponent } from '../kart-info/kart-info.component';
import { LapGroupComponent } from '../lap-group/lap-group.component';
import { KartDriveData, LapSummary } from '../sessions/session.service';

@Component({
    selector: 'kman-kart-card',
    standalone: true,
    imports: [KartInfoComponent, LapGroupComponent, StintSummaryComponent],
    templateUrl: './kart-card.component.html',
    styleUrl: './kart-card.component.scss'
})
export class KartCardComponent {
    @Input({ required: true }) data!: KartDriveData;

    getKartInfo(data: KartDriveData): KartInfo {
        return {
            kartId: data.kartId,
            name: data.kartName
        };
    }

    // TODO: Consider moving this method inside drive-summary component.
    getSummary(entry: KartDriveData): LapSummary {
        // TODO: Consider getting this from backend to avoid calculations on frontend.
        const totalAllLaps = entry.laps.length;
        const totalTrueLaps = entry.laps.length - 4;

        const allTimes = entry.laps.map(lap => lap.lapTime);
        const trueTimes = entry.laps.slice(2, -2).map(lap => lap.lapTime);

        const fastestLapTime = Math.min(...allTimes);
        let slowestLapTime = Math.max(...allTimes);
        let averageLapTime = allTimes.reduce((a, b) => a + b) / totalAllLaps;

        const fastestLap: number = entry.laps.find(
            lap => lap.lapTime === fastestLapTime
        )!.lapNumber;

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
}
