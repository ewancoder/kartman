import { NgClass } from '@angular/common';
import { Component, Input, OnChanges, OnInit } from '@angular/core';
import { LapEntry } from '../sessions/session.service';
import { StintLapComponent } from '../stint-lap/stint-lap.component';

@Component({
    selector: 'kman-stint-laps',
    standalone: true,
    imports: [StintLapComponent, NgClass],
    templateUrl: './stint-laps.component.html',
    styleUrl: './stint-laps.component.scss'
})
export class StintLapsComponent implements OnInit, OnChanges {
    @Input({ required: true }) laps!: LapEntry[];
    fastestLapTime!: number;

    ngOnInit() {
        this.fastestLapTime = Math.min(...this.laps.filter(lap => !lap.isInvalidLap).map(lap => lap.lapTime));
    }

    ngOnChanges() {
        // TODO: Figure out how to do this without ngOnChanges hook.
        // This happens X = KARTS times per LATEST session.
        console.log('changes');
        this.fastestLapTime = Math.min(...this.laps.filter(lap => !lap.isInvalidLap).map(lap => lap.lapTime));
    }
}
