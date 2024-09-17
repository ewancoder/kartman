import { NgClass } from '@angular/common';
import { Component, Input, OnInit } from '@angular/core';
import { StintLapComponent } from '../stint-lap/stint-lap.component';
import { LapEntry } from '../sessions/session.service';

@Component({
    selector: 'kman-stint-laps',
    standalone: true,
    imports: [StintLapComponent, NgClass],
    templateUrl: './stint-laps.component.html',
    styleUrl: './stint-laps.component.scss'
})
export class StintLapsComponent implements OnInit {
    @Input({ required: true }) laps!: LapEntry[];
    fastestLapTime!: number;

    ngOnInit() {
        this.fastestLapTime = Math.min(...this.laps.map(lap => lap.lapTime));
    }
}
