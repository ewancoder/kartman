import { Routes } from '@angular/router';
import { SessionsViewComponent } from './sessions-view/sessions-view.component';
import { TimingComponent } from './timing/timing.component';

export const routes: Routes = [
    { path: '', redirectTo: '/sessions', pathMatch: 'full' },
    { path: 'sessions', redirectTo: `/sessions/today`, pathMatch: 'full' },
    { path: 'sessions/:day', component: SessionsViewComponent },
    { path: 'timing', component: TimingComponent }
];
