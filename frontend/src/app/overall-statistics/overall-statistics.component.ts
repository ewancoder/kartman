import { AsyncPipe, DecimalPipe } from '@angular/common';
import { Component } from '@angular/core';
import { Observable } from 'rxjs';
import { StatisticsService } from './statistics.service';

@Component({
    selector: 'kman-overall-statistics',
    standalone: true,
    imports: [AsyncPipe, DecimalPipe],
    templateUrl: './overall-statistics.component.html',
    styleUrl: './overall-statistics.component.scss'
})
export class OverallStatisticsComponent {
    totalLaps$: Observable<number>;

    constructor(private statistics: StatisticsService) {
        this.totalLaps$ = statistics.totalLaps$;
    }
}
