import { NgClass } from '@angular/common';
import { Component, Input, OnInit } from '@angular/core';
import { LapComponent } from '../lap/lap.component';
import { LapEntry } from '../sessions/session.service';

@Component({
    selector: 'kman-lap-group',
    standalone: true,
    imports: [LapComponent, NgClass],
    templateUrl: './lap-group.component.html',
    styleUrl: './lap-group.component.scss'
})
export class LapGroupComponent implements OnInit {
    @Input({ required: true }) laps!: LapEntry[];
    fastestLapTime!: number;

    ngOnInit() {
        this.fastestLapTime = Math.min(...this.laps.map(lap => lap.lapTime));
    }
}
