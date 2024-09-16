import { ChangeDetectionStrategy, Component, Input, OnInit } from '@angular/core';
import { KartDriveData, LapSummary, SessionService } from '../sessions/session.service';
import {
    BehaviorSubject,
    Observable,
    retry,
    share,
    Subject,
    switchMap,
    takeUntil,
    tap,
    timer
} from 'rxjs';
import { AsyncPipe, NgClass } from '@angular/common';
import { LoaderComponent } from '../loader/loader.component';
import { KartInfo } from '../kart-info/kart-info.component';
import { Loader } from '../sessions/sessions.component';
import { KartCardComponent } from '../kart-card/kart-card.component';

@Component({
    selector: 'kman-session-data',
    standalone: true,
    imports: [AsyncPipe, LoaderComponent, NgClass, KartCardComponent],
    templateUrl: './session-data.component.html',
    styleUrl: './session-data.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class SessionDataComponent implements OnInit {
    data$: Observable<KartDriveData[]> | undefined;
    @Input() centered = false;
    @Input({ required: true }) sessionId!: string;
    private _polled = false;
    @Input() set polled(value: boolean) {
        this._polled = value;
        if (!value) {
            this.stopPolling$.next(true);
        }
    }
    loading$: Observable<boolean> | undefined;
    private stopPolling$ = new Subject<boolean>();

    constructor(private sessionService: SessionService) {}

    ngOnInit(): void {
        const data$ = this.sessionService.getKartDriveData(this.sessionId);
        const loader = new Loader(data$);

        const polledData$ = timer(0, 3000).pipe(
            takeUntil(this.stopPolling$),
            switchMap(() => loader.data$),
            retry(3),
            share()
        );

        this.data$ = this._polled ? polledData$ : loader.data$;
        this.loading$ = loader.loading$;
    }
}
