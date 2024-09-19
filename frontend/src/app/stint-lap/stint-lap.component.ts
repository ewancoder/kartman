import { NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { LapEntry } from '../session.service';

export interface StintLap extends LapEntry {
    fastest: boolean;
}

@Component({
    selector: 'kman-stint-lap',
    standalone: true,
    imports: [NgClass],
    templateUrl: './stint-lap.component.html',
    styleUrl: './stint-lap.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class StintLapComponent {
    @Input({ required: true }) lap!: StintLap;
}
