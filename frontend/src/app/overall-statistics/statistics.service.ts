import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, retry, share, switchMap, timer } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class StatisticsService {
    totalLaps$: Observable<number>;

    constructor(private http: HttpClient) {
        const totalLaps$ = this.http.get<number>(`https://api.kartman.typingrealm.com/api/total-laps`);
        const polledData$ = timer(0, 3000).pipe(
            switchMap(() => totalLaps$),
            retry(3),
            share()
        );

        this.totalLaps$ = polledData$;
    }
}
