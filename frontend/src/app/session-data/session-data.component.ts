import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { KartDriveData, LapSummary, SessionService } from '../sessions/session.service';
import { BehaviorSubject, Observable, retry, share, switchMap, tap, timer } from 'rxjs';
import { AsyncPipe, NgClass } from '@angular/common';
import { LoaderComponent } from '../loader/loader.component';
import { KartInfo, KartInfoComponent } from "../kart-info/kart-info.component";
import { LapGroupComponent } from '../lap-group/lap-group.component';
import { DriveSummaryComponent } from "../drive-summary/drive-summary.component";
import { Loader } from '../sessions/sessions.component';

@Component({
  selector: 'kman-session-data',
  standalone: true,
  imports: [AsyncPipe, LoaderComponent, KartInfoComponent, LapGroupComponent, DriveSummaryComponent, NgClass],
  templateUrl: './session-data.component.html',
  styleUrl: './session-data.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SessionDataComponent {
  data$: Observable<KartDriveData[]> | undefined;
  @Input({required: true}) sessionId!: string;
  @Input() polled: boolean = false;
  loading$: Observable<boolean> | undefined;

  constructor(private sessionService: SessionService) {}

  ngOnInit(): void {
    const data$ = this.sessionService.getKartDriveData(this.sessionId);
    const loader = new Loader(data$);

    const polledData$ = timer(0, 3000).pipe(
      switchMap(() => loader.data$),
      retry(3),
      share()
    );

    this.data$ = this.polled ? polledData$ : loader.data$;
    this.loading$ = loader.loading$;
  }

  getKartInfo(data: KartDriveData): KartInfo {
    return {
      kartId: data.kartId,
      name: data.kartName
    }
  }

  // TODO: Consider moving this method inside drive-summary component.
  getSummary(entry: KartDriveData): LapSummary {
    // TODO: Consider getting this from backend to avoid calculations on frontend.
    const totalAllLaps = entry.laps.length;
    const totalTrueLaps = entry.laps.length - 4;

    const allTimes = entry.laps.map(lap => lap.lapTime);
    const trueTimes = entry.laps.slice(2, -2).map(lap => lap.lapTime);

    let fastestLapTime = Math.min(...allTimes);
    let slowestLapTime = Math.max(...allTimes);
    let averageLapTime = allTimes.reduce((a, b) => a + b) / totalAllLaps;

    const fastestLap: number = entry.laps.find(lap => lap.lapTime === fastestLapTime)!.lapNumber;

    if (totalTrueLaps > 0) {
      //fastestLapTime = Math.min(...trueTimes);
      slowestLapTime = Math.max(...trueTimes);
      averageLapTime = trueTimes.reduce((a, b) => a + b) / totalTrueLaps;
    }

    const consistency = slowestLapTime - fastestLapTime;

    return {
      fastestLap: fastestLap,
      fastestLapTime: fastestLapTime,
      totalLaps: totalAllLaps,
      averageLapTime: averageLapTime,
      slowestLapTime: slowestLapTime,
      consistency: consistency
    }
  }
}
