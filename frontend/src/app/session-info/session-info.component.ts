import { Component, Input } from '@angular/core';
import { SessionInfo } from '../sessions/session.service';
import { WeatherInfoComponent } from '../weather-info/weather-info.component';

@Component({
  selector: 'kman-session-info',
  standalone: true,
  imports: [WeatherInfoComponent],
  templateUrl: './session-info.component.html',
  styleUrl: './session-info.component.scss'
})
export class SessionInfoComponent {
  @Input({required: true}) session!: SessionInfo;
}
