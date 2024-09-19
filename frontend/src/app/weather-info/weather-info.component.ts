import { Component, Input } from '@angular/core';
import { WeatherInfo } from '../session.service';

@Component({
    selector: 'kman-weather-info',
    standalone: true,
    imports: [],
    templateUrl: './weather-info.component.html',
    styleUrl: './weather-info.component.scss'
})
export class WeatherInfoComponent {
    @Input({ required: true }) weather!: WeatherInfo;
}
