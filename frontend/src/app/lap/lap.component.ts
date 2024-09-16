import { NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { LapEntry } from '../sessions/session.service';

@Component({
    selector: 'kman-lap',
    standalone: true,
    imports: [NgClass],
    templateUrl: './lap.component.html',
    styleUrl: './lap.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class LapComponent {
    @Input({ required: true }) lap!: LapEntry;
    @Input() fastest: boolean = false;
}
