import { AsyncPipe, DecimalPipe, NgClass } from '@angular/common';
import { Component, OnInit, signal, WritableSignal } from '@angular/core';
import { map, Observable, retry, share, switchMap, timer } from 'rxjs';
import { KartDriveData, SessionInfo, SessionService } from '../sessions/session.service';

@Component({
    selector: 'kman-timing',
    standalone: true,
    imports: [AsyncPipe, DecimalPipe, NgClass],
    templateUrl: './timing.component.html',
    styleUrl: './timing.component.scss'
})
export class TimingComponent implements OnInit {
    constructor(private sessionService: SessionService) {}
    private currentSessionId: string | undefined;
    timing$Signal: WritableSignal<Observable<KartTiming[]> | undefined> = signal(undefined);

    ngOnInit(): void {
        const sessions$ = this.sessionService.getSessions('today');
        const polledData$ = timer(0, 20000).pipe(
            switchMap(() => sessions$),
            retry(3),
            share()
        );

        polledData$.subscribe(sessions => {
            if (sessions[0].sessionId !== this.currentSessionId) {
                this.updateCurrentSession(sessions[0]);
            }
        });
    }

    private updateCurrentSession(session: SessionInfo) {
        this.currentSessionId = session.sessionId;

        const data$ = this.sessionService.getKartDriveData(this.currentSessionId);
        const polledData$ = timer(0, 3000).pipe(
            switchMap(() => data$),
            retry(3),
            share()
        );

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
}

export interface KartTiming {
    kartName: string;
    bestLapTime: number;
    minLapN: number;
    lastLapTime: number;
    delta: number;
    totalLaps: number;
}
