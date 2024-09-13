import { Component, Input } from '@angular/core';
import { LapEntry } from '../sessions/session.service';
import { LapComponent, LapRow } from '../lap/lap.component';
import { NgClass } from '@angular/common';

@Component({
  selector: 'kman-lap-group',
  standalone: true,
  imports: [LapComponent, NgClass],
  templateUrl: './lap-group.component.html',
  styleUrl: './lap-group.component.scss'
})
export class LapGroupComponent {
  @Input({required: true}) laps!: LapEntry[];
  lapRows: LapRow[] | undefined;

  ngOnInit() {
    const minLaptime = Math.min(...this.laps.map(entry => entry.lapTime));
    this.lapRows = this.laps.map(entry => {
      return {
        ...entry,
        fastest: entry.lapTime === minLaptime
      };
    });
  }
}
