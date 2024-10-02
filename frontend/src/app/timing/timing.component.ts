import { AsyncPipe, DecimalPipe, NgClass } from '@angular/common';
import { Component, OnDestroy, OnInit, signal, WritableSignal } from '@angular/core';
import { map, Observable, retry, share, Subscription, switchMap, timer } from 'rxjs';
import { KartDriveData, SessionInfo, SessionService } from '../session.service';

@Component({
    selector: 'kman-timing',
    standalone: true,
    imports: [AsyncPipe, DecimalPipe, NgClass],
    templateUrl: './timing.component.html',
    styleUrl: './timing.component.scss'
})
export class TimingComponent implements OnInit, OnDestroy {
    constructor(private sessionService: SessionService) {}
    private currentSessionId: string | undefined;
    timing$Signal: WritableSignal<Observable<KartTiming[]> | undefined> = signal(undefined);
    private _subscription!: Subscription;

    ngOnInit(): void {
        const sessions$ = this.sessionService.getSessions('today');
        const polledData$ = timer(0, 10000).pipe(
            switchMap(() => sessions$),
            retry(3),
            share()
        );

        this._subscription = polledData$.subscribe(sessions => {
            if (sessions[0].sessionId !== this.currentSessionId) {
                this.updateCurrentSession(sessions[0]);
            }
        });
    }

    ngOnDestroy(): void {
        this._subscription.unsubscribe();
    }

    private updateCurrentSession(session: SessionInfo) {
        this.currentSessionId = session.sessionId;

        const data$ = this.sessionService.getKartDriveData(this.currentSessionId);
        const polledData$ = timer(0, 3000).pipe(
            switchMap(() => data$),
            retry(3),
            share()
        );

        // TODO: Consider using distinctUntilChanged. But we need to check ALL values here. Probably doesn't worth it.

        this.timing$Signal?.set(polledData$.pipe(map(data => data.map(x => this.getKartData(x)))));
    }

    private getKartData(data: KartDriveData): KartTiming {
        const kartName = data.kartName;
        const bestLapTime = Math.min(...data.laps.map(lap => lap.lapTime));
        const minLapN = data.laps.find(lap => lap.lapTime === bestLapTime)?.lapNumber;
        const lastLapTime = data.laps.at(-1)?.lapTime;
        const delta = lastLapTime! - bestLapTime;
        const totalLaps = data.laps.length;

        return {
            kartName: kartName,
            bestLapTime: bestLapTime,
            minLapN: minLapN!,
            lastLapTime: lastLapTime!,
            delta: delta,
            totalLaps: totalLaps
        };
    }

    generateKartColor(kartNumber: number, totalKarts = 88) {
        const hue = ((kartNumber * 137) % totalKarts) * (360 / totalKarts);
        const saturation = 90;
        const lightness = 50;
        return `hsl(${hue}, ${saturation}%, ${lightness}%)`;
    }
}

export interface KartTiming {
    kartName: string;
    bestLapTime: number;
    minLapN: number;
    lastLapTime: number;
    delta: number;
    totalLaps: number;
}
