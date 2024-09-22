import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { catchError, delay, map, Observable, of, retry } from 'rxjs';
import config from '../config';

export interface LapEntry {
    lapNumber: number;
    lapTime: number;
    isInvalidLap: boolean;
    lapId: number;
}

export interface KartDriveData {
    kartId: string;
    kartName: string;
    //summary: LapSummary;
    laps: LapEntry[];
}

export interface LapSummary {
    totalLaps: number;
    validLaps: number;
    fastestLap: number;
    averageLapTime: number;
    fastestLapTime: number;
    slowestLapTime: number;
    consistency: number;
}

export interface SessionInfo {
    sessionId: string;
    name: string;
    startedAt: Date;
    weatherInfo: WeatherInfo;
}

export interface WeatherInfo {
    humidity: number;
    airTempC: number;
}

@Injectable({ providedIn: 'root' })
export class SessionService {
    constructor(private http: HttpClient) {}

    getSessions(day: string): Observable<SessionInfo[]> {
        return this.http.get<SessionInfo[]>(`${config.apiUri}/sessions/${day}`).pipe(
            map(infos =>
                infos?.map(info => ({
                    ...info,
                    startedAt: new Date(`${info.startedAt}Z`)
                }))
            ),
            retry({
                count: 3,
                delay: 1000
            }),
            catchError(() => {
                console.error('Could not get the data.');
                return of([]);
            })
        );

        return of([
            {
                sessionId: 'test-1',
                name: 'Session 1',
                startedAt: new Date(),
                weatherInfo: {
                    humidity: 10,
                    airTempC: 20
                }
            },
            {
                sessionId: 'test-2',
                name: 'Session 2',
                startedAt: new Date(),
                weatherInfo: {
                    humidity: 10,
                    airTempC: 20
                }
            }
        ]).pipe(delay(5000));
    }

    getKartDriveData(sessionId: string): Observable<KartDriveData[]> {
        return this.http.get<KartDriveData[]>(`${config.apiUri}/history/${sessionId}`).pipe(
            retry({
                count: 3,
                delay: 1000
            }),
            catchError(() => {
                console.error('Could not get the data.');
                return of([]);
            })
        );

        return of([
            {
                kartId: 'kart-1',
                kartName: 'Kart 1' + sessionId,
                /*summary: {
                    totalLaps: 2,
                    averageLapTime: 100,
                    fastestLapTime: 24.831
                },*/
                laps: [
                    {
                        lapNumber: 1,
                        lapTime: 24.831,
                        isInvalidLap: false,
                        lapId: 1
                    },
                    {
                        lapNumber: 2,
                        lapTime: 25.311,
                        isInvalidLap: false,
                        lapId: 2
                    }
                ]
            },
            {
                kartId: 'kart-2',
                kartName: 'Kart 2',
                /*summary: {
                    totalLaps: 2,
                    averageLapTime: 100,
                    fastestLapTime: 8825.311
                },*/
                laps: [
                    {
                        lapNumber: 1,
                        lapTime: 8824.831,
                        isInvalidLap: false,
                        lapId: 3
                    },
                    {
                        lapNumber: 2,
                        lapTime: 8825.311,
                        isInvalidLap: false,
                        lapId: 4
                    }
                ]
            }
        ]).pipe(delay(5000));
    }

    invalidateLap(lapId: number) {
        return this.http.put(`${config.apiUri}/history/laps/${lapId}/invalid`, null);
    }

    validateLap(lapId: number) {
        return this.http.put(`${config.apiUri}/history/laps/${lapId}/valid`, null);
    }

    getTotalLaps() {
        return this.http.get<number>(`${config.apiUri}/total-laps`);
    }

    getFirstDate() {
        return this.http.get<Date>(`${config.apiUri}/first-date`);
    }
}
