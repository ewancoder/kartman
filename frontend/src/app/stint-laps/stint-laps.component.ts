import { NgClass } from '@angular/common';
import { Component, Input } from '@angular/core';
import { LapEntry } from '../session.service';
import { StintLapComponent } from '../stint-lap/stint-lap.component';

@Component({
    selector: 'kman-stint-laps',
    standalone: true,
    imports: [StintLapComponent, NgClass],
    templateUrl: './stint-laps.component.html',
    styleUrl: './stint-laps.component.scss'
})
export class StintLapsComponent {
    @Input({ required: true }) laps!: LapEntry[];

    getFastestLap() {
        return Math.min(...this.laps.filter(lap => !lap.isInvalidLap).map(lap => lap.lapTime));
    }
}
