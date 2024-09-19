import { NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { LapEntry } from '../session.service';

@Component({
    selector: 'kman-stint-lap',
    standalone: true,
    imports: [NgClass],
    templateUrl: './stint-lap.component.html',
    styleUrl: './stint-lap.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class StintLapComponent {
    @Input({ required: true }) lap!: LapEntry;
    @Input() fastest = false;
}
