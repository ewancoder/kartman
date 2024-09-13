import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { KartDriveData, LapSummary, SessionService } from '../sessions/session.service';
import { BehaviorSubject, Observable, share, tap } from 'rxjs';
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
  loading$: Observable<boolean> | undefined;

  constructor(private sessionService: SessionService) {}

  ngOnInit(): void {
    const data$ = this.sessionService.getKartDriveData(this.sessionId);
    const loader = new Loader(data$);

    this.data$ = loader.data$;
    this.loading$ = loader.loading$;
  }

  getKartInfo(data: KartDriveData): KartInfo {
    return {
      kartId: data.kartId,
      name: data.kartName
    }
  }

  getSummary(entry: KartDriveData): LapSummary {
    // TODO: Consider getting this from backend to avoid calculations on frontend.
    const totalLaps = entry.laps.length;
    const fastestLapTime: number = Math.min(...entry.laps.map(lap => lap.lapTime));
    const fastestLap: number = entry.laps.find(lap => lap.lapTime === fastestLapTime)!.lapNumber;
    const averageLapTime = entry.laps.map(lap => lap.lapTime).reduce((a, b) => a + b) / totalLaps;

    return {
      fastestLap: fastestLap,
      fastestLapTime: fastestLapTime,
      totalLaps: totalLaps,
      averageLapTime: averageLapTime
    }
  }
}
