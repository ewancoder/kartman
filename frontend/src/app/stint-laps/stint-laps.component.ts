import { NgClass } from '@angular/common';
import { Component, Input } from '@angular/core';
import { LapEntry } from '../session.service';
import { StintLap, StintLapComponent } from '../stint-lap/stint-lap.component';

@Component({
    selector: 'kman-stint-laps',
    standalone: true,
    imports: [StintLapComponent, NgClass],
    templateUrl: './stint-laps.component.html',
    styleUrl: './stint-laps.component.scss'
})
export class StintLapsComponent {
    @Input({ required: true, transform: toStintLap }) laps!: StintLap[];
}

function toStintLap(laps: LapEntry[]): StintLap[] {
    const fastestLapTime = Math.min(...laps.filter(lap => !lap.isInvalidLap).map(lap => lap.lapTime));

    return laps.map(lap => ({
        ...lap,
        fastest: lap.lapTime === fastestLapTime
    }));
}
