import { Component, Input } from '@angular/core';
import { KartInfo } from './kart-info.model';

@Component({
    selector: 'kman-kart-info',
    standalone: true,
    imports: [],
    templateUrl: './kart-info.component.html',
    styleUrl: './kart-info.component.scss'
})
export class KartInfoComponent {
    @Input({ required: true }) kartInfo!: KartInfo;
}
