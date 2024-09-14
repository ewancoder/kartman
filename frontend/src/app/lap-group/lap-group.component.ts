import { Component, Input } from '@angular/core';
import { LapEntry } from '../sessions/session.service';
import { LapComponent } from '../lap/lap.component';
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
  fastestLapTime!: number;

  ngOnInit() {
    this.fastestLapTime = Math.min(...this.laps.map(lap => lap.lapTime));
  }
}
